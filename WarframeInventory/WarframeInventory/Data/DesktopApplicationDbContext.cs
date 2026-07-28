using Microsoft.EntityFrameworkCore;

namespace WarframeInventory.Data;

public sealed class DesktopApplicationDbContext : ApplicationDbContext
{
    public DesktopApplicationDbContext(
        DbContextOptions<DesktopApplicationDbContext> options)
        : base(options)
    {
    }
}

public sealed class DesktopDbContextFactoryAdapter
    : IDbContextFactory<ApplicationDbContext>
{
    private readonly IDbContextFactory<DesktopApplicationDbContext> _factory;

    public DesktopDbContextFactoryAdapter(
        IDbContextFactory<DesktopApplicationDbContext> factory)
    {
        _factory = factory;
    }

    public ApplicationDbContext CreateDbContext()
        => _factory.CreateDbContext();

    public async Task<ApplicationDbContext> CreateDbContextAsync(
        CancellationToken cancellationToken = default)
        => await _factory.CreateDbContextAsync(cancellationToken);
}
