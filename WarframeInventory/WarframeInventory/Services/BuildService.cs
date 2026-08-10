using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WarframeInventory.Data;
using WarframeInventory.Models;

namespace WarframeInventory.Services;

public sealed class BuildService
{
    private const int SearchLimit = 60;
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly IMemoryCache _cache;

    public BuildService(IDbContextFactory<ApplicationDbContext> dbFactory, IMemoryCache cache)
    {
        _dbFactory = dbFactory;
        _cache = cache;
    }

    public async Task<BuildWorkspace> LoadAsync(string userId, CancellationToken ct = default)
    {
        var targetsTask = SearchTargetsAsync("", "all", ct);
        var modsTask = SearchModsAsync(userId, "", "all", "all", "all", "all", ct);
        var filtersTask = LoadFiltersAsync(ct);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var builds = await db.SavedBuilds.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.IsArchived).ThenByDescending(x => x.UpdatedUtc)
            .ToListAsync(ct);
        var modKeys = builds.SelectMany(x => DeserializeSlots(x.ModsJson))
            .Select(x => x.ModUnique).Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var ownedKeys = modKeys.Count == 0 ? [] : await db.UserMods.AsNoTracking()
            .Where(x => x.UserId == userId && modKeys.Contains(x.ModUnique)
                        && (x.Owned || x.Quantity > 0))
            .Select(x => x.ModUnique).ToListAsync(ct);
        var owned = ownedKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var progress = builds.ToDictionary(x => x.Id, x =>
        {
            var keys = DeserializeSlots(x.ModsJson).Select(s => s.ModUnique)
                .Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return new BuildProgress(keys.Count(k => owned.Contains(k)), keys.Count);
        });

