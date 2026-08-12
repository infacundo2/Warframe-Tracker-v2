using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WarframeInventory.Data;
using WarframeInventory.Models;
using WarframeInventory.Services;
using Xunit;

namespace WarframeInventory.Tests;

public sealed class FarmPlannerServiceTests
{
    [Fact]
    public async Task Finds_reward_routes_and_owned_refinements_without_component_drop_text()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"warframe-planner-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<DesktopApplicationDbContext>()
                .UseSqlite($"Data Source={databasePath}").Options;
            var factory = new TestFactory(options);
            int goalId;

            await using (var setup = new DesktopApplicationDbContext(options))
            {
                await setup.Database.MigrateAsync();
                setup.Users.Add(new IdentityUser { Id = "planner-user", UserName = "planner" });
                setup.Warframes.Add(new Warframe
                {
                    UniqueName = "/Lotus/Powersuits/RevenantPrime",
                    Name = "Revenant Prime",
                    ComponentsJson = JsonSerializer.Serialize(new[]
                    {
                        new WarframeComponent { Name = "Blueprint" },
                        new WarframeComponent { Name = "Chassis" }
                    })
                });
                var goal = new UserGoal
                {
                    UserId = "planner-user", TargetType = "warframe",
                    TargetUnique = "/Lotus/Powersuits/RevenantPrime", DisplayName = "Revenant Prime"
                };
                setup.UserGoals.Add(goal);

                foreach (var (suffix, label, chance) in new[]
                         {
                             ("Bronze", "Intact", 2d), ("Silver", "Exceptional", 4d),
                             ("Gold", "Flawless", 6d), ("Platinum", "Radiant", 10d)
                         })
                {
                    var relicUnique = $"/Lotus/Types/Game/Projections/MesoR1{suffix}";
                    setup.Relics.Add(new Relic
                    {
                        UniqueName = relicUnique, Name = $"Meso R1 {label}", Vaulted = false
                    });
                    setup.RelicRewards.Add(new RelicReward
                    {
                        RelicUnique = relicUnique,
                        ItemUnique = "market:revenant_prime_blueprint",
                        ItemName = "Revenant Prime Blueprint",
                        Rarity = "rare",
                        Chance = chance
                    });
                    if (suffix == "Platinum")
                        setup.UserRelics.Add(new UserRelic
                        {
                            UserId = "planner-user", RelicUnique = relicUnique, Quantity = 2
                        });
                }

                await setup.SaveChangesAsync();
                goalId = goal.Id;
            }

            var plan = await new FarmPlannerService(factory)
                .BuildAsync("planner-user", goalId, "balanced");

            Assert.NotNull(plan);
            var route = Assert.Single(plan!.Routes);
            Assert.Equal("Blueprint", route.ComponentName);
            Assert.Equal("Reliquia Meso R1", route.RelicName);
            Assert.Equal(2, route.Owned["Radiante"]);
            Assert.Equal(2, route.TotalOwned);
            Assert.Equal(10, route.Chances["Radiante"]);
            Assert.Equal("Radiante", route.RecommendedRefinement);
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
