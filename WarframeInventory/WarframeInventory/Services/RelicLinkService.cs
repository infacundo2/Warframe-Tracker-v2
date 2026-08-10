using Microsoft.EntityFrameworkCore;
using WarframeInventory.Data;
using WarframeInventory.Models;

namespace WarframeInventory.Services;

public sealed class RelicLinkService
{
    private static readonly string[] RefinementLabels =
    [
        "Intact", "Exceptional", "Flawless", "Radiant",
        "Intacta", "Excepcional", "Perfecta", "Radiante"
    ];

    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly CatalogCacheService _catalogCache;

    public RelicLinkService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        CatalogCacheService catalogCache)
    {
        _dbFactory = dbFactory;
        _catalogCache = catalogCache;
    }

    public async Task PopulateAsync(
        IReadOnlyCollection<WarframeComponent> components,
        string userId,
        bool includeRelics,
        CancellationToken ct = default)
    {
        if (!includeRelics || components.Count == 0)
            return;

        var familiesByComponent = components.ToDictionary(
            component => component,
            component => component.Drops
                .Where(drop => !string.IsNullOrWhiteSpace(drop.Location)
                               && (drop.Location.Contains("Relic", StringComparison.OrdinalIgnoreCase)
                                   || drop.Location.Contains("Reliquia", StringComparison.OrdinalIgnoreCase)))
                .Select(drop => NormalizeFamilyName(drop.Location))
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());

        var neededFamilies = familiesByComponent.Values
            .SelectMany(names => names)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (neededFamilies.Count == 0)
            return;

        var catalog = await _catalogCache.GetRelicsAsync(ct);

        var variantsByFamily = catalog
            .Select(relic => new { Family = NormalizeFamilyName(relic.Name), Relic = relic })
            .Where(item => neededFamilies.Contains(item.Family))
            .GroupBy(item => item.Family, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Relic).ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var relevantUniqueNames = variantsByFamily.Values
            .SelectMany(variants => variants)
            .Select(relic => relic.UniqueName)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var ownedByUnique = string.IsNullOrWhiteSpace(userId)
            ? new Dictionary<string, int>(StringComparer.Ordinal)
            : await db.UserRelics.AsNoTracking()
                .Where(entry => entry.UserId == userId
                                && relevantUniqueNames.Contains(entry.RelicUnique))
                .ToDictionaryAsync(entry => entry.RelicUnique, entry => entry.Quantity, ct);

        foreach (var (component, families) in familiesByComponent)
        {
            component.RelicLinks = families
                .Where(variantsByFamily.ContainsKey)
                .Select(family => CreateLink(family, variantsByFamily[family], ownedByUnique))
                .OrderBy(link => link.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    private static RelicLink CreateLink(
        string family,
        IReadOnlyCollection<RelicCatalogEntry> variants,
        IReadOnlyDictionary<string, int> ownedByUnique)
    {
        int QuantityFor(string refinement) => variants
            .Where(variant => GetRefinementName(variant.UniqueName) == refinement)
            .Sum(variant => ownedByUnique.GetValueOrDefault(variant.UniqueName));

        var intact = variants.FirstOrDefault(
                         variant => GetRefinementName(variant.UniqueName) == "Intacta")
                     ?? variants.First();

        return new RelicLink
        {
            Name = family,
            UniqueName = intact.UniqueName,
            Vaulted = variants.All(variant => variant.Vaulted),
            IntactQuantity = QuantityFor("Intacta"),
            ExceptionalQuantity = QuantityFor("Excepcional"),
            FlawlessQuantity = QuantityFor("Perfecta"),
            RadiantQuantity = QuantityFor("Radiante")
        };
    }

    private static string NormalizeFamilyName(string value)
    {
        var normalized = value.Trim();

        foreach (var label in RefinementLabels)
        {
            normalized = normalized.Replace(
                $" ({label})", "", StringComparison.OrdinalIgnoreCase);

            if (normalized.EndsWith($" {label}", StringComparison.OrdinalIgnoreCase))
                normalized = normalized[..^(label.Length + 1)];
        }

        if (normalized.EndsWith(" Relic", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[..^6];
        if (normalized.StartsWith("Reliquia ", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[9..];

        return string.Join(' ', normalized.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string GetRefinementName(string uniqueName)
        => uniqueName.EndsWith("Silver", StringComparison.OrdinalIgnoreCase) ? "Excepcional" :
           uniqueName.EndsWith("Gold", StringComparison.OrdinalIgnoreCase) ? "Perfecta" :
           uniqueName.EndsWith("Platinum", StringComparison.OrdinalIgnoreCase) ? "Radiante" :
           "Intacta";
}
