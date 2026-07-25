using Microsoft.EntityFrameworkCore;
using WarframeInventory.Data;

namespace WarframeInventory.Services;

public sealed class UniversalSearchService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public UniversalSearchService(IDbContextFactory<ApplicationDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(
        string query, string category = "all", string? userId = null,
        CancellationToken ct = default)
    {
        query = query.Trim();
        if (query.Length < 2)
            return [];

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var intent = query.ToLowerInvariant();
        if (intent.Contains("reliquia") && intent.Contains("vaulted"))
        {
            var relicQuery = db.Relics.AsNoTracking().Where(x => x.Vaulted);
            if (intent.Contains("poseo") || intent.Contains("tengo"))
            {
                var owned = db.UserRelics.Where(x => x.UserId == userId && x.Quantity > 0)
                    .Select(x => x.RelicUnique);
                relicQuery = relicQuery.Where(x => owned.Contains(x.UniqueName));
            }
            return await relicQuery.OrderBy(x => x.Name).Take(60)
                .Select(x => new SearchHit("relic", x.UniqueName, x.Name, "Vaulted",
                    x.ImageName, $"/relics/{Uri.EscapeDataString(x.UniqueName)}"))
                .ToListAsync(ct);
        }
        if (intent.Contains("arma") && intent.Contains("prime")
            && (intent.Contains("incompleta") || intent.Contains("faltante")))
        {
            var owned = db.UserWeapons.Where(x => x.UserId == userId && x.Owned)
                .Select(x => x.WeaponUnique);
            return await db.Weapons.AsNoTracking().Where(x => x.IsPrime && !owned.Contains(x.UniqueName))
                .OrderBy(x => x.Name).Take(60)
                .Select(x => new SearchHit("weapon", x.UniqueName, x.Name,
                    "Prime incompleta", x.ImageName, $"/weapons/{Uri.EscapeDataString(x.UniqueName)}"))
                .ToListAsync(ct);
        }
        if (intent.StartsWith("reliquias con ") || intent.StartsWith("reliquia con "))
        {
            var separator = intent.IndexOf(" con ", StringComparison.Ordinal);
            var rewardSearch = query[(separator + 5)..].Trim();
            var rewardKeys = await db.RelicRewards.AsNoTracking()
                .Where(x => EF.Functions.Like(x.ItemName, $"%{rewardSearch}%"))
                .Select(x => x.RelicUnique).Distinct().Take(100).ToListAsync(ct);
            return await db.Relics.AsNoTracking().Where(x => rewardKeys.Contains(x.UniqueName))
                .OrderBy(x => x.Name).Take(60)
                .Select(x => new SearchHit("relic", x.UniqueName, x.Name,
                    x.Vaulted ? "Vaulted" : "Disponible", x.ImageName,
                    $"/relics/{Uri.EscapeDataString(x.UniqueName)}"))
                .ToListAsync(ct);
        }
        if (intent.StartsWith("mods para "))
        {
            var compat = query["mods para ".Length..].Trim();
            return await db.Mods.AsNoTracking()
                .Where(x => x.CompatName != null && EF.Functions.Like(x.CompatName, $"%{compat}%"))
                .OrderBy(x => x.Name).Take(60)
                .Select(x => new SearchHit("mod", x.UniqueName, x.Name,
                    x.CompatName ?? "Mod", x.ImageName, $"/mods/{Uri.EscapeDataString(x.UniqueName)}"))
                .ToListAsync(ct);
        }
        var pattern = $"%{query}%";
        var hits = new List<SearchHit>();

        if (category is "all" or "warframe")
            hits.AddRange(await db.Warframes.AsNoTracking()
                .Where(x => EF.Functions.Like(x.Name, pattern))
                .OrderBy(x => x.Name).Take(20)
                .Select(x => new SearchHit("warframe", x.UniqueName, x.Name,
                    x.Description ?? "", x.ImageName, $"/warframes/{Uri.EscapeDataString(x.UniqueName)}"))
                .ToListAsync(ct));

        if (category is "all" or "weapon")
            hits.AddRange(await db.Weapons.AsNoTracking()
                .Where(x => EF.Functions.Like(x.Name, pattern))
                .OrderBy(x => x.Name).Take(20)
                .Select(x => new SearchHit("weapon", x.UniqueName, x.Name,
                    x.Type ?? "Arma", x.ImageName, $"/weapons/{Uri.EscapeDataString(x.UniqueName)}"))
                .ToListAsync(ct));

        if (category is "all" or "mod")
            hits.AddRange(await db.Mods.AsNoTracking()
                .Where(x => EF.Functions.Like(x.Name, pattern)
                            || (x.CompatName != null && EF.Functions.Like(x.CompatName, pattern)))
                .OrderBy(x => x.Name).Take(20)
                .Select(x => new SearchHit("mod", x.UniqueName, x.Name,
                    x.CompatName ?? x.Rarity ?? "Mod", x.ImageName,
                    $"/mods/{Uri.EscapeDataString(x.UniqueName)}"))
                .ToListAsync(ct));

        if (category is "all" or "relic")
            hits.AddRange(await db.Relics.AsNoTracking()
                .Where(x => EF.Functions.Like(x.Name, pattern))
                .GroupBy(x => x.Name)
                .Select(x => x.First())
                .OrderBy(x => x.Name).Take(20)
                .Select(x => new SearchHit("relic", x.UniqueName, x.Name,
                    x.Vaulted ? "Vaulted" : "Disponible", x.ImageName,
                    $"/relics/{Uri.EscapeDataString(x.UniqueName)}"))
                .ToListAsync(ct));

        if (category is "all" or "component")
            hits.AddRange(await db.RelicRewards.AsNoTracking()
                .Where(x => EF.Functions.Like(x.ItemName, pattern))
                .GroupBy(x => new { x.ItemUnique, x.ItemName })
                .OrderBy(x => x.Key.ItemName).Take(20)
                .Select(x => new SearchHit("component", x.Key.ItemUnique, x.Key.ItemName,
                    $"{x.Select(y => y.RelicUnique).Distinct().Count()} variantes de reliquia",
                    null, "/relics"))
                .ToListAsync(ct));

        return hits.OrderBy(x => x.Name).Take(60).ToList();
    }
}

public sealed record SearchHit(
    string Category,
    string UniqueName,
    string Name,
    string Subtitle,
    string? ImageName,
    string Route);
