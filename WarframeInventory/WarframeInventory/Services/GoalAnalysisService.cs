using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WarframeInventory.Data;
using WarframeInventory.Models;

namespace WarframeInventory.Services;

public sealed class GoalAnalysisService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    public GoalAnalysisService(IDbContextFactory<ApplicationDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<IReadOnlyDictionary<int, GoalAnalysis>> AnalyzeAsync(
        string userId, IReadOnlyCollection<UserGoal> goals, CancellationToken ct = default)
    {
        if (goals.Count == 0) return new Dictionary<int, GoalAnalysis>();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var warframeKeys = goals.Where(x => x.TargetType == "warframe").Select(x => x.TargetUnique).Distinct().ToList();
        var weaponKeys = goals.Where(x => x.TargetType == "weapon").Select(x => x.TargetUnique).Distinct().ToList();
        var modKeys = goals.Where(x => x.TargetType == "mod").Select(x => x.TargetUnique).Distinct().ToList();

        var userComponents = await db.UserComponents.AsNoTracking().Where(x => x.UserId == userId).ToListAsync(ct);
        var userRelics = await db.UserRelics.AsNoTracking().Where(x => x.UserId == userId && x.Quantity > 0).ToListAsync(ct);
        var relicQuantities = userRelics.ToDictionary(x => x.RelicUnique, x => x.Quantity);
        var relics = await db.Relics.AsNoTracking()
            .Select(x => new Relic { UniqueName = x.UniqueName, Name = x.Name, Vaulted = x.Vaulted })
            .ToListAsync(ct);
        var warframes = await db.Warframes.AsNoTracking().Where(x => warframeKeys.Contains(x.UniqueName))
            .Select(x => new { x.UniqueName, x.ComponentsJson }).ToDictionaryAsync(x => x.UniqueName, ct);
        var weapons = await db.Weapons.AsNoTracking().Where(x => weaponKeys.Contains(x.UniqueName))
            .Select(x => new { x.UniqueName, x.ComponentsJson }).ToDictionaryAsync(x => x.UniqueName, ct);
        var ownedWarframes = (await db.UserWarframes.AsNoTracking()
            .Where(x => x.UserId == userId && x.Owned && warframeKeys.Contains(x.WarframeUnique))
            .Select(x => x.WarframeUnique).ToListAsync(ct)).ToHashSet();
        var ownedWeapons = (await db.UserWeapons.AsNoTracking()
            .Where(x => x.UserId == userId && x.Owned && weaponKeys.Contains(x.WeaponUnique))
            .Select(x => x.WeaponUnique).ToListAsync(ct)).ToHashSet();
        var ownedMods = (await db.UserMods.AsNoTracking()
            .Where(x => x.UserId == userId && (x.Owned || x.Quantity > 0))
            .Select(x => x.ModUnique).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var builds = await db.SavedBuilds.AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsArchived).ToListAsync(ct);
        var buildsByTarget = builds.GroupBy(x => x.TargetUnique).ToDictionary(x => x.Key,
            x => (IReadOnlyList<GoalBuildSummary>)x.Select(build =>
            {
                var keys = BuildService.DeserializeSlots(build.ModsJson).Select(s => s.ModUnique)
                    .Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                return new GoalBuildSummary(build.Id, build.Name,
                    keys.Count(k => ownedMods.Contains(k)), keys.Count, build.IsCompleted);
            }).ToList());

        var result = new Dictionary<int, GoalAnalysis>();
        foreach (var goal in goals)
        {
            GoalAnalysis analysis;
            if (goal.TargetType == "warframe" && warframes.TryGetValue(goal.TargetUnique, out var wf))
                analysis = AnalyzeComponents(goal.Id, goal.TargetUnique, wf.ComponentsJson,
                    ownedWarframes.Contains(goal.TargetUnique), userComponents, relics, relicQuantities);
            else if (goal.TargetType == "weapon" && weapons.TryGetValue(goal.TargetUnique, out var weapon))
                analysis = AnalyzeComponents(goal.Id, goal.TargetUnique, weapon.ComponentsJson,
                    ownedWeapons.Contains(goal.TargetUnique), userComponents, relics, relicQuantities);
            else if (goal.TargetType == "mod")
            {
                var owned = ownedMods.Contains(goal.TargetUnique);
                analysis = new GoalAnalysis(goal.Id, owned ? 100 : 0, 1, owned ? 0 : 1,
                    owned ? [] : [goal.DisplayName], [], owned);
            }
            else if (goal.TargetType == "relic")
                analysis = AnalyzeRelic(goal, relics, relicQuantities);
            else analysis = GoalAnalysis.Empty(goal.Id);

            result[goal.Id] = analysis with
            {
                Builds = buildsByTarget.GetValueOrDefault(goal.TargetUnique) ?? []
            };
        }
        return result;
    }

    private static GoalAnalysis AnalyzeRelic(UserGoal goal, IReadOnlyCollection<Relic> relics,
        IReadOnlyDictionary<string, int> relicQuantities)
    {
        var target = relics.FirstOrDefault(x => x.UniqueName == goal.TargetUnique);
        if (target is null) return GoalAnalysis.Empty(goal.Id);
        var variants = relics.Where(x => x.Name == target.Name).ToList();
        var total = variants.Sum(x => relicQuantities.GetValueOrDefault(x.UniqueName));
        var desired = Math.Max(1, goal.DesiredQuantity);
        var complete = total >= desired;
        return new GoalAnalysis(goal.Id, (int)Math.Min(100, Math.Round(total * 100d / desired)),
            desired, Math.Max(0, desired - total), complete ? [] : [$"{desired - total} × {target.Name}"],
            [CreateRelicSummary(target.Name, variants, relicQuantities, "Reliquia")], complete)
        { CurrentQuantity = total, DesiredQuantity = desired };
    }

    private static GoalAnalysis AnalyzeComponents(int goalId, string parentUnique, string? componentsJson,
        bool parentOwned, IReadOnlyCollection<UserComponent> userComponents,
        IReadOnlyCollection<Relic> relics, IReadOnlyDictionary<string, int> relicQuantities)
    {
        if (parentOwned) return new GoalAnalysis(goalId, 100, 1, 0, [], [], true);
        var components = DeserializeComponents(componentsJson);
        if (components.Count == 0) return new GoalAnalysis(goalId, 0, 1, 1, ["Sin componentes registrados"], [], false);
        var inventory = userComponents.Where(x => x.ParentUnique == parentUnique)
            .ToDictionary(x => x.ComponentName, StringComparer.OrdinalIgnoreCase);
        var missing = components.Where(component => !inventory.TryGetValue(component.Name, out var stored)
            || (!stored.Owned && stored.Quantity < Math.Max(1, component.ItemCount))).ToList();
        var progress = (int)Math.Round((components.Count - missing.Count) * 100d / components.Count);
        var relatedSources = missing.SelectMany(x => x.Drops)
            .Select(x => new { Name = CleanRelicName(x.Location), x.Chance, x.Location })
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var related = relatedSources.Select(source => new
            {
                Source = source,
                Variants = relics.Where(x =>
                    string.Equals(x.Name, $"Reliquia {source.Key}", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.Name, $"{source.Key} Relic", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.Name, source.Key, StringComparison.OrdinalIgnoreCase)).ToList()
            })
            .Where(x => x.Variants.Count > 0)
            .Select(x => CreateRelicSummary(x.Variants[0].Name, x.Variants, relicQuantities,
                RarityFromChance(x.Source.OrderBy(drop => RefinementOrder(drop.Location)).First().Chance)))
            .OrderByDescending(x => x.OwnedQuantity).ThenBy(x => x.Name).ToList();
        return new GoalAnalysis(goalId, progress, components.Count, missing.Count,
            missing.Select(x => x.ItemCount > 1 ? $"{x.ItemCount} × {x.Name}" : x.Name).ToList(), related, false);
    }

    private static List<WarframeComponent> DeserializeComponents(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<WarframeComponent>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? []; }
        catch (JsonException) { return []; }
    }

    private static string CleanRelicName(string? location)
    {
        if (string.IsNullOrWhiteSpace(location) || !location.Contains("Relic", StringComparison.OrdinalIgnoreCase)) return "";
        var parenthesis = location.IndexOf(" (", StringComparison.Ordinal);
        var value = parenthesis >= 0 ? location[..parenthesis] : location;
        return value.Replace("Reliquia", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Relic", "", StringComparison.OrdinalIgnoreCase).Trim();
    }

    private static GoalRelicSummary CreateRelicSummary(string name, IReadOnlyCollection<Relic> variants,
        IReadOnlyDictionary<string, int> quantities, string rarity)
    {
        int Quantity(string suffix) => variants.Where(x => x.UniqueName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .Sum(x => quantities.GetValueOrDefault(x.UniqueName));
        var intact = variants.Where(x => !x.UniqueName.EndsWith("Silver", StringComparison.OrdinalIgnoreCase)
                                         && !x.UniqueName.EndsWith("Gold", StringComparison.OrdinalIgnoreCase)
                                         && !x.UniqueName.EndsWith("Platinum", StringComparison.OrdinalIgnoreCase))
            .Sum(x => quantities.GetValueOrDefault(x.UniqueName));
        return new GoalRelicSummary(name, variants.All(x => x.Vaulted),
            intact, Quantity("Silver"), Quantity("Gold"), Quantity("Platinum"), rarity);
    }

    private static int RefinementOrder(string? location)
        => location?.Contains("Intact", StringComparison.OrdinalIgnoreCase) == true ? 0
            : location?.Contains("Exceptional", StringComparison.OrdinalIgnoreCase) == true ? 1
            : location?.Contains("Flawless", StringComparison.OrdinalIgnoreCase) == true ? 2 : 3;
    private static string RarityFromChance(double chance)
    {
        var percentage = chance is > 0 and <= 1 ? chance * 100 : chance;
        return percentage <= 2.01 ? "Rara" : percentage <= 11.01 ? "Poco común" : "Común";
    }
}

public sealed record GoalAnalysis(int GoalId, int ProgressPercent, int ComponentCount, int MissingCount,
    IReadOnlyList<string> MissingComponents, IReadOnlyList<GoalRelicSummary> RelatedRelics, bool IsOwned)
{
    public bool IsOneAway => MissingCount == 1;
    public int CurrentQuantity { get; init; }
    public int DesiredQuantity { get; init; }
    public IReadOnlyList<GoalBuildSummary> Builds { get; init; } = [];
    public int BuildProgressPercent => Builds.Count == 0 ? 0 : (int)Math.Round(Builds.Average(x => x.Percent));
    public int OverallProgressPercent => Builds.Count == 0 ? ProgressPercent
        : (int)Math.Round((ProgressPercent + BuildProgressPercent) / 2d);
    public static GoalAnalysis Empty(int goalId) => new(goalId, 0, 0, 0, [], [], false);
}
public sealed record GoalRelicSummary(string Name, bool Vaulted, int Intact, int Exceptional,
    int Flawless, int Radiant, string Rarity)
{
    public int OwnedQuantity => Intact + Exceptional + Flawless + Radiant;
}
public sealed record GoalBuildSummary(int BuildId, string Name, int OwnedMods, int TotalMods, bool Completed)
{
    public int Percent => TotalMods == 0 ? 0 : (int)Math.Round(OwnedMods * 100d / TotalMods);
}
