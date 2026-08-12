using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WarframeInventory.Data;
using WarframeInventory.Services;
using Xunit;

namespace WarframeInventory.Tests;

public sealed class AgentTokenServiceTests
{
    [Fact]
    public async Task Pairing_is_one_time_and_token_authenticates_the_approved_user()
    {
        var path = Path.Combine(Path.GetTempPath(), $"agent-pair-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<DesktopApplicationDbContext>()
                .UseSqlite($"Data Source={path}").Options;
            var factory = new Factory(options);
            await using (var db = new DesktopApplicationDbContext(options))
            {
                await db.Database.MigrateAsync();
                db.Users.Add(new IdentityUser { Id = "u1", UserName = "operator" });
                await db.SaveChangesAsync();
            }
            var service = new AgentTokenService(factory);
            var started = await service.StartPairingAsync("QA PC", "https://tracker.test", default);
            Assert.True(await service.ApproveAsync(started.Code, "u1", default));
            var exchanged = await service.ExchangeAsync(started.PairingId, started.Verifier, default);
            Assert.Equal("approved", exchanged.Status);
            Assert.NotNull(exchanged.Token);
            var device = await service.AuthenticateAsync(exchanged.Token!, default);
            Assert.Equal("u1", device!.UserId);
            Assert.Equal("consumed", (await service.ExchangeAsync(started.PairingId,
                started.Verifier, default)).Status);

            var expired = await service.StartPairingAsync("Old PC", "https://tracker.test", default);
            await using (var db = new DesktopApplicationDbContext(options))
            {
                var row = await db.AgentPairings.SingleAsync(x => x.Id == expired.PairingId);
                row.ExpiresUtc = DateTime.UtcNow.AddSeconds(-1);
                await db.SaveChangesAsync();
            }
            Assert.Equal("expired", (await service.ExchangeAsync(expired.PairingId,
                expired.Verifier, default)).Status);
            Assert.False(await service.ApproveAsync(expired.Code, "u1", default));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }
    private sealed class Factory(DbContextOptions<DesktopApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new DesktopApplicationDbContext(options);
        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken ct = default)
            => Task.FromResult(CreateDbContext());
    }
}
