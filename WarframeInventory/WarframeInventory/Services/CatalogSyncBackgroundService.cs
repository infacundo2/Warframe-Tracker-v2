using Microsoft.EntityFrameworkCore;
using WarframeInventory.Data;

namespace WarframeInventory.Services;

public sealed class CatalogSyncBackgroundService : BackgroundService
{
    private static readonly TimeSpan SyncInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(30);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly ILogger<CatalogSyncBackgroundService> _logger;

    public CatalogSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IDbContextFactory<ApplicationDbContext> dbFactory,
        ILogger<CatalogSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        using var timer = new PeriodicTimer(CheckInterval);
        do
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync(stoppingToken);
                var lastSuccess = await db.DataSyncStates.AsNoTracking()
                    .Where(x => x.Id == 1)
                    .Select(x => x.LastSuccessUtc)
                    .FirstOrDefaultAsync(stoppingToken);

                if (lastSuccess is null || DateTime.UtcNow - lastSuccess >= SyncInterval)
                {
                    using var scope = _scopeFactory.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<DataSyncService>()
                        .SyncAllAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Scheduled catalog synchronization was deferred.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
