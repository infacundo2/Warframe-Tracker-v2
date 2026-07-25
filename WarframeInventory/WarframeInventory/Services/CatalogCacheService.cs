using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WarframeInventory.Data;

namespace WarframeInventory.Services;

public sealed record CatalogCounts(int Warframes, int Weapons, int Mods, int Relics);

public sealed class CatalogCacheService
{
    private const string CountsKey = "catalog:counts:v1";
    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public CatalogCacheService(
        IMemoryCache cache,
        IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _cache = cache;
        _dbFactory = dbFactory;
    }

    public async Task<CatalogCounts> GetCountsAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CountsKey, out CatalogCounts? cached) && cached is not null)
            return cached;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var counts = new CatalogCounts(
            await db.Warframes.AsNoTracking().CountAsync(ct),
            await db.Weapons.AsNoTracking().CountAsync(ct),
            await db.Mods.AsNoTracking().CountAsync(ct),
            await db.Relics.AsNoTracking().CountAsync(ct));
        _cache.Set(CountsKey, counts, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
            Size = 1
        });
        return counts;
    }

    public void Invalidate() => _cache.Remove(CountsKey);
}
