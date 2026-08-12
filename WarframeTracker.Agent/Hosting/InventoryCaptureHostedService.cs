using Microsoft.Extensions.Options;
using WarframeTracker.Agent.Configuration;
using WarframeTracker.Agent.Diagnostics;
using WarframeTracker.Agent.Inventory;
using WarframeTracker.Agent.Providers.Inventory;

namespace WarframeTracker.Agent.Hosting;

public sealed class InventoryCaptureHostedService : BackgroundService
{
    private readonly IInventoryProvider _provider;
    private readonly SnapshotStore _store;
    private readonly AgentStatus _status;
    private readonly TrackerAgentOptions _options;
    private readonly ILogger<InventoryCaptureHostedService> _logger;
    public InventoryCaptureHostedService(IInventoryProvider provider, SnapshotStore store,
        AgentStatus status, IOptions<TrackerAgentOptions> options,
        ILogger<InventoryCaptureHostedService> logger)
        => (_provider, _store, _status, _options, _logger)
            = (provider, store, status, options.Value, logger);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.InventoryProviderEnabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_status.Current.State == AgentState.WarframeRunning)
                await CaptureAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
    private async Task CaptureAsync(CancellationToken ct)
    {
        var result = await _provider.CaptureAsync(ct);
        if (!result.Success || result.Snapshot is null) return;
        var previous = await _store.ReadLatestAsync(ct);
        var candidate = InventorySnapshotTools.Normalize(result.Snapshot with
        {
            BatchId = Guid.NewGuid(), Sequence = (previous?.Sequence ?? 0) + 1,
            CapturedUtc = DateTime.UtcNow
        });
        if (previous?.ContentHash == candidate.ContentHash) return;
        var differences = InventorySnapshotTools.Compare(previous, candidate);
        await _store.SaveLatestAsync(candidate, ct);
        await _store.EnqueueAsync(candidate, ct);
        _logger.LogInformation("[Inventario] Snapshot {Sequence} preparado: {Changes} cambios, {Items} objetos.",
            candidate.Sequence, differences.Count, candidate.Items.Count);
    }
}