        await Task.WhenAll(targetsTask, modsTask, filtersTask);
        return new BuildWorkspace(targetsTask.Result, modsTask.Result, builds,
            filtersTask.Result, progress);
    }

    public async Task<IReadOnlyList<BuildTarget>> SearchTargetsAsync(
        string? search, string category, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var term = search?.Trim() ?? "";
        var result = new List<BuildTarget>();
        if (category is "all" or "warframe")
        {
            var query = db.Warframes.AsNoTracking();
            if (term.Length > 0) query = query.Where(x => EF.Functions.Like(x.Name, $"%{term}%"));
            result.AddRange(await query.OrderBy(x => x.Name).Take(SearchLimit)
                .Select(x => new BuildTarget("warframe", x.UniqueName, x.Name, "Warframe"))
                .ToListAsync(ct));
        }

        if (category != "warframe")
        {
            var query = db.Weapons.AsNoTracking();
            if (term.Length > 0) query = query.Where(x => EF.Functions.Like(x.Name, $"%{term}%"));
            query = category switch
            {
                "primary" => query.Where(x => x.Type != null &&
                    (x.Type.Contains("Rifle") || x.Type.Contains("Shotgun") || x.Type.Contains("Primary"))),
                "secondary" => query.Where(x => x.Type != null &&
                    (x.Type.Contains("Pistol") || x.Type.Contains("Secondary"))),
                "melee" => query.Where(x => x.Type != null && x.Type.Contains("Melee")),
                "archwing" => query.Where(x => (x.Type != null && x.Type.Contains("Arch")) ||
                    x.Category.Contains("Arch")),
                _ => query
            };
            var weapons = await query.OrderBy(x => x.Name).Take(SearchLimit)
                .Select(x => new { x.UniqueName, x.Name, x.Type, x.Category }).ToListAsync(ct);
            result.AddRange(weapons.Select(x => new BuildTarget(
                ClassifyWeapon(x.Type, x.Category), x.UniqueName, x.Name,
                string.IsNullOrWhiteSpace(x.Type) ? x.Category : x.Type!)));
        }
        return result.OrderBy(x => x.Name).Take(SearchLimit).ToList();
    }

    public async Task<IReadOnlyList<BuildMod>> SearchModsAsync(
        string userId, string? search, string compatibility, string polarity,
        string rarity, string ownership, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var ownedQuery = db.UserMods.AsNoTracking()
            .Where(x => x.UserId == userId && (x.Owned || x.Quantity > 0))
            .Select(x => x.ModUnique);
        var query = db.Mods.AsNoTracking().AsQueryable();
        var term = search?.Trim() ?? "";
        if (term.Length > 0) query = query.Where(x => EF.Functions.Like(x.Name, $"%{term}%"));
        if (compatibility != "all") query = query.Where(x =>
            (x.CompatName != null && x.CompatName == compatibility) || x.Category == compatibility);
        if (polarity != "all") query = query.Where(x => x.Polarity == polarity);
        if (rarity != "all") query = query.Where(x => x.Rarity == rarity);
        if (ownership == "owned") query = query.Where(x => ownedQuery.Contains(x.UniqueName));
        else if (ownership == "missing") query = query.Where(x => !ownedQuery.Contains(x.UniqueName));
        var rows = await query.OrderBy(x => x.Name).Take(SearchLimit)
            .Select(x => new { x.UniqueName, x.Name, x.BaseDrain, x.FusionLimit,
                x.Polarity, x.CompatName, x.Category, x.Rarity }).ToListAsync(ct);
        var pageKeys = rows.Select(x => x.UniqueName).ToList();
        var owned = (pageKeys.Count == 0 ? [] : await ownedQuery.Where(x => pageKeys.Contains(x))
            .ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return rows.Select(x => new BuildMod(x.UniqueName, x.Name, x.BaseDrain ?? 0,
            x.FusionLimit ?? 0, x.Polarity ?? "", x.CompatName ?? x.Category,
            x.Rarity ?? "", owned.Contains(x.UniqueName))).ToList();
    }

    public async Task SaveAsync(
        string userId, int id, string name, BuildTarget target, string tags,
        int capacity, int forma, IReadOnlyList<BuildSlot> slots, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var build = id == 0 ? null : await db.SavedBuilds
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (build is null)
        {
            build = new SavedBuild { UserId = userId };
            db.SavedBuilds.Add(build);
        }
        var trimmedName = name.Trim();
        var trimmedTags = tags.Trim();
        build.Name = trimmedName[..Math.Min(trimmedName.Length, 120)];
        build.TargetType = target.Type;
        build.TargetUnique = target.UniqueName;
        build.TargetName = target.Name;
        build.Tags = trimmedTags[..Math.Min(trimmedTags.Length, 255)];
        build.Capacity = Math.Clamp(capacity, 0, 90);
        build.FormaCount = Math.Clamp(forma, 0, 100);
        build.ModsJson = JsonSerializer.Serialize(slots);
        build.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetStatusAsync(string userId, int id, bool completed, bool archived,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var build = await db.SavedBuilds.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (build is null) return;
        build.IsCompleted = completed;
        build.IsArchived = archived;
        build.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetModOwnedAsync(string userId, string modUnique, bool owned,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var row = await db.UserMods.FirstOrDefaultAsync(x => x.UserId == userId && x.ModUnique == modUnique, ct);
        if (row is null)
        {
            row = new UserMod { UserId = userId, ModUnique = modUnique };
            db.UserMods.Add(row);
        }
        row.Owned = owned;
        row.Quantity = owned ? Math.Max(1, row.Quantity) : 0;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string userId, int id, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var build = await db.SavedBuilds.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (build is null) return;
        db.SavedBuilds.Remove(build);
        await db.SaveChangesAsync(ct);
    }

    private async Task<BuildFilters> LoadFiltersAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue<BuildFilters>("build-filter-facets-v1", out var cached) && cached is not null)
            return cached;
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var compatibility = await db.Mods.AsNoTracking().Select(x => x.CompatName ?? x.Category)
            .Where(x => x != "").Distinct().OrderBy(x => x).Take(80).ToListAsync(ct);
        var polarities = await db.Mods.AsNoTracking().Where(x => x.Polarity != null && x.Polarity != "")
            .Select(x => x.Polarity!).Distinct().OrderBy(x => x).ToListAsync(ct);
        var rarities = await db.Mods.AsNoTracking().Where(x => x.Rarity != null && x.Rarity != "")
            .Select(x => x.Rarity!).Distinct().OrderBy(x => x).ToListAsync(ct);
        var result = new BuildFilters(compatibility, polarities, rarities);
        _cache.Set("build-filter-facets-v1", result, TimeSpan.FromHours(1));
        return result;
    }

    private static string ClassifyWeapon(string? type, string? category)
    {
        var value = $"{type} {category}";
        if (value.Contains("Arch", StringComparison.OrdinalIgnoreCase)) return "archwing";
        if (value.Contains("Melee", StringComparison.OrdinalIgnoreCase)) return "melee";
        if (value.Contains("Pistol", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Secondary", StringComparison.OrdinalIgnoreCase)) return "secondary";
        return "primary";
    }

    internal static List<BuildSlot> DeserializeSlots(string json)
    {
        try { return JsonSerializer.Deserialize<List<BuildSlot>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}

public sealed record BuildWorkspace(
    IReadOnlyList<BuildTarget> Targets,
    IReadOnlyList<BuildMod> Mods,
    IReadOnlyList<SavedBuild> Builds,
    BuildFilters Filters,
    IReadOnlyDictionary<int, BuildProgress> Progress);
public sealed record BuildFilters(IReadOnlyList<string> Compatibilities,
    IReadOnlyList<string> Polarities, IReadOnlyList<string> Rarities);
public sealed record BuildProgress(int Owned, int Total)
{
    public int Percent => Total == 0 ? 0 : (int)Math.Round(Owned * 100d / Total);
}
public sealed record BuildTarget(string Type, string UniqueName, string Name, string Category);
public sealed record BuildMod(string UniqueName, string Name, int Drain, int MaxRank,
    string Polarity, string Compatibility, string Rarity, bool Owned);
public sealed record BuildSlot(string ModUnique, string Name, int Drain, string Polarity, bool Owned)
{
    public int Rank { get; init; }
    public int MaxRank { get; init; }
    public string SlotPolarity { get; init; } = "";
    public int RankedDrain => Drain + Math.Clamp(Rank, 0, MaxRank);
    public int EffectiveDrain => string.IsNullOrWhiteSpace(SlotPolarity) ? RankedDrain
        : string.Equals(SlotPolarity, Polarity, StringComparison.OrdinalIgnoreCase)
            ? (int)Math.Ceiling(RankedDrain / 2d)
            : (int)Math.Ceiling(RankedDrain * 1.25d);
}
