using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WarframeInventory.Contracts.AgentV1;
using WarframeInventory.Data;
using WarframeInventory.Models;

namespace WarframeInventory.Services;

public sealed class AgentTokenService
{
    private static readonly TimeSpan PairingLifetime = TimeSpan.FromMinutes(10);
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public AgentTokenService(IDbContextFactory<ApplicationDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<PairingStartResponse> StartPairingAsync(
        string deviceName, string publicBaseUrl, CancellationToken ct)
    {
        var code = CreateCode();
        var verifier = CreateToken(32);
        var pairing = new AgentPairing
        {
            Id = Guid.NewGuid(),
            CodeHash = Hash(code),
            VerifierHash = Hash(verifier),
            DeviceName = SanitizeDeviceName(deviceName),
            CreatedUtc = DateTime.UtcNow,
            ExpiresUtc = DateTime.UtcNow.Add(PairingLifetime)
        };
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        db.AgentPairings.Add(pairing);
        await db.SaveChangesAsync(ct);
        return new PairingStartResponse(pairing.Id, code, verifier, pairing.ExpiresUtc,
            $"{publicBaseUrl.TrimEnd('/')}/agent/connect?code={Uri.EscapeDataString(code)}");
    }

    public async Task<bool> ApproveAsync(string code, string userId, CancellationToken ct)
    {
        var codeHash = Hash(NormalizeCode(code));
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;
        var pairing = await db.AgentPairings.FirstOrDefaultAsync(x =>
            x.CodeHash == codeHash && x.ExpiresUtc > now && x.ConsumedUtc == null
            && x.ApprovedUtc == null && x.UserId == null, ct);
        if (pairing is null)
            return false;
        pairing.UserId = userId;
        pairing.ApprovedUtc = now;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PairingTokenResponse> ExchangeAsync(
        Guid pairingId, string verifier, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var pairing = await db.AgentPairings.FirstOrDefaultAsync(x => x.Id == pairingId, ct);
        if (pairing is null || !FixedEquals(pairing.VerifierHash, Hash(verifier)))
            return new("invalid", null, null, null, null);
        if (pairing.ExpiresUtc <= DateTime.UtcNow)
            return new("expired", null, null, null, pairing.ExpiresUtc);
        if (pairing.ConsumedUtc is not null)
            return new("consumed", null, null, null, pairing.ExpiresUtc);
        if (pairing.ApprovedUtc is null || string.IsNullOrWhiteSpace(pairing.UserId))
            return new("pending", null, null, null, pairing.ExpiresUtc);

        var token = CreateToken(48);
        var device = new AgentDevice
        {
            Id = Guid.NewGuid(), UserId = pairing.UserId, Name = pairing.DeviceName,
            TokenHash = Hash(token), CreatedUtc = DateTime.UtcNow
        };
        pairing.ConsumedUtc = DateTime.UtcNow;
        db.AgentDevices.Add(device);
        await db.SaveChangesAsync(ct);
        var userName = await db.Users.AsNoTracking().Where(x => x.Id == device.UserId)
            .Select(x => x.UserName).FirstOrDefaultAsync(ct);
        return new("approved", token, device.Id, userName, pairing.ExpiresUtc);
    }

    public async Task<AgentDevice?> AuthenticateAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 256)
            return null;
        var hash = Hash(token);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.AgentDevices.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TokenHash == hash && x.RevokedUtc == null, ct);
    }

    public async Task TouchAsync(Guid deviceId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.AgentDevices.Where(x => x.Id == deviceId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.LastSeenUtc, DateTime.UtcNow), ct);
    }

    internal static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    internal static bool FixedEquals(string left, string right)
        => CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(left), Convert.FromHexString(right));
    private static string CreateToken(int bytes)
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string CreateCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(8);
        return string.Create(9, bytes, (span, source) =>
        {
            for (var index = 0; index < 4; index++) span[index] = alphabet[source[index] % alphabet.Length];
            span[4] = '-';
            for (var index = 4; index < 8; index++) span[index + 1] = alphabet[source[index] % alphabet.Length];
        });
    }
    private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();
    private static string SanitizeDeviceName(string value)
    {
        var cleaned = string.Join(' ', (value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (cleaned.Length == 0) cleaned = "Windows PC";
        return cleaned[..Math.Min(cleaned.Length, 80)];
    }
}
