using Microsoft.Extensions.Options;
using WarframeTracker.Agent.Configuration;
using WarframeTracker.Agent.Inventory;
using WarframeTracker.Agent.Security;
using WarframeTracker.Agent.Sync;

namespace WarframeTracker.Agent.Hosting;

public sealed class InventorySyncHostedService : BackgroundService
{
    private readonly TrackerAgentOptions _options;
    private readonly SnapshotStore _store;
    private readonly IAgentCredentialStore _credentials;
    private readonly AgentApiClient _api;
    private readonly ILogger<InventorySyncHostedService> _logger;
    public InventorySyncHostedService(IOptions<TrackerAgentOptions> options, SnapshotStore store,
        IAgentCredentialStore credentials, AgentApiClient api,
        ILogger<InventorySyncHostedService> logger)
        => (_options, _store, _credentials, _api, _logger)
            = (options.Value, store, credentials, api, logger);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.AutomaticSyncEnabled) return;
        var attempt = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            var token = await _credentials.ReadTokenAsync(stoppingToken);
            if (string.IsNullOrWhiteSpace(token))
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                continue;
            }
            var queue = await _store.ReadQueueAsync(stoppingToken);
            if (queue.Count == 0)
            {
                attempt = 0;
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                continue;
            }
            try
            {
                var next = queue[0];
                if (next.Snapshot.CapturedUtc < DateTime.UtcNow.AddHours(-_options.MaximumSnapshotAgeHours))
                {
                    _store.RemoveQueued(next.Path);
                    _logger.LogWarning("[Sync] Snapshot antiguo descartado antes de enviarlo.");
                    continue;
                }
                var preview = await _api.PreviewAsync(token, next.Snapshot, stoppingToken);
                var applied = await _api.ApplyAsync(token, next.Snapshot.BatchId, stoppingToken);
                if (applied.Status is "applied" or "already_applied")
                {
                    _store.RemoveQueued(next.Path);
                    attempt = 0;
                    _logger.LogInformation("[Sync] Batch {BatchId} aplicado: {Changes} cambios.",
                        applied.BatchId, applied.ChangedRecords);
                }
            }
            catch (AgentApiException exception) when (!exception.Retryable)
            {
                _logger.LogWarning("[Sync] Batch rechazado: {Code}.", exception.Code);
                _store.RemoveQueued(queue[0].Path);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                                   || exception is AgentApiException)
            {
                attempt = Math.Min(attempt + 1, 8);
                var delay = TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, attempt))
                                                 + Random.Shared.NextDouble() * 3);
                _logger.LogWarning("[Sync] Render no disponible; reintento en {Seconds:0}s.",
                    delay.TotalSeconds);
                await Task.Delay(delay, stoppingToken);
            }
        }
    }
}
