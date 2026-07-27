using Microsoft.EntityFrameworkCore;
using WarframeInventory.Data;
using WarframeInventory.Models;

namespace WarframeInventory.Services;

public sealed class RelicIntelligenceService
{
    public static readonly string[] Refinements =
        ["Intacta", "Excepcional", "Perfecta", "Radiante"];

    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly MarketPriceService _market;

    public RelicIntelligenceService(
        IDbContextFactory<ApplicationDbContext> dbFactory, MarketPriceService market)
    {
        _dbFactory = dbFactory;
        _market = market;
    }

    public async Task<RelicLabData?> LoadAsync(string uniqueName, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var selected = await db.Relics.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UniqueName == uniqueName, ct);
        if (selected is null)
            return null;

        var variants = await db.Relics.AsNoTracking()
            .Where(x => x.Name == selected.Name)
            .ToListAsync(ct);
        var keys = variants.Select(x => x.UniqueName).ToList();
        var rawRewards = await db.RelicRewards.AsNoTracking()
            .Where(x => keys.Contains(x.RelicUnique))
            .ToListAsync(ct);

        var rewards = rawRewards
            .GroupBy(x => x.ItemUnique)
            .Select(group =>
            {
                var first = group.First();
                var chances = group
                    .GroupBy(x => RefinementFromUnique(x.RelicUnique))
                    .ToDictionary(x => x.Key, x => NormalizeChance(x.First().Chance),
                        StringComparer.OrdinalIgnoreCase);
                FillCanonicalChances(chances);
                var rarity = RarityFromChances(chances.Values);

                return new RelicLabReward(
                    first.ItemUnique,
                    WarframeSpanishText.Reward(first.ItemName),
                    rarity,
                    chances,
                    Recommend(rarity, variants.All(x => x.Vaulted)),
                    first.MarketUrlName);
            })
            .OrderByDescending(x => x.Chances["Intacta"])
            .ThenBy(x => x.ItemName)
            .ToList();
        var pricedRewards = new List<RelicLabReward>();
        foreach (var reward in rewards)
        {
            var price = await _market.GetAsync(reward.MarketUrlName, ct);
            pricedRewards.Add(reward with
            {
                LowestSellPrice = price?.LowestSell,
                HighestBuyPrice = price?.HighestBuy
            });
        }

        return new RelicLabData(
            selected.UniqueName,
            selected.Name,
            selected.ImageName,
            variants.All(x => x.Vaulted),
            pricedRewards);
    }

    public static RelicSimulation Simulate(
        RelicLabReward reward, string refinement, int squadSize, int openings)
    {
        squadSize = Math.Clamp(squadSize, 1, 4);
        openings = Math.Max(1, openings);
        var percent = reward.Chances.GetValueOrDefault(refinement);
        var probability = Math.Clamp(percent / 100d, 0d, 1d);
        var rolls = squadSize * openings;
        var cumulative = 1d - Math.Pow(1d - probability, rolls);
        return new RelicSimulation(
            refinement,
            squadSize,
            openings,
            percent,
            cumulative * 100d,
            TraceCost(refinement) * rolls,
            AttemptsFor(percent, squadSize, .50),
            AttemptsFor(percent, squadSize, .75),
            AttemptsFor(percent, squadSize, .90));
    }

    public async Task RecordOpeningAsync(
        string userId, RelicLabData lab, RelicLabReward reward, string refinement,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        db.RelicOpenings.Add(new RelicOpening
        {
            UserId = userId,
            RelicName = lab.Name,
            RelicUnique = lab.UniqueName,
            Refinement = refinement,
            RewardUnique = reward.ItemUnique,
            RewardName = reward.ItemName
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<RelicOpening>> HistoryAsync(
        string userId, string relicName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return [];
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.RelicOpenings.AsNoTracking()
            .Where(x => x.UserId == userId && x.RelicName == relicName)
            .OrderByDescending(x => x.OpenedUtc).Take(30).ToListAsync(ct);
    }

    private static int AttemptsFor(double percent, int squadSize, double confidence)
    {
        var probabilityPerRun = 1d - Math.Pow(1d - Math.Clamp(percent / 100d, 0d, 1d),
            Math.Clamp(squadSize, 1, 4));
        if (probabilityPerRun <= 0)
            return 0;
        if (probabilityPerRun >= 1)
            return 1;
        return (int)Math.Ceiling(Math.Log(1d - confidence) / Math.Log(1d - probabilityPerRun));
    }

    private static string Recommend(string rarity, bool vaulted) => rarity switch
    {
        "Rara" => vaulted
            ? "Radiante: maximiza una reliquia difícil de reemplazar."
            : "Radiante: la mejora de probabilidad compensa los vestigios.",
        "Poco común" => "Perfecta ofrece buen equilibrio; Radiante maximiza la probabilidad.",
        _ => "Intacta: refinar reduce la probabilidad de esta recompensa común."
    };

    private static int TraceCost(string refinement) => refinement switch
    {
        "Excepcional" => 25,
        "Perfecta" => 50,
        "Radiante" => 100,
        _ => 0
    };

    private static string RefinementFromUnique(string uniqueName)
        => uniqueName.EndsWith("Silver", StringComparison.OrdinalIgnoreCase) ? "Excepcional"
            : uniqueName.EndsWith("Gold", StringComparison.OrdinalIgnoreCase) ? "Perfecta"
            : uniqueName.EndsWith("Platinum", StringComparison.OrdinalIgnoreCase) ? "Radiante"
            : "Intacta";

    private static double NormalizeChance(double chance)
        => chance is > 0 and <= 1 ? chance * 100d : chance;

    private static string RarityFromChances(IEnumerable<double> chances)
    {
        var values = chances.ToList();
        if (values.Count == 0)
            return "Desconocida";
        if (values.Max() > 20d)
            return "Común";
        return values.Min() <= 2.01d ? "Rara" : "Poco común";
    }

    private static void FillCanonicalChances(IDictionary<string, double> chances)
    {
        var intact = chances.TryGetValue("Intacta", out var intactChance) ? intactChance : 0d;
        var canonical = intact <= 2.01d
            ? new[] { 2d, 4d, 6d, 10d }
            : intact <= 11.01d
                ? new[] { 11d, 13d, 17d, 20d }
                : new[] { 25.33d, 23.33d, 20d, 16.67d };
        for (var i = 0; i < Refinements.Length; i++)
            chances.TryAdd(Refinements[i], canonical[i]);
    }
}

public sealed record RelicLabData(
    string UniqueName,
    string Name,
    string? ImageName,
    bool Vaulted,
    IReadOnlyList<RelicLabReward> Rewards);

public sealed record RelicLabReward(
    string ItemUnique,
    string ItemName,
    string Rarity,
    IReadOnlyDictionary<string, double> Chances,
    string Recommendation,
    string? MarketUrlName)
{
    public int? LowestSellPrice { get; init; }
    public int? HighestBuyPrice { get; init; }
}

public sealed record RelicSimulation(
    string Refinement,
    int SquadSize,
    int Openings,
    double IndividualChance,
    double CumulativeChance,
    int TotalTraceCost,
    int Attempts50,
    int Attempts75,
    int Attempts90);
