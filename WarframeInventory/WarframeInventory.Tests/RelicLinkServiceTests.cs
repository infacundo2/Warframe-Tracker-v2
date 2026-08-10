using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WarframeInventory.Data;
using WarframeInventory.Models;
using WarframeInventory.Services;
using Xunit;

namespace WarframeInventory.Tests;

public sealed class RelicLinkServiceTests
{
    [Fact]
    public async Task Groups_refinements_and_loads_owned_quantities_in_bulk()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"warframe-relic-links-{Guid.NewGuid():N}.db");

        try
        {
            var options = new DbContextOptionsBuilder<DesktopApplicationDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;
            var factory = new TestFactory(options);

            await using (var setup = new DesktopApplicationDbContext(options))
            {
                await setup.Database.MigrateAsync();
                setup.Users.Add(new IdentityUser
                {
                    Id = "relic-user",
                    UserName = "relic-user",
                    NormalizedUserName = "RELIC-USER"
                });

                var variants = new[]
                {
                    ("Bronze", "Intact", 2),
                    ("Silver", "Exceptional", 3),
                    ("Gold", "Flawless", 4),
                    ("Platinum", "Radiant", 5)
                };
                foreach (var (suffix, label, quantity) in variants)
                {
                    var uniqueName = $"/Lotus/Types/Game/Projections/Test{suffix}";
                    setup.Relics.Add(new Relic
                    {
                        UniqueName = uniqueName,
                        Name = $"Axi T1 {label}",
                        Vaulted = true
                    });
                    setup.UserRelics.Add(new UserRelic
                    {
                        UserId = "relic-user",
                        RelicUnique = uniqueName,
                        Quantity = quantity
                    });
                }

                await setup.SaveChangesAsync();
            }

            using var cache = new MemoryCache(new MemoryCacheOptions());
            var catalogCache = new CatalogCacheService(cache, factory);
            var service = new RelicLinkService(factory, catalogCache);
            var component = new WarframeComponent
            {
                Name = "Test Chassis",
                Drops =
                [
                    new DropLocation { Location = "Axi T1 Relic (Intact)" },
                    new DropLocation { Location = "Axi T1 Relic (Exceptional)" },
                    new DropLocation { Location = "Axi T1 Relic (Flawless)" },
                    new DropLocation { Location = "Axi T1 Relic (Radiant)" }
                ]
            };

            await service.PopulateAsync([component], "relic-user", includeRelics: true);

            var link = Assert.Single(component.RelicLinks!);
            Assert.Equal("Axi T1", link.Name);
            Assert.True(link.Vaulted);
            Assert.Equal(2, link.IntactQuantity);
            Assert.Equal(3, link.ExceptionalQuantity);
            Assert.Equal(4, link.FlawlessQuantity);
            Assert.Equal(5, link.RadiantQuantity);
            Assert.Equal(14, link.TotalQuantity);
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
        public ApplicationDbContext CreateDbContext()
            => new DesktopApplicationDbContext(options);

        public Task<ApplicationDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
