using Microsoft.EntityFrameworkCore;
using WarframeInventory.Data;
using WarframeInventory.Models;

namespace WarframeInventory.Services;

public sealed class InventoryAdvancedService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    public InventoryAdvancedService(IDbContextFactory<ApplicationDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<InventoryManagerData> LoadAsync(
        string userId, string search, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var query = db.Relics.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => EF.Functions.Like(x.Name, $"%{search.Trim()}%"));
        var relics = await query.OrderBy(x => x.Name).Take(120)
            .Select(x => new { x.UniqueName, x.Name, x.Vaulted }).ToListAsync(ct);
        var keys = relics.Select(x => x.UniqueName).ToList();
        var owned = await db.UserRelics.AsNoTracking()
            .Where(x => x.UserId == userId && keys.Contains(x.RelicUnique))
            .ToDictionaryAsync(x => x.RelicUnique, x => x.Quantity, ct);
        var rows = relics.Select(x => new BulkRelic(
            x.UniqueName, x.Name, x.Vaulted, owned.GetValueOrDefault(x.UniqueName))).ToList();

        var history = await db.InventoryEvents.AsNoTracking()
            .Where(x => x.UserId == userId).OrderByDescending(x => x.OccurredUtc)
            .Take(80).ToListAsync(ct);
        var stats = new InventoryStats(
            history.Count(x => x.OccurredUtc >= DateTime.UtcNow.AddDays(-7)
                               && x.NewValue > x.PreviousValue),
            history.GroupBy(x => x.Category).OrderByDescending(x => x.Count())
                .Select(x => x.Key).FirstOrDefault() ?? "sin actividad",
            history.Count(x => x.IsReverted));
        return new InventoryManagerData(rows, history, stats);
    }

    public async Task<int> ApplyRelicBulkAsync(
        string userId, IReadOnlyCollection<string> keys, int delta, CancellationToken ct = default)
    {
        if (keys.Count == 0 || delta is < -99 or > 99)
            return 0;
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var existing = await db.UserRelics.Where(x =>
            x.UserId == userId && keys.Contains(x.RelicUnique)).ToListAsync(ct);
        foreach (var key in keys)
        {
            var row = existing.FirstOrDefault(x => x.RelicUnique == key);
            if (row is null)
                db.UserRelics.Add(new UserRelic
                    { UserId = userId, RelicUnique = key, Quantity = Math.Max(0, delta) });
            else
                row.Quantity = Math.Clamp(row.Quantity + delta, 0, 9999);
        }
        await db.SaveChangesAsync(ct);
        return keys.Count;
    }

    public async Task<bool> UndoAsync(string userId, long eventId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entry = await db.InventoryEvents.FirstOrDefaultAsync(
            x => x.Id == eventId && x.UserId == userId && !x.IsReverted, ct);
        if (entry is null)
            return false;

        switch (entry.Category)
        {
            case "warframe":
                var frame = await db.UserWarframes.FirstOrDefaultAsync(x =>
                    x.UserId == userId && x.WarframeUnique == entry.TargetUnique, ct);
                if (frame is null) return false;
                frame.Owned = entry.PreviousValue > 0;
                break;
            case "weapon":
                var weapon = await db.UserWeapons.FirstOrDefaultAsync(x =>
                    x.UserId == userId && x.WeaponUnique == entry.TargetUnique, ct);
                if (weapon is null) return false;
                weapon.Owned = entry.PreviousValue > 0;
                break;
            case "mod":
                var mod = await db.UserMods.FirstOrDefaultAsync(x =>
                    x.UserId == userId && x.ModUnique == entry.TargetUnique, ct);
                if (mod is null) return false;
                mod.Quantity = entry.PreviousValue;
                mod.Owned = entry.PreviousValue > 0;
                break;
            case "relic":
                var relic = await db.UserRelics.FirstOrDefaultAsync(x =>
                    x.UserId == userId && x.RelicUnique == entry.TargetUnique, ct);
                if (relic is null) return false;
                relic.Quantity = entry.PreviousValue;
                break;
            case "component":
                var component = await db.UserComponents.FirstOrDefaultAsync(x =>
                    x.UserId == userId && x.ParentUnique == entry.TargetUnique
                    && x.ComponentName == entry.DisplayName, ct);
                if (component is null) return false;
                component.Quantity = entry.PreviousValue;
                component.Owned = entry.PreviousValue > 0;
                break;
            default:
                return false;
        }
        entry.IsReverted = true;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task SaveMetadataAsync(
        string userId, string category, string targetUnique, string notes, string tags,
        DateTime? acquired, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var row = await db.InventoryMetadata.FirstOrDefaultAsync(x =>
            x.UserId == userId && x.Category == category && x.TargetUnique == targetUnique, ct);
        if (row is null)
        {
            row = new InventoryMetadata
                { UserId = userId, Category = category, TargetUnique = targetUnique };
            db.InventoryMetadata.Add(row);
        }
        row.Notes = notes.Trim();
        row.Tags = tags.Trim()[..Math.Min(tags.Trim().Length, 255)];
        row.AcquiredUtc = acquired;
        row.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<InventoryMetadata?> GetMetadataAsync(
        string userId, string category, string targetUnique, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.InventoryMetadata.AsNoTracking().FirstOrDefaultAsync(x =>
            x.UserId == userId && x.Category == category && x.TargetUnique == targetUnique, ct);
    }
}

public sealed record InventoryManagerData(
    IReadOnlyList<BulkRelic> Relics,
    IReadOnlyList<InventoryEvent> History,
    InventoryStats Stats);
public sealed record BulkRelic(string UniqueName, string Name, bool Vaulted, int Quantity);
public sealed record InventoryStats(int AddedThisWeek, string MostActiveCategory, int RevertedChanges);
