using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WarframeInventory.Data;

namespace WarframeInventory.Services;

public sealed class WorldstateService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public WorldstateService(
        HttpClient http,
        IMemoryCache cache,
        IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _http = http;
        _cache = cache;
        _dbFactory = dbFactory;
    }

    public async Task<WorldstateBrief> LoadAsync(string? userId, CancellationToken ct = default)
    {
        var live = await GetLiveStateAsync(ct);
        var ownedByTier = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var goalsByTier = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var owned = await (
                from userRelic in db.UserRelics.AsNoTracking()
                join relic in db.Relics.AsNoTracking()
                    on userRelic.RelicUnique equals relic.UniqueName
                where userRelic.UserId == userId && userRelic.Quantity > 0
                select new { relic.Name, userRelic.Quantity })
                .ToListAsync(ct);

            foreach (var entry in owned)
            {
                var tier = Tiers.FirstOrDefault(x =>
                    entry.Name.Contains(x, StringComparison.OrdinalIgnoreCase));
                if (tier is not null)
                    ownedByTier[tier] = ownedByTier.GetValueOrDefault(tier) + entry.Quantity;
            }

            var goals = await db.UserGoals.AsNoTracking()
                .Where(x => x.UserId == userId && !x.IsCompleted)
                .Select(x => new { x.DisplayName, x.TargetUnique })
                .ToListAsync(ct);
            var targetKeys = goals.Select(x => x.TargetUnique).ToList();
            var relatedRewards = new List<GoalRewardLink>();
            foreach (var target in targetKeys)
            {
                relatedRewards.AddRange(await db.RelicRewards.AsNoTracking()
                    .Where(x => x.ItemUnique.StartsWith(target))
                    .Select(x => new GoalRewardLink(x.RelicUnique, x.ItemUnique))
                    .ToListAsync(ct));
            }
            var relicKeys = relatedRewards.Select(x => x.RelicUnique).Distinct().ToList();
            var relatedRelics = await db.Relics.AsNoTracking()
                .Where(x => relicKeys.Contains(x.UniqueName))
                .Select(x => new { x.UniqueName, x.Name })
                .ToListAsync(ct);
            foreach (var relation in relatedRewards)
            {
                var relicName = relatedRelics.FirstOrDefault(x =>
                    x.UniqueName == relation.RelicUnique)?.Name ?? "";
                var tier = Tiers.FirstOrDefault(x =>
                    relicName.Contains(x, StringComparison.OrdinalIgnoreCase));
                var goal = goals.FirstOrDefault(x =>
                    relation.ItemUnique.StartsWith(x.TargetUnique, StringComparison.OrdinalIgnoreCase));
                if (tier is null || goal is null)
                    continue;
                if (!goalsByTier.TryGetValue(tier, out var names))
                    goalsByTier[tier] = names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                names.Add(goal.DisplayName);
            }
        }

        return new WorldstateBrief(
            live.Fissures.Select(x => x with
            {
                OwnedRelics = ownedByTier.GetValueOrDefault(x.Tier),
                RelatedGoals = goalsByTier.TryGetValue(x.Tier, out var names)
                    ? names.OrderBy(x => x).ToList() : []
            }).ToList(),
            live.Alerts,
            live.Invasions,
            live.Cycles,
            live.VoidTrader,
            live.Nightwave,
            DateTime.UtcNow);
    }

    private async Task<LiveWorldstate> GetLiveStateAsync(CancellationToken ct)
    {
        const string cacheKey = "worldstate:complete:pc";
        if (_cache.TryGetValue(cacheKey, out LiveWorldstate? cached)
            && cached is not null)
            return cached;

        try
        {
            var response = await _http.GetFromJsonAsync<WorldstateResponse>(
                "pc?language=en", ct) ?? new();
            var fissures = response.Fissures
                .Where(x => x.Active && !x.Expired)
                .Select(x => new ActiveFissure(
                    x.Id,
                    x.Tier,
                    x.Node,
                    x.MissionType,
                    x.Enemy,
                    x.Expiry,
                    x.IsHard,
                    x.IsStorm,
                    0,
                    []))
                .OrderBy(x => Array.IndexOf(Tiers, x.Tier))
                .ThenBy(x => x.Expiry)
                .ToList();
            var alerts = response.Alerts.Select(x => new WorldAlert(
                x.Id, x.Mission.Node, x.Mission.Type,
                x.Mission.Reward.AsString, x.Expiry)).ToList();
            var invasions = response.Invasions.Where(x => !x.Completed)
                .Select(x => new WorldInvasion(
                    x.Id, x.Node, x.Attacker.Faction, x.Defender.Faction,
                    RewardText(x.Attacker.Reward), RewardText(x.Defender.Reward),
                    Math.Clamp(x.Completion, 0, 100))).ToList();
            var cycles = new[]
            {
                Cycle("Cetus", response.CetusCycle),
                Cycle("Vallis", response.VallisCycle),
                Cycle("Cambion", response.CambionCycle),
                Cycle("Zariman", response.ZarimanCycle),
                Cycle("Duviri", response.DuviriCycle)
            }.Where(x => x is not null).Cast<WorldCycle>().ToList();
            var trader = string.IsNullOrWhiteSpace(response.VoidTrader.Id) ? null
                : new VoidTraderBrief(
                    response.VoidTrader.Character,
                    response.VoidTrader.Location,
                    response.VoidTrader.Activation,
                    response.VoidTrader.Expiry,
                    response.VoidTrader.Inventory.Select(x =>
                        new TraderItem(x.Item, x.Ducats, x.Credits)).ToList());
            var nightwave = string.IsNullOrWhiteSpace(response.Nightwave.Id) ? null
                : new NightwaveBrief(
                    response.Nightwave.Season,
                    response.Nightwave.Expiry,
                    response.Nightwave.ActiveChallenges.Select(x =>
                        new NightwaveChallenge(x.Title, x.Desc, x.Reputation,
                            x.IsDaily, x.IsElite, x.Expiry)).ToList());
            var result = new LiveWorldstate(
                fissures, alerts, invasions, cycles, trader, nightwave);
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(2));
            return result;
        }
        catch (HttpRequestException)
        {
            return LiveWorldstate.Empty;
        }
        catch (TaskCanceledException)
        {
            return LiveWorldstate.Empty;
        }
    }

    private static WorldCycle? Cycle(string location, CycleResponse source)
        => string.IsNullOrWhiteSpace(source.Id) ? null
            : new WorldCycle(location, source.State, source.Expiry);

    private static string RewardText(RewardResponse reward)
    {
        if (!string.IsNullOrWhiteSpace(reward.AsString))
            return reward.AsString;
        var items = reward.CountedItems.Select(x => $"{x.Count} × {x.Type}")
            .Concat(reward.Items);
        return string.Join(", ", items);
    }

    private static readonly string[] Tiers = ["Lith", "Meso", "Neo", "Axi", "Requiem"];
    private sealed record GoalRewardLink(string RelicUnique, string ItemUnique);

    private sealed class FissureResponse
    {
        public string Id { get; set; } = "";
        public string Tier { get; set; } = "";
        public string Node { get; set; } = "";
        public string MissionType { get; set; } = "";
        public string Enemy { get; set; } = "";
        public DateTime Expiry { get; set; }
        public bool Active { get; set; }
        public bool Expired { get; set; }
        public bool IsHard { get; set; }
        public bool IsStorm { get; set; }
    }

    private sealed class WorldstateResponse
    {
        public List<FissureResponse> Fissures { get; set; } = [];
        public List<AlertResponse> Alerts { get; set; } = [];
        public List<InvasionResponse> Invasions { get; set; } = [];
        public CycleResponse CetusCycle { get; set; } = new();
        public CycleResponse VallisCycle { get; set; } = new();
        public CycleResponse CambionCycle { get; set; } = new();
        public CycleResponse ZarimanCycle { get; set; } = new();
        public CycleResponse DuviriCycle { get; set; } = new();
        public VoidTraderResponse VoidTrader { get; set; } = new();
        public NightwaveResponse Nightwave { get; set; } = new();
    }

    private sealed class AlertResponse
    {
        public string Id { get; set; } = "";
        public DateTime Expiry { get; set; }
        public MissionResponse Mission { get; set; } = new();
    }
    private sealed class MissionResponse
    {
        public string Node { get; set; } = "";
        public string Type { get; set; } = "";
        public RewardResponse Reward { get; set; } = new();
    }
    private sealed class InvasionResponse
    {
        public string Id { get; set; } = "";
        public string Node { get; set; } = "";
        public InvasionSideResponse Attacker { get; set; } = new();
        public InvasionSideResponse Defender { get; set; } = new();
        public double Completion { get; set; }
        public bool Completed { get; set; }
    }
    private sealed class InvasionSideResponse
    {
        public string Faction { get; set; } = "";
        public RewardResponse Reward { get; set; } = new();
    }
    private sealed class RewardResponse
    {
        public string AsString { get; set; } = "";
        public List<string> Items { get; set; } = [];
        public List<CountedItemResponse> CountedItems { get; set; } = [];
    }
    private sealed class CountedItemResponse
    {
        public int Count { get; set; }
        public string Type { get; set; } = "";
    }
    private sealed class CycleResponse
    {
        public string Id { get; set; } = "";
        public string State { get; set; } = "";
        public DateTime Expiry { get; set; }
    }
    private sealed class VoidTraderResponse
    {
        public string Id { get; set; } = "";
        public string Character { get; set; } = "Baro Ki'Teer";
        public string Location { get; set; } = "";
        public DateTime Activation { get; set; }
        public DateTime Expiry { get; set; }
        public List<TraderItemResponse> Inventory { get; set; } = [];
    }
    private sealed class TraderItemResponse
    {
        public string Item { get; set; } = "";
        public int Ducats { get; set; }
        public int Credits { get; set; }
    }
    private sealed class NightwaveResponse
    {
        public string Id { get; set; } = "";
        public int Season { get; set; }
        public DateTime Expiry { get; set; }
        public List<NightwaveChallengeResponse> ActiveChallenges { get; set; } = [];
    }
    private sealed class NightwaveChallengeResponse
    {
        public string Title { get; set; } = "";
        public string Desc { get; set; } = "";
        public int Reputation { get; set; }
        public bool IsDaily { get; set; }
        public bool IsElite { get; set; }
        public DateTime Expiry { get; set; }
    }
}

