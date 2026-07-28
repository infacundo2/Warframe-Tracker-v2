using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using WarframeInventory.Data;
using WarframeInventory.Models;

namespace WarframeInventory.Services;

public sealed class RelicSyncService
{
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(15);
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly AlecaFrameRelicClient _client;
    private readonly IDataProtector _tokenProtector;

    public RelicSyncService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        AlecaFrameRelicClient client,
        IDataProtectionProvider dataProtection)
    {
        _dbFactory = dbFactory;
        _client = client;
        _tokenProtector = dataProtection.CreateProtector(
            "WarframeInventory.AlecaFrame.PublicToken.v1");
    }

    public async Task<RelicSyncStatus> GetStatusAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var profile = await db.RelicSyncProfiles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        return profile is null
            ? RelicSyncStatus.Empty
            : new RelicSyncStatus(
                !string.IsNullOrWhiteSpace(profile.ProtectedToken),
                profile.LastSyncUtc,
                profile.LastSourceCount,
                profile.LastMatchedCount,
                profile.LastStatus);
    }

    public async Task<RelicSyncPreview> PreviewAsync(
        string userId,
        string? suppliedToken,
        bool rememberToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new RelicSyncException("Debes iniciar sesión para sincronizar.");

        var token = await ResolveTokenAsync(userId, suppliedToken, cancellationToken);
        AlecaRelicInventory sourceInventory;
        try
        {
            sourceInventory = await _client.GetRelicsAsync(token, cancellationToken);
        }
        catch (Exception exception)
        {
            await RecordFailureAsync(userId, exception.Message, cancellationToken);
            throw;
        }

        var sourceEntries = sourceInventory.Entries;
        if (sourceEntries.Count == 0)
            throw new RelicSyncException(
                "AlecaFrame devolvió cero reliquias. Por seguridad no se modificará el inventario.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var catalog = await db.Relics.AsNoTracking().ToListAsync(cancellationToken);
        var catalogByKey = catalog
            .Select(relic => new { Relic = relic, Key = CatalogKey(relic) })
            .Where(x => x.Key is not null)
            .GroupBy(x => x.Key!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => x.Relic).ToList(),
                StringComparer.Ordinal);

        var incoming = sourceEntries
            .GroupBy(SourceKey, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => checked(group.Sum(x => x.Quantity)),
                StringComparer.Ordinal);

        var matched = new Dictionary<string, MatchedRelic>(StringComparer.Ordinal);
        var preservedUniqueNames = new HashSet<string>(StringComparer.Ordinal);
        var unknown = new List<string>();
        foreach (var (sourceKey, quantity) in incoming)
        {
            if (!catalogByKey.TryGetValue(sourceKey, out var candidates)
                || candidates.Count != 1)
            {
                unknown.Add(DisplaySourceKey(sourceKey));
                if (candidates is not null)
                {
                    foreach (var candidate in candidates)
                        preservedUniqueNames.Add(candidate.UniqueName);
                }
                continue;
            }

            var relic = candidates[0];
            matched[relic.UniqueName] = new MatchedRelic(
                relic.UniqueName,
                relic.Name,
                RefinementFromUnique(relic.UniqueName),
                quantity);
        }

        if (matched.Count == 0)
            throw new RelicSyncException(
                "Ninguna reliquia recibida coincide con el catálogo local. Sincroniza primero el catálogo.");

        if (unknown.Count > matched.Count)
            throw new RelicSyncException(
                "Demasiadas reliquias no coinciden con el catálogo. Por seguridad no se aplicarán cambios.");

        var current = await db.UserRelics.AsNoTracking()
            .Where(x => x.UserId == userId && x.Quantity > 0)
            .ToDictionaryAsync(x => x.RelicUnique, x => x.Quantity, cancellationToken);

        var changes = new List<RelicSyncChange>();
        foreach (var entry in matched.Values)
        {
            var previous = current.GetValueOrDefault(entry.UniqueName);
            if (previous != entry.Quantity)
            {
                changes.Add(new RelicSyncChange(
                    entry.UniqueName,
                    entry.Name,
                    entry.Refinement,
                    previous,
                    entry.Quantity));
            }
        }

        if (sourceInventory.IsAuthoritative)
        {
            foreach (var (uniqueName, previous) in current)
            {
                if (matched.ContainsKey(uniqueName)
                    || preservedUniqueNames.Contains(uniqueName))
                    continue;

                var relic = catalog.FirstOrDefault(x => x.UniqueName == uniqueName);
                if (relic is null)
                    continue;

                changes.Add(new RelicSyncChange(
                    uniqueName,
                    relic.Name,
                    RefinementFromUnique(uniqueName),
                    previous,
                    0));
            }
        }

        var now = DateTime.UtcNow;
        var profile = await db.RelicSyncProfiles
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (profile is null)
        {
            profile = new RelicSyncProfile { UserId = userId };
            db.RelicSyncProfiles.Add(profile);
        }

        if (rememberToken)
            profile.ProtectedToken = _tokenProtector.Protect(token);
        else
            profile.ProtectedToken = null;

        profile.LastPreviewUtc = now;
        profile.LastSourceCount = sourceInventory.DeclaredCount;
        profile.LastMatchedCount = matched.Count;
        profile.LastStatus = "Previewed";
        profile.LastError = null;
        await db.SaveChangesAsync(cancellationToken);

        return new RelicSyncPreview(
            userId,
            now,
            sourceInventory.DeclaredCount,
            matched.Count,
            sourceInventory.SkippedRecords,
            sourceInventory.IsAuthoritative,
            unknown.OrderBy(x => x).ToList(),
            matched.Values.ToDictionary(x => x.UniqueName, StringComparer.Ordinal),
            preservedUniqueNames,
            changes
                .OrderByDescending(x => Math.Abs(x.NewQuantity - x.PreviousQuantity))
                .ThenBy(x => x.Name)
                .ThenBy(x => RefinementOrder(x.Refinement))
                .ToList());
    }

    public async Task<int> ApplyAsync(
        string userId,
        RelicSyncPreview preview,
        CancellationToken cancellationToken = default)
    {
        if (preview.UserId != userId)
            throw new RelicSyncException("La vista previa pertenece a otra sesión.");
        if (DateTime.UtcNow - preview.CreatedUtc > PreviewLifetime)
            throw new RelicSyncException(
                "La vista previa venció. Analiza nuevamente el inventario antes de aplicarlo.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var stored = await db.UserRelics
            .Where(x => x.UserId == userId)
            .ToDictionaryAsync(x => x.RelicUnique, cancellationToken);
        var changed = 0;

        foreach (var change in preview.Changes)
        {
            if (!stored.TryGetValue(change.UniqueName, out var userRelic))
            {
                if (change.NewQuantity == 0)
                    continue;
                userRelic = new UserRelic
                {
                    UserId = userId,
                    RelicUnique = change.UniqueName,
                    Quantity = change.NewQuantity
                };
                db.UserRelics.Add(userRelic);
                stored[change.UniqueName] = userRelic;
                changed++;
            }
            else if (userRelic.Quantity != change.NewQuantity)
            {
                userRelic.Quantity = change.NewQuantity;
                changed++;
            }
        }

        var profile = await db.RelicSyncProfiles
            .SingleAsync(x => x.UserId == userId, cancellationToken);
        profile.LastSyncUtc = DateTime.UtcNow;
        profile.LastStatus = "Succeeded";
        profile.LastError = null;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return changed;
    }

    public async Task DisconnectAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var profile = await db.RelicSyncProfiles
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (profile is null)
            return;
        profile.ProtectedToken = null;
        profile.LastStatus = "Disconnected";
        profile.LastError = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> ResolveTokenAsync(
        string userId,
        string? suppliedToken,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(suppliedToken))
            return suppliedToken.Trim();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var protectedToken = await db.RelicSyncProfiles.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.ProtectedToken)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(protectedToken))
            throw new RelicSyncException(
                "Pega un token público de AlecaFrame con permiso para reliquias.");

        try
        {
            return _tokenProtector.Unprotect(protectedToken);
        }
        catch
        {
            throw new RelicSyncException(
                "El token guardado ya no puede descifrarse. Conecta uno nuevo.");
        }
    }

    private async Task RecordFailureAsync(
        string userId,
        string message,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var profile = await db.RelicSyncProfiles
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (profile is null)
        {
            profile = new RelicSyncProfile { UserId = userId };
            db.RelicSyncProfiles.Add(profile);
        }

        profile.LastStatus = "Failed";
        profile.LastError = message.Length > 500 ? message[..500] : message;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string SourceKey(AlecaRelicEntry entry)
        => $"{entry.Era}:{Normalize(entry.Code)}:{entry.Refinement.ToUpperInvariant()}";

    private static string? CatalogKey(Relic relic)
    {
        var era = relic.UniqueName.Contains("T1VoidProjection", StringComparison.OrdinalIgnoreCase)
            ? "LITH"
            : relic.UniqueName.Contains("T2VoidProjection", StringComparison.OrdinalIgnoreCase)
                ? "MESO"
                : relic.UniqueName.Contains("T3VoidProjection", StringComparison.OrdinalIgnoreCase)
                    ? "NEO"
                    : relic.UniqueName.Contains("T4VoidProjection", StringComparison.OrdinalIgnoreCase)
                        ? "AXI"
                        : relic.UniqueName.Contains("T5VoidProjection", StringComparison.OrdinalIgnoreCase)
                            ? "REQUIEM"
                            : null;
        if (era is null || relic.UniqueName.EndsWith("OmniA", StringComparison.OrdinalIgnoreCase))
            return null;

        var normalizedName = Normalize(relic.Name)
            .Replace("RELIQUIA ", "", StringComparison.Ordinal)
            .Replace(" RELIC", "", StringComparison.Ordinal);
        var eraLabel = era == "REQUIEM" ? "REQUIEM" : era;
        var marker = eraLabel + " ";
        var markerIndex = normalizedName.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
            return null;

        var code = normalizedName[(markerIndex + marker.Length)..].Trim();
        if (string.IsNullOrWhiteSpace(code))
            return null;
        return $"{era}:{code}:{RefinementFromUnique(relic.UniqueName).ToUpperInvariant()}";
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToUpperInvariant(character));
        }

        return string.Join(' ', builder.ToString().Normalize(NormalizationForm.FormC)
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
    }

    private static string RefinementFromUnique(string uniqueName)
        => uniqueName.EndsWith("Silver", StringComparison.OrdinalIgnoreCase) ? "Excepcional"
            : uniqueName.EndsWith("Gold", StringComparison.OrdinalIgnoreCase) ? "Perfecta"
            : uniqueName.EndsWith("Platinum", StringComparison.OrdinalIgnoreCase) ? "Radiante"
            : "Intacta";

    private static int RefinementOrder(string value) => value switch
    {
        "Intacta" => 0,
        "Excepcional" => 1,
        "Perfecta" => 2,
        "Radiante" => 3,
        _ => 4
    };

    private static string DisplaySourceKey(string sourceKey)
    {
        var parts = sourceKey.Split(':');
        return parts.Length == 3
            ? $"{parts[0]} {parts[1]} · {ToTitle(parts[2])}"
            : sourceKey;
    }

    private static string ToTitle(string value)
        => CultureInfo.GetCultureInfo("es-CL").TextInfo.ToTitleCase(value.ToLowerInvariant());
}

public sealed record RelicSyncStatus(
    bool HasSavedToken,
    DateTime? LastSyncUtc,
    int LastSourceCount,
    int LastMatchedCount,
    string LastStatus)
{
    public static readonly RelicSyncStatus Empty = new(false, null, 0, 0, "Never");
}

public sealed record RelicSyncPreview(
    string UserId,
    DateTime CreatedUtc,
    int SourceCount,
    int MatchedCount,
    int SkippedRecords,
    bool IsAuthoritative,
    IReadOnlyList<string> UnknownRelics,
    IReadOnlyDictionary<string, MatchedRelic> Matched,
    IReadOnlySet<string> PreservedUniqueNames,
    IReadOnlyList<RelicSyncChange> Changes);

public sealed record MatchedRelic(
    string UniqueName,
    string Name,
    string Refinement,
    int Quantity);

public sealed record RelicSyncChange(
    string UniqueName,
    string Name,
    string Refinement,
    int PreviousQuantity,
    int NewQuantity);
