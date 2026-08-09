using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WarframeInventory.Data;
using WarframeInventory.Services;
using Xunit;

namespace WarframeInventory.Tests;

public sealed class NativeInventorySyncServiceTests
{
    [Fact]
    public void Captures_are_isolated_per_authenticated_user()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new NativeInventorySyncService(cache, new UnusedFactory());

        service.Stage("operator-a",
            """{"MiscItems":[{"ItemType":"/Lotus/A","ItemCount":1}]}""",
            "native-test");
        service.Stage("operator-b",
            """{"MiscItems":[{"ItemType":"/Lotus/B","ItemCount":1},{"ItemType":"/Lotus/C","ItemCount":2}]}""",
            "native-test");

        Assert.Equal(1, service.GetStatus("operator-a").DistinctItems);
        Assert.Equal(2, service.GetStatus("operator-b").DistinctItems);
        Assert.NotEqual(
            service.GetStatus("operator-a").CaptureId,
            service.GetStatus("operator-b").CaptureId);
    }

    private sealed class UnusedFactory : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext()
            => throw new NotSupportedException();

        public Task<ApplicationDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
