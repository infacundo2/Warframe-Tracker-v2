using System.Globalization;
using System.Text;
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
        var componentStatuses = components.Select(component =>
        {
            inventory.TryGetValue(component.Name, out var stored);
            var required = Math.Max(1, component.ItemCount);
            var owned = stored is null ? 0 : Math.Max(stored.Quantity, stored.Owned ? required : 0);
            return new FarmComponentStatus(component.Name, required, owned,
                Math.Max(0, required - owned));
        }).ToList();
        var missing = components
            .Where(x => !inventory.TryGetValue(x.Name, out var stored)
                        || (!stored.Owned && stored.Quantity < Math.Max(1, x.ItemCount)))
            .ToList();

        var targetRewardRows = missing.Count == 0 ? [] : await db.RelicRewards.AsNoTracking()
            .Where(x => EF.Functions.Like(x.ItemName, $"%{goal.DisplayName}%"))
            .ToListAsync(ct);
        var rewardsByComponent = missing.ToDictionary(
            component => component,
            component => targetRewardRows
                .Where(reward => IsRewardForComponent(goal.DisplayName, component, reward))
                .ToList());
        var relatedRelicUniqueNames = rewardsByComponent.Values
            .SelectMany(x => x)
            .Select(x => x.RelicUnique)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var relatedRelicNames = missing.SelectMany(x => x.Drops)
            .Select(x => CleanRelicName(x.Location))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var exactRelicNames = relatedRelicNames.SelectMany(x => new[]
            { $"Reliquia {x}", $"{x} Relic", x }).Distinct().ToList();
        var relics = relatedRelicUniqueNames.Count == 0 && exactRelicNames.Count == 0
            ? []
            : await db.Relics.AsNoTracking()
                .Where(x => relatedRelicUniqueNames.Contains(x.UniqueName)
                            || exactRelicNames.Contains(x.Name))
                .ToListAsync(ct);
        var userRelics = await db.UserRelics.AsNoTracking()
            .Where(x => x.UserId == userId && x.Quantity > 0)
            .ToDictionaryAsync(x => x.RelicUnique, x => x.Quantity, ct);
        var activeGoalTargets = await db.UserGoals.AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsCompleted)
            .Select(x => new { x.TargetUnique, x.DisplayName }).ToListAsync(ct);
        var rewardLinks = await db.RelicRewards.AsNoTracking()
            .Select(x => new { x.ItemUnique, x.ItemName, x.RelicUnique }).ToListAsync(ct);
        var goalNamesByRelic = new Dictionary<string, HashSet<string>>();
        foreach (var target in activeGoalTargets)
        {
            var linkedKeys = rewardLinks
                .Where(x => x.ItemUnique.StartsWith(target.TargetUnique, StringComparison.OrdinalIgnoreCase)
                            || x.ItemName.Contains(target.DisplayName, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.RelicUnique).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var key in linkedKeys)
            {
                if (!goalNamesByRelic.TryGetValue(key, out var names))
                    goalNamesByRelic[key] = names = [];
                names.Add(target.DisplayName);
            }
        }
        var routes = new List<FarmRoute>();

        var variantsByFamily = relics
            .GroupBy(x => NormalizeRelicFamily(x.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var component in missing)
        {
            var fallbackDrops = component.Drops
                .Select(drop => new
                {
                    Drop = drop,
                    RelicName = CleanRelicName(drop.Location),
                    Refinement = RefinementFromLocation(drop.Location)
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.RelicName))
                .GroupBy(x => x.RelicName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
            var rewardRows = rewardsByComponent.GetValueOrDefault(component) ?? [];
            var families = rewardRows
                .Select(x => relics.FirstOrDefault(r => r.UniqueName == x.RelicUnique))
                .Where(x => x is not null)
                .Select(x => NormalizeRelicFamily(x!.Name))
                .Concat(fallbackDrops.Keys)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var family in families)
            {
                var variants = variantsByFamily.GetValueOrDefault(family) ?? relics.Where(x =>
                    string.Equals(x.Name, $"Reliquia {family}", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.Name, $"{family} Relic", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.Name, family, StringComparison.OrdinalIgnoreCase)).ToList();
                if (variants.Count == 0)
                    continue;

                var variantKeys = variants.Select(x => x.UniqueName)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var chances = rewardRows
                    .Where(x => variantKeys.Contains(x.RelicUnique))
                    .GroupBy(x => RefinementFromUnique(x.RelicUnique))
                    .ToDictionary(
                        x => x.Key,
                        x => NormalizeChance(x.OrderByDescending(y => y.Chance).First().Chance),
                        StringComparer.OrdinalIgnoreCase);
                if (chances.Count == 0 && fallbackDrops.TryGetValue(family, out var familyDrops))
                {
                    chances = familyDrops
                        .Where(x => !string.IsNullOrWhiteSpace(x.Refinement))
                        .GroupBy(x => x.Refinement)
                        .ToDictionary(x => x.Key, x => NormalizeChance(x.First().Drop.Chance),
                            StringComparer.OrdinalIgnoreCase);
                }
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
                    $"Reliquia {family}",
                    variants.FirstOrDefault(x => RefinementFromUnique(x.UniqueName) == "Intacta")?.UniqueName
                        ?? variants[0].UniqueName,
                    variants.All(x => x.Vaulted),
                    owned,
                    chances,
                    RarityFromChance(chances.GetValueOrDefault("Intacta")),
                    recommended,
                    TraceCost(recommended),
                    AttemptsFor(probability, .50),
                    AttemptsFor(probability, .75),
                    AttemptsFor(probability, .90),
                    locations,
                    usefulGoals,
                    RecommendAction(owned.Values.Sum(), variants.All(x => x.Vaulted), recommended)));
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

        var orderedRoutes = ordered.ToList();
        var savedBuilds = await db.SavedBuilds.AsNoTracking()
            .Where(x => x.UserId == userId && x.TargetUnique == goal.TargetUnique && !x.IsArchived)
            .OrderByDescending(x => x.UpdatedUtc).ToListAsync(ct);
        var buildModKeys = savedBuilds.SelectMany(x => BuildService.DeserializeSlots(x.ModsJson))
            .Select(x => x.ModUnique).Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct().ToList();
        var ownedBuildMods = buildModKeys.Count == 0 ? [] : await db.UserMods.AsNoTracking()
            .Where(x => x.UserId == userId && buildModKeys.Contains(x.ModUnique)
                        && (x.Owned || x.Quantity > 0))
            .Select(x => x.ModUnique).ToListAsync(ct);
        var ownedBuildSet = ownedBuildMods.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var relatedBuilds = savedBuilds.Select(x =>
        {
            var keys = BuildService.DeserializeSlots(x.ModsJson).Select(s => s.ModUnique)
                .Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return new FarmBuildProgress(x.Id, x.Name, keys.Count(k => ownedBuildSet.Contains(k)),
                keys.Count, x.IsCompleted);
        }).ToList();
        var traceBudget = componentStatuses.Where(x => !x.IsComplete)
            .Sum(component => orderedRoutes.Where(x => x.ComponentName == component.Name)
                .OrderByDescending(x => x.TotalOwned > 0).ThenBy(x => x.Vaulted)
                .ThenByDescending(x => x.RecommendedChance).FirstOrDefault()?.TraceCost ?? 0);

        return new FarmPlan(goal.Id, goal.DisplayName, strategy, componentStatuses,
            missing.Select(x => x.Name).ToList(), orderedRoutes, relatedBuilds, traceBudget);
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

    private static string NormalizeRelicFamily(string value)
    {
        var normalized = value.Trim();
        foreach (var refinement in new[]
                 {
                     "Intact", "Exceptional", "Flawless", "Radiant",
                     "Intacta", "Excepcional", "Perfecta", "Radiante"
                 })
        {
            normalized = normalized.Replace($" ({refinement})", "",
                StringComparison.OrdinalIgnoreCase);
            if (normalized.EndsWith($" {refinement}", StringComparison.OrdinalIgnoreCase))
                normalized = normalized[..^(refinement.Length + 1)];
        }
        if (normalized.EndsWith(" Relic", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[..^6];
        if (normalized.StartsWith("Reliquia ", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[9..];
        return string.Join(' ', normalized.Split(' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static bool IsRewardForComponent(
        string targetName, WarframeComponent component, RelicReward reward)
    {
        if (!string.IsNullOrWhiteSpace(component.UniqueName)
            && string.Equals(component.UniqueName, reward.ItemUnique,
                StringComparison.OrdinalIgnoreCase))
            return true;

        var rewardText = NormalizeComparison(reward.ItemName);
        var targetText = NormalizeComparison(targetName);
        if (rewardText.Length == 0 || targetText.Length == 0 || !rewardText.Contains(targetText))
            return false;

        var remainder = rewardText.Replace(targetText, "", StringComparison.Ordinal).Trim();
        var componentText = NormalizeComparison(component.Name);
        if (componentText.Contains(targetText, StringComparison.Ordinal))
            componentText = componentText.Replace(targetText, "", StringComparison.Ordinal).Trim();

        var componentKey = CanonicalComponentKey(componentText);
        var rewardKey = CanonicalComponentKey(remainder);
        return componentKey == "blueprint"
            ? rewardKey == "blueprint"
            : componentKey.Length > 0 && rewardKey.Contains(componentKey, StringComparison.Ordinal);
    }

    private static string CanonicalComponentKey(string value)
    {
        var text = $" {value} ";
        var replacements = new (string From, string To)[]
        {
            (" neuroptics ", " neuroptics "), (" neuropticas ", " neuroptics "),
            (" chassis ", " chassis "), (" chasis ", " chassis "),
            (" systems ", " systems "), (" sistemas ", " systems "),
            (" blueprint ", " blueprint "), (" plano ", " blueprint "),
            (" receiver ", " receiver "), (" receptor ", " receiver "),
            (" barrel ", " barrel "), (" canon ", " barrel "),
            (" stock ", " stock "), (" culata ", " stock "),
            (" blade ", " blade "), (" hoja ", " blade "),
            (" handle ", " handle "), (" grip ", " handle "),
            (" empunadura ", " handle "), (" link ", " link "),
            (" enlace ", " link "), (" ornament ", " ornament "),
            (" ornamento ", " ornament "), (" pouch ", " pouch "),
            (" bolsa ", " pouch "), (" string ", " string "),
            (" cuerda ", " string ")
        };
        foreach (var (from, to) in replacements)
            text = text.Replace(from, to, StringComparison.Ordinal);
        return string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string NormalizeComparison(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;
            builder.Append(char.IsLetterOrDigit(character)
                ? char.ToLowerInvariant(character)
                : ' ');
        }
        return string.Join(' ', builder.ToString().Split(' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static double NormalizeChance(double chance) => chance is > 0 and <= 1 ? chance * 100 : chance;
    private static int TraceCost(string refinement) => refinement switch
    {
        "Excepcional" => 25,
        "Perfecta" => 50,
        "Radiante" => 100,
        _ => 0
    };

    private static string RarityFromChance(double chance)
        => chance <= 2.01 ? "Rara" : chance <= 11.01 ? "Poco común" : "Común";

    private static string RecommendAction(int totalOwned, bool vaulted, string refinement)
        => totalOwned > 0
            ? $"Refinar a {refinement} y abrir la copia que ya posees."
            : vaulted
                ? "No posees esta reliquia y está vaulted: prioriza intercambio o una escuadra compartida."
                : $"Conseguir esta reliquia y mejorarla a {refinement}.";

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
                    WarframeSpanishText.Location(location),
                    WarframeSpanishText.Type(ReadString(item, "type")),
                    WarframeSpanishText.Location(ReadString(item, "rotation")),
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
    IReadOnlyList<FarmComponentStatus> Components,
    IReadOnlyList<string> MissingComponents,
    IReadOnlyList<FarmRoute> Routes,
    IReadOnlyList<FarmBuildProgress> RelatedBuilds,
    int RecommendedTraceBudget);

public sealed record FarmComponentStatus(string Name, int Required, int Owned, int Missing)
{
    public bool IsComplete => Missing == 0;
}
public sealed record FarmBuildProgress(int BuildId, string Name, int OwnedMods, int TotalMods, bool Completed)
{
    public int Percent => TotalMods == 0 ? 0 : (int)Math.Round(OwnedMods * 100d / TotalMods);
}

public sealed record FarmRoute(
    string ComponentName,
    string RelicName,
    string RelicUnique,
    bool Vaulted,
    IReadOnlyDictionary<string, int> Owned,
    IReadOnlyDictionary<string, double> Chances,
    string Rarity,
    string RecommendedRefinement,
    int TraceCost,
    int Attempts50,
    int Attempts75,
    int Attempts90,
    IReadOnlyList<RelicLocation> Locations,
    IReadOnlyList<string> UsefulGoals,
    string RecommendedAction)
{
    public int TotalOwned => Owned.Values.Sum();
    public double RecommendedChance => Chances.GetValueOrDefault(RecommendedRefinement);
}
public sealed record RelicLocation(
    string Location, string MissionType, string Rotation, double Chance);
