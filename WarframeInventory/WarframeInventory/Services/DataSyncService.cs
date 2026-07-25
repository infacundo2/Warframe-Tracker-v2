using Microsoft.EntityFrameworkCore;
using WarframeInventory.Data;
using WarframeInventory.Models;

namespace WarframeInventory.Services;

public sealed class DataSyncService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly WarframeApiService _api;
    private readonly ILogger<DataSyncService> _logger;
    private readonly CatalogCacheService _cache;

    public DataSyncService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        WarframeApiService api,
        ILogger<DataSyncService> logger,
        CatalogCacheService cache)
    {
        _dbFactory = dbFactory;
        _api = api;
        _logger = logger;
        _cache = cache;
    }

    public async Task SyncAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var state = await db.DataSyncStates.FindAsync([1], cancellationToken)
                    ?? new DataSyncState { Id = 1 };
        if (db.Entry(state).State == EntityState.Detached)
            db.DataSyncStates.Add(state);

        state.LastAttemptUtc = DateTime.UtcNow;
        state.Status = "Running";
        state.Error = null;
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var warframesTask = _api.GetWarframesAsync(cancellationToken);
            var weaponsTask = _api.GetWeaponsAsync(cancellationToken);
            var modsTask = _api.GetModsAsync(cancellationToken);
            var relicsTask = _api.GetRelicsAsync(cancellationToken);
            await Task.WhenAll(warframesTask, weaponsTask, modsTask, relicsTask);

            await UpsertWarframesAsync(db, warframesTask.Result, cancellationToken);
            await UpsertWeaponsAsync(db, weaponsTask.Result, cancellationToken);
            await UpsertModsAsync(db, modsTask.Result, cancellationToken);
            await UpsertRelicsAsync(db, relicsTask.Result, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);

            state.LastSuccessUtc = DateTime.UtcNow;
            state.Status = "Succeeded";
            await db.SaveChangesAsync(cancellationToken);
            _cache.Invalidate();
            _logger.LogInformation("Warframe catalog synchronization completed.");
        }
        catch (Exception ex)
        {
            state.Status = "Failed";
            state.Error = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            await db.SaveChangesAsync(CancellationToken.None);
            _logger.LogError(ex, "Warframe catalog synchronization failed.");
            throw;
        }
    }

    private static async Task UpsertWarframesAsync(
        ApplicationDbContext db, IReadOnlyCollection<Warframe> incoming, CancellationToken ct)
    {
        var existing = await db.Warframes.ToDictionaryAsync(x => x.UniqueName, ct);
        foreach (var item in incoming.Where(x => !string.IsNullOrWhiteSpace(x.UniqueName)))
        {
            if (!existing.TryGetValue(item.UniqueName, out var entity))
            {
                db.Warframes.Add(item);
                existing[item.UniqueName] = item;
                continue;
            }
            entity.Name = item.Name;
            entity.Description = item.Description;
            entity.ImageName = item.ImageName;
            entity.Health = item.Health;
            entity.Armor = item.Armor;
            entity.ComponentsJson = item.ComponentsJson;
        }
    }

    private static async Task UpsertWeaponsAsync(
        ApplicationDbContext db, IReadOnlyCollection<Weapon> incoming, CancellationToken ct)
    {
        var existing = await db.Weapons.ToDictionaryAsync(x => x.UniqueName, ct);
        foreach (var item in incoming.Where(x => !string.IsNullOrWhiteSpace(x.UniqueName)))
        {
            if (!existing.TryGetValue(item.UniqueName, out var entity))
            {
                db.Weapons.Add(item);
                existing[item.UniqueName] = item;
                continue;
            }
            entity.Name = item.Name;
            entity.Category = item.Category;
            entity.Type = item.Type;
            entity.ImageName = item.ImageName;
            entity.IsPrime = item.IsPrime;
            entity.MasteryReq = item.MasteryReq;
            entity.ComponentsJson = item.ComponentsJson;
            entity.Description = item.Description;
        }
    }

    private static async Task UpsertModsAsync(
        ApplicationDbContext db, IReadOnlyCollection<Mod> incoming, CancellationToken ct)
    {
        var existing = await db.Mods.ToDictionaryAsync(x => x.UniqueName, ct);
        foreach (var item in incoming.Where(x => !string.IsNullOrWhiteSpace(x.UniqueName)))
        {
            if (!existing.TryGetValue(item.UniqueName, out var entity))
            {
                db.Mods.Add(item);
                existing[item.UniqueName] = item;
                continue;
            }
            entity.Name = item.Name;
            entity.Category = item.Category;
            entity.CompatName = item.CompatName;
            entity.ImageName = item.ImageName;
            entity.IsAugment = item.IsAugment;
            entity.IsPrime = item.IsPrime;
            entity.Polarity = item.Polarity;
            entity.Rarity = item.Rarity;
            entity.BaseDrain = item.BaseDrain;
            entity.FusionLimit = item.FusionLimit;
            entity.Description = item.Description;
            entity.LevelStatsJson = item.LevelStatsJson;
            entity.DropsJson = item.DropsJson;
        }
    }

    private static async Task UpsertRelicsAsync(
        ApplicationDbContext db, IReadOnlyCollection<RelicImport> incoming, CancellationToken ct)
    {
        var existing = await db.Relics.ToDictionaryAsync(x => x.UniqueName, ct);
        var existingRewards = await db.RelicRewards
            .ToDictionaryAsync(x => (x.RelicUnique, x.ItemUnique), ct);

        foreach (var import in incoming.Where(x => !string.IsNullOrWhiteSpace(x.Relic.UniqueName)))
        {
            var item = import.Relic;
            if (!existing.TryGetValue(item.UniqueName, out var entity))
            {
                db.Relics.Add(item);
                existing[item.UniqueName] = item;
            }
            else
            {
                entity.Name = item.Name;
                entity.Category = item.Category;
                entity.ImageName = item.ImageName;
                entity.Vaulted = item.Vaulted;
                entity.Tradable = item.Tradable;
                entity.RewardsJson = item.RewardsJson;
                entity.DropsJson = item.DropsJson;
            }

            foreach (var reward in import.Rewards)
            {
                var key = (reward.RelicUnique, reward.ItemUnique);
                if (!existingRewards.TryGetValue(key, out var stored))
                {
                    db.RelicRewards.Add(reward);
                    existingRewards[key] = reward;
                    continue;
                }
                stored.ItemName = reward.ItemName;
                stored.Rarity = reward.Rarity;
                stored.Chance = reward.Chance;
                stored.MarketUrlName = reward.MarketUrlName;
            }
        }
    }
}
