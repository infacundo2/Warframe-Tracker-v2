using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WarframeInventory.Data;
using WarframeInventory.Models;
using WarframeInventory.Services;
using Xunit;

namespace WarframeInventory.Tests;

public sealed class DesktopInventorySyncServiceTests
{
    [Fact]
    public async Task Stages_previews_and_applies_a_complete_capture()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"warframe-tracker-test-{Guid.NewGuid():N}.db");
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
                    Id = "test-user",
                    UserName = "operator",
                    NormalizedUserName = "OPERATOR",
                    Email = "operator@example.test",
                    NormalizedEmail = "OPERATOR@EXAMPLE.TEST"
                });
                setup.Warframes.Add(new Warframe
                {
                    UniqueName = "/Lotus/Powersuits/TestSuit",
                    Name = "Testframe",
                    ComponentsJson =
                        """[{"name":"Testframe Chassis","uniqueName":"/Lotus/Types/Recipes/TestChassis"}]"""
                });
                setup.Weapons.Add(new Weapon
                {
                    UniqueName = "/Lotus/Weapons/TestRifle",
                    Name = "Test Rifle"
                });
                setup.Mods.Add(new Mod
                {
                    UniqueName = "/Lotus/Upgrades/Mods/TestMod",
                    Name = "Test Mod"
                });
                setup.Relics.Add(new Relic
                {
                    UniqueName = "/Lotus/Types/Game/Projections/TestRelic",
                    Name = "Reliquia Lith T1"
                });
                await setup.SaveChangesAsync();
            }

            var service = new DesktopInventorySyncService(factory);
            var receipt = service.Stage(
                """
                {
                  "Credits": 5000,
                  "PremiumCredits": 25,
                  "FusionPoints": 700,
                  "Suits": [
                    {"ItemType":"/Lotus/Powersuits/TestSuit","ItemCount":1}
                  ],
                  "LongGuns": [
                    {"ItemType":"/Lotus/Weapons/TestRifle","ItemCount":1}
                  ],
                  "Upgrades": [
                    {"ItemType":"/Lotus/Upgrades/Mods/TestMod","ItemCount":4}
                  ],
                  "MiscItems": [
                    {"ItemType":"/Lotus/Types/Game/Projections/TestRelic","ItemCount":3},
                    {"ItemType":"/Lotus/Types/Recipes/TestChassis","ItemCount":2},
                    {"ItemType":"/Lotus/Types/Items/MiscItems/OrokinCell","ItemCount":11}
                  ]
                }
                """,
                "test");

            Assert.True(receipt.IsAuthoritative);
            Assert.Equal(6, receipt.DistinctItems);

            var preview = await service.PreviewAsync("test-user");
            Assert.Single(preview.Warframes);
            Assert.Single(preview.Weapons);
            Assert.Single(preview.Mods);
            Assert.Single(preview.Relics);
            Assert.Single(preview.Components);
            Assert.Single(preview.Resources);
            Assert.Equal(6, preview.Changes.Count);

            var result = await service.ApplyAsync("test-user", preview);
            Assert.True(result.ChangedRecords >= 6);
            Assert.False(service.GetStatus().HasCapture);

            await using var verification = new DesktopApplicationDbContext(options);
            Assert.True((await verification.UserWarframes.SingleAsync()).Owned);
            Assert.True((await verification.UserWeapons.SingleAsync()).Owned);
            Assert.Equal(4, (await verification.UserMods.SingleAsync()).Quantity);
            Assert.Equal(3, (await verification.UserRelics.SingleAsync()).Quantity);
            Assert.Equal(2, (await verification.UserComponents.SingleAsync()).Quantity);
            Assert.Equal(11, (await verification.UserResources.SingleAsync()).Quantity);
            var account = await verification.AlecaAccountSnapshots.SingleAsync();
            Assert.Equal(5000, account.Credits);
            Assert.Equal(700, account.Endo);
            Assert.Equal(25, account.Platinum);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
                File.Delete(databasePath);
        }
    }

    private sealed class TestFactory
        : IDbContextFactory<ApplicationDbContext>
    {
        private readonly DbContextOptions<DesktopApplicationDbContext> _options;

        public TestFactory(DbContextOptions<DesktopApplicationDbContext> options)
        {
            _options = options;
        }

        public ApplicationDbContext CreateDbContext()
            => new DesktopApplicationDbContext(_options);

        public Task<ApplicationDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
