using Microsoft.Extensions.Options;
using WarframeTracker.Agent.Configuration;

namespace WarframeTracker.Agent.ProcessDetection;

public sealed class WindowsWarframeProcessDetector : BackgroundService,
    IWarframeProcessDetector
{
    private readonly IProcessQuery _processQuery;
    private readonly TrackerAgentOptions _options;
    private readonly ILogger<WindowsWarframeProcessDetector> _logger;
    private readonly SemaphoreSlim _checkGate = new(1, 1);
    private int _currentProcessId = -1;

    public WindowsWarframeProcessDetector(
        IProcessQuery processQuery,
        IOptions<TrackerAgentOptions> options,
        ILogger<WindowsWarframeProcessDetector> logger)
    {
        _processQuery = processQuery;
        _options = options.Value;
        _logger = logger;
    }

    public event EventHandler<WarframeProcessEvent>? WarframeStarted;
    public event EventHandler<WarframeProcessEvent>? WarframeStopped;

    public int? CurrentProcessId
    {
        get
        {
            var processId = Volatile.Read(ref _currentProcessId);
            return processId < 0 ? null : processId;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("[Agent] Detección desactivada por configuración.");
            return;
        }

        _logger.LogInformation(
            "[Agent] Vigilando {ProcessName} cada {IntervalSeconds} segundos.",
            _options.ProcessName,
            _options.ProcessDetectionIntervalSeconds);

        await CheckNowAsync(stoppingToken);
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(_options.ProcessDetectionIntervalSeconds));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await CheckNowAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Apagado normal del host.
        }
    }

    public async Task CheckNowAsync(CancellationToken cancellationToken = default)
    {
        await _checkGate.WaitAsync(cancellationToken);
        try
        {
            IReadOnlyCollection<DetectedProcess> processes;
            try
            {
                processes = _processQuery.FindByName(_options.ProcessName);
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException)
            {
                _logger.LogWarning(
                    exception,
                    "[Agent] No se pudo consultar el proceso de Warframe; se conserva el estado anterior.");
                return;
            }

            var current = CurrentProcessId;
            if (current is not null && processes.Any(x => x.ProcessId == current.Value))
                return;

            if (current is not null)
            {
                Volatile.Write(ref _currentProcessId, -1);
                RaiseSafely(WarframeStopped, new WarframeProcessEvent(
                    current.Value,
                    DateTimeOffset.UtcNow));
            }

            var next = processes.OrderBy(x => x.ProcessId).FirstOrDefault();
            if (next is null)
                return;

            Volatile.Write(ref _currentProcessId, next.ProcessId);
            RaiseSafely(WarframeStarted, new WarframeProcessEvent(
                next.ProcessId,
                DateTimeOffset.UtcNow));
        }
        finally
        {
            _checkGate.Release();
        }
    }

    private void RaiseSafely(
        EventHandler<WarframeProcessEvent>? handlers,
        WarframeProcessEvent processEvent)
    {
        if (handlers is null)
            return;

        foreach (EventHandler<WarframeProcessEvent> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, processEvent);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "[Agent] Un consumidor falló al procesar el cambio de estado de Warframe.");
            }
        }
    }

    public override void Dispose()
    {
        _checkGate.Dispose();
        base.Dispose();
    }
}
