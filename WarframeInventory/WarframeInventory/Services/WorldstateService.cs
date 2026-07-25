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
        var fissures = await GetFissuresAsync(ct);
        var ownedByTier = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
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
        }

        return new WorldstateBrief(
            fissures.Select(x => x with
            {
                OwnedRelics = ownedByTier.GetValueOrDefault(x.Tier)
            }).ToList(),
            DateTime.UtcNow);
    }

    private async Task<IReadOnlyList<ActiveFissure>> GetFissuresAsync(CancellationToken ct)
    {
        const string cacheKey = "worldstate:fissures:pc";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<ActiveFissure>? cached)
            && cached is not null)
            return cached;

        try
        {
            var response = await _http.GetFromJsonAsync<List<FissureResponse>>(
                "pc/fissures?language=es", ct) ?? [];
            var result = response
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
                    0))
                .OrderBy(x => Array.IndexOf(Tiers, x.Tier))
                .ThenBy(x => x.Expiry)
                .ToList();
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(2));
            return result;
        }
        catch (HttpRequestException)
        {
            return [];
        }
        catch (TaskCanceledException)
        {
            return [];
        }
    }

    private static readonly string[] Tiers = ["Lith", "Meso", "Neo", "Axi", "Requiem"];

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
}

public sealed record WorldstateBrief(
    IReadOnlyList<ActiveFissure> Fissures,
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
    int OwnedRelics);
