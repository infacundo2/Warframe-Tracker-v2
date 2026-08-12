using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WarframeInventory.Contracts.AgentV1;
using WarframeInventory.Data;
using WarframeInventory.Models;

namespace WarframeInventory.Services;

public sealed class AgentInventoryIngestionService
{
    private static readonly TimeSpan StageLifetime = TimeSpan.FromMinutes(30);
    private const string ServerComputedHashMarker = "server-computed";
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();
    public AgentInventoryIngestionService(IDbContextFactory<ApplicationDbContext> dbFactory,
        IMemoryCache cache) => (_dbFactory, _cache) = (dbFactory, cache);

    public async Task<AgentInventoryPreviewResponse> PreviewAsync(Guid deviceId, string userId,
        AgentInventorySnapshot snapshot, CancellationToken ct)
    {
        Validate(snapshot);
        var gate = _locks.GetOrAdd(deviceId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var receivedHash = ComputeHash(snapshot);
            var existing = await db.InventorySyncBatches.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == snapshot.BatchId && x.DeviceId == deviceId, ct);
            if (existing is not null)
            {
                if (!FixedHashEquals(existing.ContentHash, receivedHash))
                    throw new AgentInventoryException("batch_content_mismatch");
                return new(existing.Id, existing.Status, snapshot.Items.Count,
                    snapshot.Items.Sum(x => x.Quantity), 0, existing.ChangedRecords,
                    existing.IsAuthoritative);
            }
            var device = await db.AgentDevices.FirstOrDefaultAsync(x => x.Id == deviceId
                && x.UserId == userId && x.RevokedUtc == null, ct)
                ?? throw new AgentInventoryException("device_revoked");
            if (snapshot.Sequence <= device.LastSequence)
                throw new AgentInventoryException("stale_sequence");
            if (snapshot.CapturedUtc < DateTime.UtcNow.AddHours(-24)
                || snapshot.CapturedUtc > DateTime.UtcNow.AddMinutes(5))
                throw new AgentInventoryException("stale_snapshot");
            if (!string.IsNullOrWhiteSpace(snapshot.ContentHash)
                && !string.Equals(snapshot.ContentHash, ServerComputedHashMarker,
                    StringComparison.Ordinal)
                && !FixedHashEquals(snapshot.ContentHash, receivedHash))
                throw new AgentInventoryException("invalid_hash");

            var pipeline = new DesktopInventorySyncService(_dbFactory);
            pipeline.Stage(ToDesktopJson(snapshot), $"tracker-agent:{deviceId:N}");
            var preview = await pipeline.PreviewAsync(userId, ct);
            var batch = new InventorySyncBatch
            {
                Id = snapshot.BatchId, DeviceId = deviceId, UserId = userId,
                Sequence = snapshot.Sequence, Source = "tracker-agent",
                ContentHash = receivedHash,
                IsAuthoritative = preview.IsAuthoritative,
                CapturedUtc = snapshot.CapturedUtc.ToUniversalTime(),
                ReceivedUtc = DateTime.UtcNow, Status = "previewed"
            };
            db.InventorySyncBatches.Add(batch);
            await db.SaveChangesAsync(ct);
            _cache.Set(CacheKey(deviceId, snapshot.BatchId), new StagedAgentBatch(pipeline, preview),
                StageLifetime);
            return new(batch.Id, batch.Status, snapshot.Items.Count,
                snapshot.Items.Sum(x => x.Quantity), preview.UnknownItemCount,
                preview.Changes.Count, preview.IsAuthoritative);
        }
        finally { gate.Release(); }
    }

    public async Task<AgentInventoryApplyResponse> ApplyAsync(Guid deviceId, string userId,
        Guid batchId, CancellationToken ct)
    {
        var gate = _locks.GetOrAdd(deviceId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var batch = await db.InventorySyncBatches.FirstOrDefaultAsync(x => x.Id == batchId
                && x.DeviceId == deviceId && x.UserId == userId, ct)
                ?? throw new AgentInventoryException("batch_not_found");
            if (batch.Status == "applied")
                return new(batch.Id, "already_applied", batch.ChangedRecords, batch.AppliedUtc);
            var currentSequence = await db.AgentDevices.AsNoTracking()
                .Where(x => x.Id == deviceId && x.RevokedUtc == null)
                .Select(x => (long?)x.LastSequence).FirstOrDefaultAsync(ct)
                ?? throw new AgentInventoryException("device_revoked");
            if (batch.Sequence <= currentSequence)
                throw new AgentInventoryException("stale_sequence");
            if (!_cache.TryGetValue<StagedAgentBatch>(CacheKey(deviceId, batchId), out var staged)
                || staged is null)
                throw new AgentInventoryException("preview_expired");

            var result = await staged.Pipeline.ApplyAsync(userId, staged.Preview, ct);
            batch.Status = "applied";
            batch.AppliedUtc = result.AppliedUtc;
            batch.ChangedRecords = result.ChangedRecords;
            var device = await db.AgentDevices.FirstAsync(x => x.Id == deviceId, ct);
            device.LastSequence = Math.Max(device.LastSequence, batch.Sequence);
            device.LastSeenUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            _cache.Remove(CacheKey(deviceId, batchId));
            return new(batch.Id, batch.Status, batch.ChangedRecords, batch.AppliedUtc);
        }
        finally { gate.Release(); }
    }

    private static void Validate(AgentInventorySnapshot snapshot)
    {
        if (snapshot.BatchId == Guid.Empty || snapshot.Sequence < 1)
            throw new AgentInventoryException("invalid_batch");
        if (snapshot.Items.Count is 0 or > 50_000)
            throw new AgentInventoryException("invalid_item_count");
        if (snapshot.Items.Any(x => string.IsNullOrWhiteSpace(x.Section)
            || string.IsNullOrWhiteSpace(x.UniqueName) || x.Quantity is < 0 or > 1_000_000))
            throw new AgentInventoryException("invalid_item");
    }
    private static string ComputeHash(AgentInventorySnapshot snapshot)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            snapshot.Sequence, CapturedUtc = snapshot.CapturedUtc.ToUniversalTime(),
            snapshot.IsAuthoritative,
            Items = snapshot.Items.OrderBy(x => x.Section, StringComparer.Ordinal)
                .ThenBy(x => x.UniqueName, StringComparer.Ordinal).ToArray(), snapshot.Account
        });
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
    private static bool FixedHashEquals(string supplied, string computed)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(DecodeBase64Url(supplied),
                DecodeBase64Url(computed));
        }
        catch (FormatException) { return false; }
    }
    private static byte[] DecodeBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized += (normalized.Length % 4) switch
        {
            0 => "",
            2 => "==",
            3 => "=",
            _ => throw new FormatException("Invalid Base64URL length.")
        };
        return Convert.FromBase64String(normalized);
    }
    private static string ToDesktopJson(AgentInventorySnapshot snapshot)
    {
        var root = new Dictionary<string, object?>();
        foreach (var section in snapshot.Items.GroupBy(x => x.Section))
            root[section.Key] = section.Select(x => new
                { ItemType = x.UniqueName, ItemCount = x.Quantity, XP = x.Experience }).ToArray();
        if (snapshot.Account is { } account)
        {
            root["RegularCredits"] = account.Credits; root["FusionPoints"] = account.Endo;
            root["PremiumCredits"] = account.Platinum; root["PrimeBucks"] = account.Ducats;
            root["Aya"] = account.Aya; root["PlayerLevel"] = account.MasteryRank;
        }
        return JsonSerializer.Serialize(root);
    }
    private static string CacheKey(Guid device, Guid batch) => $"agent-batch:{device:N}:{batch:N}";
    private sealed record StagedAgentBatch(DesktopInventorySyncService Pipeline,
        DesktopInventoryPreview Preview);
}

public sealed class AgentInventoryException(string code) : Exception(code)
{
    public string Code => Message;
}
