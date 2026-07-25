using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WarframeInventory.Data;
using WarframeInventory.Models;

namespace WarframeInventory.Services;

public sealed class FarmPlannerService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public FarmPlannerService(IDbContextFactory<ApplicationDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<FarmPlan?> BuildAsync(
        string userId, int goalId, string strategy, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var goal = await db.UserGoals.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == goalId && x.UserId == userId, ct);
        if (goal is null || goal.TargetType is not ("warframe" or "weapon"))
            return null;

        var componentsJson = goal.TargetType == "warframe"
            ? await db.Warframes.AsNoTracking()
                .Where(x => x.UniqueName == goal.TargetUnique)
                .Select(x => x.ComponentsJson)
                .FirstOrDefaultAsync(ct)
            : await db.Weapons.AsNoTracking()
                .Where(x => x.UniqueName == goal.TargetUnique)
                .Select(x => x.ComponentsJson)
                .FirstOrDefaultAsync(ct);
        var components = DeserializeComponents(componentsJson);
        var inventory = await db.UserComponents.AsNoTracking()
            .Where(x => x.UserId == userId && x.ParentUnique == goal.TargetUnique)
            .ToDictionaryAsync(x => x.ComponentName, StringComparer.OrdinalIgnoreCase, ct);
        var missing = components
            .Where(x => !inventory.TryGetValue(x.Name, out var stored)
                        || (!stored.Owned && stored.Quantity <= 0))
            .ToList();

        var relics = await db.Relics.AsNoTracking().ToListAsync(ct);
        var userRelics = await db.UserRelics.AsNoTracking()
            .Where(x => x.UserId == userId && x.Quantity > 0)
            .ToDictionaryAsync(x => x.RelicUnique, x => x.Quantity, ct);
        var activeGoalTargets = await db.UserGoals.AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsCompleted)
            .Select(x => new { x.TargetUnique, x.DisplayName }).ToListAsync(ct);
        var goalNamesByRelic = new Dictionary<string, HashSet<string>>();
        foreach (var target in activeGoalTargets)
        {
            var linkedKeys = await db.RelicRewards.AsNoTracking()
                .Where(x => x.ItemUnique.StartsWith(target.TargetUnique))
                .Select(x => x.RelicUnique).Distinct().ToListAsync(ct);
            foreach (var key in linkedKeys)
            {
                if (!goalNamesByRelic.TryGetValue(key, out var names))
                    goalNamesByRelic[key] = names = [];
                names.Add(target.DisplayName);
            }
        }
        var routes = new List<FarmRoute>();

        foreach (var component in missing)
        {
            var groupedDrops = component.Drops
                .Select(drop => new
                {
                    Drop = drop,
                    RelicName = CleanRelicName(drop.Location),
                    Refinement = RefinementFromLocation(drop.Location)
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.RelicName))
                .GroupBy(x => x.RelicName, StringComparer.OrdinalIgnoreCase);

            foreach (var group in groupedDrops)
            {
                var variants = relics.Where(x =>
                        string.Equals(x.Name, $"Reliquia {group.Key}", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(x.Name, $"{group.Key} Relic", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(x.Name, group.Key, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (variants.Count == 0)
                    continue;

                var chances = group
                    .Where(x => !string.IsNullOrWhiteSpace(x.Refinement))
                    .GroupBy(x => x.Refinement)
                    .ToDictionary(
                        x => x.Key,
                        x => NormalizeChance(x.First().Drop.Chance),
                        StringComparer.OrdinalIgnoreCase);
                FillCanonicalChances(chances);

                var owned = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Intacta"] = QuantityFor(variants, userRelics, "Intacta"),
                    ["Excepcional"] = QuantityFor(variants, userRelics, "Excepcional"),
                    ["Perfecta"] = QuantityFor(variants, userRelics, "Perfecta"),
                    ["Radiante"] = QuantityFor(variants, userRelics, "Radiante")
                };
                var recommended = RecommendRefinement(strategy, chances, owned);
                var probability = chances.GetValueOrDefault(recommended);
                var locations = variants.SelectMany(x => ParseRelicLocations(x.DropsJson))
                    .GroupBy(x => $"{x.Location}|{x.Rotation}", StringComparer.OrdinalIgnoreCase)
                    .Select(x => x.OrderByDescending(y => y.Chance).First())
                    .OrderByDescending(x => x.Chance).Take(6).ToList();
                var usefulGoals = variants.SelectMany(x =>
                        goalNamesByRelic.GetValueOrDefault(x.UniqueName) ?? [])
                    .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();

                routes.Add(new FarmRoute(
                    component.Name,
                    variants[0].Name,
                    variants.FirstOrDefault(x => RefinementFromUnique(x.UniqueName) == "Intacta")?.UniqueName
                        ?? variants[0].UniqueName,
                    variants.All(x => x.Vaulted),
                    owned,
                    chances,
                    recommended,
                    TraceCost(recommended),
                    AttemptsFor(probability, .50),
                    AttemptsFor(probability, .75),
                    AttemptsFor(probability, .90),
                    locations,
                    usefulGoals));
            }
        }

        var ordered = strategy switch
        {
            "owned" => routes.OrderByDescending(x => x.TotalOwned).ThenBy(x => x.ComponentName),
            "available" => routes.OrderBy(x => x.Vaulted).ThenByDescending(x => x.TotalOwned),
            "chance" => routes.OrderByDescending(x => x.RecommendedChance),
            "traces" => routes.OrderBy(x => x.TraceCost).ThenByDescending(x => x.TotalOwned),
            "shortest" => routes.OrderBy(x => x.Vaulted)
                .ThenBy(x => x.Attempts75).ThenByDescending(x => x.Locations.Count),
            "multi" => routes.OrderByDescending(x => x.UsefulGoals.Count)
                .ThenByDescending(x => x.TotalOwned).ThenBy(x => x.Vaulted),
            _ => routes.OrderByDescending(x => x.TotalOwned > 0).ThenBy(x => x.Vaulted)
        };

        return new FarmPlan(goal.Id, goal.DisplayName, strategy, missing.Select(x => x.Name).ToList(),
            ordered.ToList());
    }

    private static List<WarframeComponent> DeserializeComponents(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<WarframeComponent>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string RecommendRefinement(
        string strategy,
        IReadOnlyDictionary<string, double> chances,
        IReadOnlyDictionary<string, int> owned)
    {
        if (strategy == "traces")
            return "Intacta";
        if (strategy == "chance")
            return "Radiante";
        if (strategy == "owned")
        {
            var bestOwned = new[] { "Radiante", "Perfecta", "Excepcional", "Intacta" }
                .FirstOrDefault(x => owned.GetValueOrDefault(x) > 0);
            if (bestOwned is not null)
                return bestOwned;
        }

        var intact = chances.TryGetValue("Intacta", out var intactChance) ? intactChance : 0;
        return intact <= 2.01 ? "Radiante"
            : intact <= 11.01 ? "Perfecta"
            : "Intacta";
    }

    private static void FillCanonicalChances(IDictionary<string, double> chances)
    {
        var intact = chances.TryGetValue("Intacta", out var intactChance) ? intactChance : 0;
        var rarity = intact <= 2.01 ? "rare" : intact <= 11.01 ? "uncommon" : "common";
        var canonical = rarity switch
        {
            "rare" => new[] { 2d, 4d, 6d, 10d },
            "uncommon" => new[] { 11d, 13d, 17d, 20d },
            _ => new[] { 25.33d, 23.33d, 20d, 16.67d }
        };
        var names = new[] { "Intacta", "Excepcional", "Perfecta", "Radiante" };
        for (var i = 0; i < names.Length; i++)
            chances.TryAdd(names[i], canonical[i]);
    }

    private static int QuantityFor(
        IEnumerable<Relic> variants,
        IReadOnlyDictionary<string, int> quantities,
        string refinement)
        => variants.Where(x => RefinementFromUnique(x.UniqueName) == refinement)
            .Sum(x => quantities.GetValueOrDefault(x.UniqueName));

    private static string RefinementFromUnique(string uniqueName)
        => uniqueName.EndsWith("Silver", StringComparison.OrdinalIgnoreCase) ? "Excepcional"
            : uniqueName.EndsWith("Gold", StringComparison.OrdinalIgnoreCase) ? "Perfecta"
            : uniqueName.EndsWith("Platinum", StringComparison.OrdinalIgnoreCase) ? "Radiante"
            : "Intacta";

    private static string RefinementFromLocation(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return "";
        if (location.Contains("Radiant", StringComparison.OrdinalIgnoreCase)
            || location.Contains("Radiante", StringComparison.OrdinalIgnoreCase))
            return "Radiante";
        if (location.Contains("Flawless", StringComparison.OrdinalIgnoreCase)
            || location.Contains("Perfecta", StringComparison.OrdinalIgnoreCase))
            return "Perfecta";
        if (location.Contains("Exceptional", StringComparison.OrdinalIgnoreCase)
            || location.Contains("Excepcional", StringComparison.OrdinalIgnoreCase))
            return "Excepcional";
        return "Intacta";
    }

    private static string CleanRelicName(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)
            || !location.Contains("Relic", StringComparison.OrdinalIgnoreCase))
            return "";
        var parenthesis = location.IndexOf(" (", StringComparison.Ordinal);
        var withoutRefinement = parenthesis >= 0 ? location[..parenthesis] : location;
        return withoutRefinement
            .Replace("Reliquia", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Relic", "", StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static double NormalizeChance(double chance) => chance is > 0 and <= 1 ? chance * 100 : chance;
    private static int TraceCost(string refinement) => refinement switch
    {
        "Excepcional" => 25,
        "Perfecta" => 50,
        "Radiante" => 100,
        _ => 0
    };

    private static int AttemptsFor(double percentage, double confidence)
    {
        var probability = Math.Clamp(percentage / 100d, .000001, .999999);
        return (int)Math.Ceiling(Math.Log(1 - confidence) / Math.Log(1 - probability));
    }

    private static IReadOnlyList<RelicLocation> ParseRelicLocations(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return [];
            var result = new List<RelicLocation>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                var location = ReadString(item, "location");
                if (string.IsNullOrWhiteSpace(location))
                    continue;
                result.Add(new RelicLocation(
                    location,
                    ReadString(item, "type"),
                    ReadString(item, "rotation"),
                    item.TryGetProperty("chance", out var chance) && chance.TryGetDouble(out var value)
                        ? NormalizeChance(value) : 0));
            }
            return result;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string ReadString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) ? value.GetString() ?? "" : "";
}

public sealed record FarmPlan(
    int GoalId,
    string TargetName,
    string Strategy,
    IReadOnlyList<string> MissingComponents,
    IReadOnlyList<FarmRoute> Routes);

public sealed record FarmRoute(
    string ComponentName,
    string RelicName,
    string RelicUnique,
    bool Vaulted,
    IReadOnlyDictionary<string, int> Owned,
    IReadOnlyDictionary<string, double> Chances,
    string RecommendedRefinement,
    int TraceCost,
    int Attempts50,
    int Attempts75,
    int Attempts90,
    IReadOnlyList<RelicLocation> Locations,
    IReadOnlyList<string> UsefulGoals)
{
    public int TotalOwned => Owned.Values.Sum();
    public double RecommendedChance => Chances.GetValueOrDefault(RecommendedRefinement);
}
public sealed record RelicLocation(
    string Location, string MissionType, string Rotation, double Chance);
