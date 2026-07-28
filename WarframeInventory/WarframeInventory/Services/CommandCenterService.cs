using Microsoft.EntityFrameworkCore;
using WarframeInventory.Data;
using WarframeInventory.Models;

namespace WarframeInventory.Services;

public sealed class CommandCenterService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly BuildableAnalysisService _buildable;
    private readonly GoalAnalysisService _goalAnalysis;

    public CommandCenterService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        BuildableAnalysisService buildable,
        GoalAnalysisService goalAnalysis)
    {
        _dbFactory = dbFactory;
        _buildable = buildable;
        _goalAnalysis = goalAnalysis;
    }

    public async Task<CommandCenterSnapshot> GetAsync(
        string userId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var frameTotal = await db.Warframes.CountAsync(ct);
        var weaponTotal = await db.Weapons.CountAsync(ct);
        var modTotal = await db.Mods.CountAsync(ct);
        var relicTotal = await db.Relics.Select(x => x.Name).Distinct().CountAsync(ct);

        var frameOwned = await db.UserWarframes.CountAsync(
            x => x.UserId == userId && x.Owned, ct);
        var weaponOwned = await db.UserWeapons.CountAsync(
            x => x.UserId == userId && x.Owned, ct);
        var modOwned = await db.UserMods.CountAsync(
            x => x.UserId == userId && x.Owned, ct);
        var ownedRelicUniques = await db.UserRelics.AsNoTracking()
            .Where(x => x.UserId == userId && x.Quantity > 0)
            .Select(x => x.RelicUnique)
            .ToListAsync(ct);
        var ownedRelicNames = await db.Relics.AsNoTracking()
            .Where(x => ownedRelicUniques.Contains(x.UniqueName))
            .Select(x => x.Name)
            .Distinct()
            .CountAsync(ct);
        var relicCopies = await db.UserRelics
            .Where(x => x.UserId == userId)
            .SumAsync(x => (int?)x.Quantity, ct) ?? 0;

        var goals = await db.UserGoals.AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);
        var analyses = await _goalAnalysis.AnalyzeAsync(userId, goals, ct);
        var candidates = await _buildable.AnalyzeAsync(userId, ct);
        var recent = await db.InventoryEvents.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.OccurredUtc)
            .Take(12)
            .ToListAsync(ct);
        var account = await db.AlecaAccountSnapshots.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId, ct);
        await ResolveNamesAsync(db, recent, ct);

        var metrics = new[]
        {
            new CollectionMetric("Warframes", frameOwned, frameTotal, "warframes"),
            new CollectionMetric("Armas", weaponOwned, weaponTotal, "weapons"),
            new CollectionMetric("Mods", modOwned, modTotal, "mods"),
            new CollectionMetric("Reliquias", ownedRelicNames, relicTotal, "relics")
        };
        var readyCount = candidates.Count(x => x.IsReady);
        var oneAwayCount = analyses.Values.Count(x => x.IsOneAway);
        var insight = BuildInsight(goals, readyCount, oneAwayCount, metrics);

        return new CommandCenterSnapshot(
            metrics,
            goals.Count(x => !x.IsCompleted),
            goals.Count(x => x.IsCompleted),
            readyCount,
            oneAwayCount,
            relicCopies,
            recent,
            insight,
            account);
    }

    private static async Task ResolveNamesAsync(
        ApplicationDbContext db, IReadOnlyCollection<InventoryEvent> events, CancellationToken ct)
    {
        var frameKeys = events.Where(x => x.Category == "warframe")
            .Select(x => x.TargetUnique).Distinct().ToList();
        var weaponKeys = events.Where(x => x.Category == "weapon")
            .Select(x => x.TargetUnique).Distinct().ToList();
        var modKeys = events.Where(x => x.Category == "mod")
            .Select(x => x.TargetUnique).Distinct().ToList();
        var relicKeys = events.Where(x => x.Category == "relic")
            .Select(x => x.TargetUnique).Distinct().ToList();
        var names = new Dictionary<string, string>();

        foreach (var item in await db.Warframes.AsNoTracking()
                     .Where(x => frameKeys.Contains(x.UniqueName))
                     .Select(x => new { x.UniqueName, x.Name }).ToListAsync(ct))
            names[item.UniqueName] = item.Name;
        foreach (var item in await db.Weapons.AsNoTracking()
                     .Where(x => weaponKeys.Contains(x.UniqueName))
                     .Select(x => new { x.UniqueName, x.Name }).ToListAsync(ct))
            names[item.UniqueName] = item.Name;
        foreach (var item in await db.Mods.AsNoTracking()
                     .Where(x => modKeys.Contains(x.UniqueName))
                     .Select(x => new { x.UniqueName, x.Name }).ToListAsync(ct))
            names[item.UniqueName] = item.Name;
        foreach (var item in await db.Relics.AsNoTracking()
                     .Where(x => relicKeys.Contains(x.UniqueName))
                     .Select(x => new { x.UniqueName, x.Name }).ToListAsync(ct))
            names[item.UniqueName] = item.Name;

        foreach (var entry in events.Where(x => x.Category != "component"))
            if (names.TryGetValue(entry.TargetUnique, out var name))
                entry.DisplayName = name;
    }

    private static string BuildInsight(
        IReadOnlyCollection<UserGoal> goals,
        int readyCount,
        int oneAwayCount,
        IReadOnlyCollection<CollectionMetric> metrics)
    {
        if (readyCount > 0)
            return $"Tienes {readyCount} set{(readyCount == 1 ? "" : "s")} listo{(readyCount == 1 ? "" : "s")} para ensamblar.";
        if (oneAwayCount > 0)
            return $"Estás a una sola pieza de completar {oneAwayCount} objetivo{(oneAwayCount == 1 ? "" : "s")}.";
        var priority = goals.Count(x => !x.IsCompleted && x.Priority == 1);
        if (priority > 0)
            return $"{priority} objetivo{(priority == 1 ? "" : "s")} de prioridad alta espera{(priority == 1 ? "" : "n")} una ruta de farmeo.";

        var lowest = metrics.OrderBy(x => x.ProgressPercent).First();
        return $"Tu siguiente oportunidad de progreso está en {lowest.Label}: {lowest.ProgressPercent}% completado.";
    }
}

public sealed record CommandCenterSnapshot(
    IReadOnlyList<CollectionMetric> Metrics,
    int ActiveGoals,
    int CompletedGoals,
    int ReadySets,
    int OneAwaySets,
    int RelicCopies,
    IReadOnlyList<InventoryEvent> RecentEvents,
    string Insight,
    AlecaAccountSnapshot? Account);

public sealed record CollectionMetric(string Label, int Owned, int Total, string Route)
{
    public int ProgressPercent => Total == 0 ? 0 : (int)Math.Round(Owned * 100d / Total);
}
