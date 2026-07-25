using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WarframeInventory.Data;
using WarframeInventory.Models;

namespace WarframeInventory.Services;

public sealed class GoalAnalysisService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public GoalAnalysisService(IDbContextFactory<ApplicationDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<IReadOnlyDictionary<int, GoalAnalysis>> AnalyzeAsync(
        string userId, IReadOnlyCollection<UserGoal> goals, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var analyses = new Dictionary<int, GoalAnalysis>();

        var userComponents = await db.UserComponents.AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);
        var userRelics = await db.UserRelics.AsNoTracking()
            .Where(x => x.UserId == userId && x.Quantity > 0)
            .ToListAsync(ct);
        var relicQuantities = userRelics.ToDictionary(x => x.RelicUnique, x => x.Quantity);
        var relics = await db.Relics.AsNoTracking().ToListAsync(ct);

        foreach (var goal in goals)
        {
            analyses[goal.Id] = goal.TargetType switch
            {
                "warframe" => await AnalyzeWarframeAsync(
                    db, goal, userId, userComponents, relics, relicQuantities, ct),
                "weapon" => await AnalyzeWeaponAsync(
                    db, goal, userId, userComponents, relics, relicQuantities, ct),
                "mod" => await AnalyzeModAsync(db, goal, userId, ct),
                "relic" => AnalyzeRelic(goal, relics, relicQuantities),
                _ => GoalAnalysis.Empty(goal.Id)
            };
        }

        return analyses;
    }

    private static async Task<GoalAnalysis> AnalyzeWarframeAsync(
        ApplicationDbContext db,
        UserGoal goal,
        string userId,
        IReadOnlyCollection<UserComponent> userComponents,
        IReadOnlyCollection<Relic> relics,
        IReadOnlyDictionary<string, int> relicQuantities,
        CancellationToken ct)
    {
        var item = await db.Warframes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UniqueName == goal.TargetUnique, ct);
        if (item is null)
            return GoalAnalysis.Empty(goal.Id);

        var owned = await db.UserWarframes.AsNoTracking().AnyAsync(
            x => x.UserId == userId && x.WarframeUnique == item.UniqueName && x.Owned, ct);
        return AnalyzeComponents(
            goal.Id, item.UniqueName, item.ComponentsJson, owned,
            userComponents, relics, relicQuantities);
    }

    private static async Task<GoalAnalysis> AnalyzeWeaponAsync(
        ApplicationDbContext db,
        UserGoal goal,
        string userId,
        IReadOnlyCollection<UserComponent> userComponents,
        IReadOnlyCollection<Relic> relics,
        IReadOnlyDictionary<string, int> relicQuantities,
        CancellationToken ct)
    {
        var item = await db.Weapons.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UniqueName == goal.TargetUnique, ct);
        if (item is null)
            return GoalAnalysis.Empty(goal.Id);

        var owned = await db.UserWeapons.AsNoTracking().AnyAsync(
            x => x.UserId == userId && x.WeaponUnique == item.UniqueName && x.Owned, ct);
        return AnalyzeComponents(
            goal.Id, item.UniqueName, item.ComponentsJson, owned,
            userComponents, relics, relicQuantities);
    }

    private static async Task<GoalAnalysis> AnalyzeModAsync(
        ApplicationDbContext db, UserGoal goal, string userId, CancellationToken ct)
    {
        var owned = await db.UserMods.AsNoTracking().AnyAsync(
            x => x.UserId == userId && x.ModUnique == goal.TargetUnique && x.Owned, ct);
        return new GoalAnalysis(goal.Id, owned ? 100 : 0, 1, owned ? 0 : 1,
            owned ? [] : [goal.DisplayName], [], owned);
    }

    private static GoalAnalysis AnalyzeRelic(
        UserGoal goal,
        IReadOnlyCollection<Relic> relics,
        IReadOnlyDictionary<string, int> relicQuantities)
    {
        var target = relics.FirstOrDefault(x => x.UniqueName == goal.TargetUnique);
        if (target is null)
            return GoalAnalysis.Empty(goal.Id);

        var variants = relics.Where(x => x.Name == target.Name).ToList();
        var total = variants.Sum(x => relicQuantities.GetValueOrDefault(x.UniqueName));
        return new GoalAnalysis(goal.Id, total > 0 ? 100 : 0, 1, total > 0 ? 0 : 1,
            total > 0 ? [] : [target.Name],
            [new GoalRelicSummary(target.Name, target.Vaulted, total)], total > 0);
    }

    private static GoalAnalysis AnalyzeComponents(
        int goalId,
        string parentUnique,
        string? componentsJson,
        bool parentOwned,
        IReadOnlyCollection<UserComponent> userComponents,
        IReadOnlyCollection<Relic> relics,
        IReadOnlyDictionary<string, int> relicQuantities)
    {
        if (parentOwned)
            return new GoalAnalysis(goalId, 100, 1, 0, [], [], true);

        var components = DeserializeComponents(componentsJson);
        if (components.Count == 0)
            return new GoalAnalysis(goalId, 0, 1, 1, ["Sin componentes registrados"], [], false);

        var inventory = userComponents
            .Where(x => x.ParentUnique == parentUnique)
            .ToDictionary(x => x.ComponentName, StringComparer.OrdinalIgnoreCase);
        var missing = components
            .Where(component => !inventory.TryGetValue(component.Name, out var stored)
                                || (!stored.Owned && stored.Quantity <= 0))
            .ToList();
        var completed = components.Count - missing.Count;
        var progress = (int)Math.Round(completed * 100d / components.Count);

        var relatedNames = missing
            .SelectMany(x => x.Drops)
            .Select(x => CleanRelicName(x.Location))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var related = new List<GoalRelicSummary>();

        foreach (var name in relatedNames)
        {
            var variants = relics.Where(x =>
                    string.Equals(x.Name, $"Reliquia {name}", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.Name, $"{name} Relic", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (variants.Count == 0)
                continue;

            related.Add(new GoalRelicSummary(
                variants[0].Name,
                variants.All(x => x.Vaulted),
                variants.Sum(x => relicQuantities.GetValueOrDefault(x.UniqueName))));
        }

        return new GoalAnalysis(
            goalId,
            progress,
            components.Count,
            missing.Count,
            missing.Select(x => x.Name).ToList(),
            related.OrderByDescending(x => x.OwnedQuantity).ThenBy(x => x.Name).ToList(),
            false);
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

    private static string CleanRelicName(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)
            || !location.Contains("Relic", StringComparison.OrdinalIgnoreCase))
            return "";

        return location
            .Replace("Reliquia", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Relic", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" (Radiant)", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" (Flawless)", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" (Exceptional)", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" (Intact)", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" (Radiante)", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" (Perfecta)", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" (Excepcional)", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" (Intacta)", "", StringComparison.OrdinalIgnoreCase)
            .Trim();
    }
}

public sealed record GoalAnalysis(
    int GoalId,
    int ProgressPercent,
    int ComponentCount,
    int MissingCount,
    IReadOnlyList<string> MissingComponents,
    IReadOnlyList<GoalRelicSummary> RelatedRelics,
    bool IsOwned)
{
    public bool IsOneAway => MissingCount == 1;
    public static GoalAnalysis Empty(int goalId) => new(goalId, 0, 0, 0, [], [], false);
}

public sealed record GoalRelicSummary(string Name, bool Vaulted, int OwnedQuantity);
