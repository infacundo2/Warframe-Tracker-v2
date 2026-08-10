using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WarframeInventory.Data;
using WarframeInventory.Models;
using WarframeInventory.Services;
using Xunit;

namespace WarframeInventory.Tests;

public sealed class GoalAnalysisServiceTests
{
    [Fact]
    public async Task Analyzes_only_the_requested_goal_page_with_bounded_catalog_queries()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"warframe-goals-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<DesktopApplicationDbContext>()
                .UseSqlite($"Data Source={databasePath}").Options;
            var factory = new TestFactory(options);
            await using (var setup = new DesktopApplicationDbContext(options))
            {
                await setup.Database.MigrateAsync();
                setup.Users.Add(new IdentityUser { Id = "goal-user", UserName = "goals", NormalizedUserName = "GOALS" });
                for (var index = 0; index < 12; index++)
                {
                    var parent = $"/Lotus/Powersuits/Goal{index}";
                    var relicName = $"Lith G{index} Relic";
                    setup.Warframes.Add(new Warframe
                    {
                        UniqueName = parent,
                        Name = $"Goalframe {index}",
                        ComponentsJson = JsonSerializer.Serialize(new[]
                        {
                            new WarframeComponent
                            {
                                Name = $"Goalframe {index} Chassis",
                                UniqueName = $"{parent}/Chassis",
                                Drops = [new DropLocation { Location = $"{relicName} (Intact)", Chance = 11 }]
                            }
                        })
                    });
                    setup.UserGoals.Add(new UserGoal
                    {
                        UserId = "goal-user", TargetType = "warframe", TargetUnique = parent,
                        DisplayName = $"Goalframe {index}", Priority = 2
                    });
                    foreach (var suffix in new[] { "Bronze", "Silver", "Gold", "Platinum" })
                        setup.Relics.Add(new Relic
                        {
                            UniqueName = $"/Lotus/Types/Game/Projections/Goal{index}{suffix}",
                            Name = relicName
                        });
                }
                await setup.SaveChangesAsync();
            }

            List<UserGoal> page;
            await using (var db = new DesktopApplicationDbContext(options))
                page = await db.UserGoals.AsNoTracking().OrderBy(x => x.Id).Take(6).ToListAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var result = await new GoalAnalysisService(factory).AnalyzeAsync("goal-user", page, timeout.Token);

            Assert.Equal(6, result.Count);
            Assert.All(result.Values, analysis =>
            {
                Assert.Equal(1, analysis.MissingCount);
                Assert.Single(analysis.RelatedRelics);
            });
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
