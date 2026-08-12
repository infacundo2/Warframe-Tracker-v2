using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WarframeInventory.Data;
using WarframeInventory.Models;
using WarframeInventory.Services;
using Xunit;

namespace WarframeInventory.Tests;

public sealed class DesktopInventorySyncServiceTests
{
    [Fact]
    public void Stage_accepts_null_optional_numeric_values_from_agent_payloads()
    {
        var options = new DbContextOptionsBuilder<DesktopApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var service = new DesktopInventorySyncService(new TestFactory(options));

        var receipt = service.Stage(
            """
            {
              "RegularCredits": null,
              "PlayerLevel": null,
              "QAOnly": [
                {"ItemType":"/QA/Probe","ItemCount":1,"XP":null}
              ]
            }
            """,
            "agent-null-regression");

        Assert.Equal(1, receipt.DistinctItems);
        Assert.Equal(1, receipt.TotalQuantity);
        Assert.False(receipt.IsAuthoritative);
    }

    [Fact]
    public async Task Apply_rolls_back_and_keeps_capture_when_commit_does_not_complete()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"warframe-tracker-rollback-{Guid.NewGuid():N}.db");
        var failure = new FailAfterSaveInterceptor();
        try
        {
            var options = new DbContextOptionsBuilder<DesktopApplicationDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .AddInterceptors(failure)
                .Options;
            var factory = new TestFactory(options);

            await using (var setup = new DesktopApplicationDbContext(options))
            {
                await setup.Database.MigrateAsync();
                setup.Users.Add(new IdentityUser
                {
                    Id = "rollback-user",
                    UserName = "rollback",
                    NormalizedUserName = "ROLLBACK"
                });
                setup.Warframes.Add(new Warframe
                {
                    UniqueName = "/Lotus/Powersuits/RollbackSuit",
                    Name = "Rollbackframe"
                });
                setup.Weapons.Add(new Weapon
                {
                    UniqueName = "/Lotus/Weapons/RollbackWeapon",
                    Name = "Rollback Weapon"
                });
                setup.Mods.Add(new Mod
                {
                    UniqueName = "/Lotus/Upgrades/Mods/RollbackMod",
                    Name = "Rollback Mod"
                });
                setup.Relics.Add(new Relic
                {
                    UniqueName = "/Lotus/Types/Game/Projections/RollbackRelic",
                    Name = "Reliquia Lith R1"
                });
                await setup.SaveChangesAsync();
            }

            var service = new DesktopInventorySyncService(factory);
            service.Stage(
                """
                {
                  "Suits": [
                    {"ItemType":"/Lotus/Powersuits/RollbackSuit","ItemCount":1}
                  ],
                  "MiscItems": [
                    {"ItemType":"/Lotus/Types/Items/MiscItems/OrokinCell","ItemCount":2}
                  ]
                }
                """,
                "rollback-test");
            var preview = await service.PreviewAsync("rollback-user");
            failure.Armed = true;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ApplyAsync("rollback-user", preview));

            Assert.True(service.GetStatus().HasCapture);
            await using var verification = new DesktopApplicationDbContext(options);
            Assert.Empty(await verification.UserWarframes.ToListAsync());
            Assert.Empty(await verification.UserResources.ToListAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
                File.Delete(databasePath);
        }
    }

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
                setup.Warframes.Add(new Warframe
                {
                    UniqueName = "/Lotus/Powersuits/ArchivedSuit",
                    Name = "Archived Testframe"
                });
                setup.Weapons.Add(new Weapon
                {
                    UniqueName = "/Lotus/Weapons/TestRifle",
                    Name = "Test Rifle"
                });
                setup.Weapons.Add(new Weapon
                {
                    UniqueName = "/Lotus/Weapons/ArchivedRifle",
                    Name = "Archived Rifle"
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
                  "RegularCredits": 5000,
                  "PremiumCredits": 25,
                  "FusionPoints": 700,
                  "PlayerLevel": 27,
                  "XPInfo": [
                    {"ItemType":"/Lotus/Powersuits/TestSuit","XP":900000},
                    {"ItemType":"/Lotus/Powersuits/ArchivedSuit","XP":900000},
                    {"ItemType":"/Lotus/Weapons/TestRifle","XP":450000},
                    {"ItemType":"/Lotus/Weapons/ArchivedRifle","XP":450000}
                  ],
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
                    {"ItemType":"/Lotus/Types/Items/MiscItems/OrokinCell","ItemCount":11},
                    {"ItemType":"/Lotus/Types/Items/MiscItems/SchismKey","ItemCount":9},
                    {"ItemType":"/Lotus/Types/Items/MiscItems/PrimeBucks","ItemCount":350}
                  ]
                }
                """,
                "test");

            Assert.True(receipt.IsAuthoritative);
            Assert.Equal(8, receipt.DistinctItems);

            var preview = await service.PreviewAsync("test-user");
            Assert.Single(preview.Warframes);
            Assert.Single(preview.Weapons);
            Assert.Equal(2, preview.MasteredWarframes.Count);
            Assert.Equal(2, preview.MasteredWeapons.Count);
            Assert.Single(preview.Mods);
            Assert.Single(preview.Relics);
            Assert.Single(preview.Components);
            Assert.Equal(3, preview.Resources.Count);
            Assert.Equal(12, preview.Changes.Count);
            Assert.Equal(9, preview.Account.Aya);
            Assert.Equal(350, preview.Account.Ducats);
            Assert.Equal(27, preview.Account.MasteryRank);

            var result = await service.ApplyAsync("test-user", preview);
            Assert.True(result.ChangedRecords >= 6);
            Assert.False(service.GetStatus().HasCapture);

            await using var verification = new DesktopApplicationDbContext(options);
            var savedWarframes = await verification.UserWarframes.ToListAsync();
            Assert.Equal(2, savedWarframes.Count);
            Assert.True(savedWarframes.Single(x => x.WarframeUnique.EndsWith("TestSuit")).Owned);
            Assert.True(savedWarframes.All(x => x.Mastered));
            Assert.False(savedWarframes.Single(x => x.WarframeUnique.EndsWith("ArchivedSuit")).Owned);
            var savedWeapons = await verification.UserWeapons.ToListAsync();
            Assert.Equal(2, savedWeapons.Count);
            Assert.True(savedWeapons.Single(x => x.WeaponUnique.EndsWith("TestRifle")).Owned);
            Assert.True(savedWeapons.All(x => x.Mastered));
            Assert.False(savedWeapons.Single(x => x.WeaponUnique.EndsWith("ArchivedRifle")).Owned);
            Assert.Equal(4, (await verification.UserMods.SingleAsync()).Quantity);
            Assert.Equal(3, (await verification.UserRelics.SingleAsync()).Quantity);
            Assert.Equal(2, (await verification.UserComponents.SingleAsync()).Quantity);
            Assert.Equal(3, await verification.UserResources.CountAsync());
            Assert.Equal(11, (await verification.UserResources.SingleAsync(
                x => x.ResourceUnique.EndsWith("OrokinCell"))).Quantity);
            var account = await verification.AlecaAccountSnapshots.SingleAsync();
            Assert.Equal(5000, account.Credits);
            Assert.Equal(700, account.Endo);
            Assert.Equal(25, account.Platinum);
            Assert.Equal(9, account.Aya);
            Assert.Equal(350, account.Ducats);
            Assert.Equal(27, account.MasteryRank);
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

    private sealed class FailAfterSaveInterceptor : SaveChangesInterceptor
    {
        public bool Armed { get; set; }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            if (Armed)
                throw new InvalidOperationException("Fallo de prueba antes del commit.");
            return ValueTask.FromResult(result);
        }
    }
}