public sealed record WorldstateBrief(
    IReadOnlyList<ActiveFissure> Fissures,
    IReadOnlyList<WorldAlert> Alerts,
    IReadOnlyList<WorldInvasion> Invasions,
    IReadOnlyList<WorldCycle> Cycles,
    VoidTraderBrief? VoidTrader,
    NightwaveBrief? Nightwave,
    DateTime LoadedUtc);

public sealed record ActiveFissure(
    string Id,
    string Tier,
    string Node,
    string MissionType,
    string Enemy,
    DateTime Expiry,
    bool IsHard,
    bool IsStorm,
    int OwnedRelics,
    IReadOnlyList<string> RelatedGoals);
public sealed record WorldAlert(
    string Id, string Node, string MissionType, string Reward, DateTime Expiry);
public sealed record WorldInvasion(
    string Id, string Node, string Attacker, string Defender,
    string AttackerReward, string DefenderReward, double Completion);
public sealed record WorldCycle(string Location, string State, DateTime Expiry);
public sealed record VoidTraderBrief(
    string Character, string Location, DateTime Activation, DateTime Expiry,
    IReadOnlyList<TraderItem> Inventory);
public sealed record TraderItem(string Name, int Ducats, int Credits);
public sealed record NightwaveBrief(
    int Season, DateTime Expiry, IReadOnlyList<NightwaveChallenge> Challenges);
public sealed record NightwaveChallenge(
    string Title, string Description, int Reputation,
    bool IsDaily, bool IsElite, DateTime Expiry);
internal sealed record LiveWorldstate(
    IReadOnlyList<ActiveFissure> Fissures,
    IReadOnlyList<WorldAlert> Alerts,
    IReadOnlyList<WorldInvasion> Invasions,
    IReadOnlyList<WorldCycle> Cycles,
    VoidTraderBrief? VoidTrader,
    NightwaveBrief? Nightwave)
{
    public static LiveWorldstate Empty { get; } = new([], [], [], [], null, null);
}
