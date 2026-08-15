using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WarframeInventory.Data;
using WarframeInventory.Models;
using WarframeInventory.Services;
using Xunit;

namespace WarframeInventory.Tests;

public sealed class UniversalSearchServiceTests
{
    [Fact]
    public async Task Searches_all_catalog_sections_without_hanging()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"warframe-search-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<DesktopApplicationDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;
            var factory = new TestFactory(options);

            await using (var setup = new DesktopApplicationDbContext(options))
            {
                await setup.Database.MigrateAsync();
                setup.Warframes.Add(new Warframe
                {
                    UniqueName = "/Lotus/Warframes/RevenantPrime",
                    Name = "Revenant Prime"
                });
                setup.Weapons.Add(new Weapon
                {
                    UniqueName = "/Lotus/Weapons/RevenantTest",
                    Name = "Revenant Test Weapon"
                });
                setup.Mods.Add(new Mod
                {
                    UniqueName = "/Lotus/Mods/RevenantTest",
                    Name = "Revenant Test Mod"
                });
                setup.Relics.Add(new Relic
                {
                    UniqueName = "/Lotus/Relics/RevenantTest",
                    Name = "Revenant Test Relic"
                });
                setup.RelicRewards.Add(new RelicReward
                {
                    RelicUnique = "/Lotus/Relics/RevenantTest",
                    ItemUnique = "/Lotus/Items/RevenantTest",
                    ItemName = "Revenant Prime Blueprint"
                });
                await setup.SaveChangesAsync();
            }

            var service = new UniversalSearchService(factory);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var results = await service.SearchAsync("revenant", "all", ct: timeout.Token);

            Assert.Contains(results, x => x.Category == "warframe");
            Assert.Contains(results, x => x.Category == "weapon");
            Assert.Contains(results, x => x.Category == "mod");
            Assert.Contains(results, x => x.Category == "relic");
            Assert.Contains(results, x => x.Category == "component");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    private sealed class TestFactory(DbContextOptions<DesktopApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new DesktopApplicationDbContext(options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
