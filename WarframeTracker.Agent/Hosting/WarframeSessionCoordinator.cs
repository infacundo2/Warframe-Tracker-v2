using Microsoft.Extensions.Options;
using WarframeTracker.Agent.Configuration;
using WarframeTracker.Agent.Diagnostics;
using WarframeTracker.Agent.ProcessDetection;

namespace WarframeTracker.Agent.Hosting;

public sealed class WarframeSessionCoordinator : IHostedService, IDisposable
{
    private readonly object _gate = new();
    private readonly IWarframeProcessDetector _detector;
    private readonly AgentStatus _status;
    private readonly TrackerAgentOptions _options;
    private readonly ILogger<WarframeSessionCoordinator> _logger;
    private CancellationTokenSource? _sessionCancellation;

    public WarframeSessionCoordinator(
        IWarframeProcessDetector detector,
        AgentStatus status,
        IOptions<TrackerAgentOptions> options,
        ILogger<WarframeSessionCoordinator> logger)
    {
        _detector = detector;
        _status = status;
        _options = options.Value;
        _logger = logger;
    }

    public CancellationToken SessionCancellationToken
    {
        get
        {
            lock (_gate)
                return _sessionCancellation?.Token ?? CancellationToken.None;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _status.Set(AgentState.Disabled);
            _logger.LogInformation("[Agent] Desactivado por configuración.");
            return Task.CompletedTask;
        }

        _detector.WarframeStarted += OnWarframeStarted;
        _detector.WarframeStopped += OnWarframeStopped;
        _status.Set(AgentState.WaitingForWarframe);
        _logger.LogInformation("[Agent] Iniciado. Estado: esperando Warframe.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _status.Set(AgentState.Stopping);
        EndSession();
        _status.Set(AgentState.Stopped);
        _logger.LogInformation("[Agent] Detenido correctamente.");
        return Task.CompletedTask;
    }

    private void OnWarframeStarted(object? sender, WarframeProcessEvent processEvent)
    {
        lock (_gate)
        {
            _sessionCancellation?.Cancel();
            _sessionCancellation?.Dispose();
            _sessionCancellation = new CancellationTokenSource();
        }

        _status.Set(AgentState.WarframeRunning, processEvent.ProcessId);
        _logger.LogInformation(
            "[Warframe] Proceso detectado. PID {ProcessId}. Sesión iniciada.",
            processEvent.ProcessId);
    }

    private void OnWarframeStopped(object? sender, WarframeProcessEvent processEvent)
    {
        EndSession();
        _status.Set(AgentState.WaitingForWarframe);
        _logger.LogInformation(
            "[Warframe] Proceso cerrado. PID {ProcessId}. Recursos de sesión liberados.",
            processEvent.ProcessId);
    }

    private void EndSession()
    {
        CancellationTokenSource? cancellation;
        lock (_gate)
        {
            cancellation = _sessionCancellation;
            _sessionCancellation = null;
        }

        cancellation?.Cancel();
        cancellation?.Dispose();
    }

    public void Dispose()
    {
        _detector.WarframeStarted -= OnWarframeStarted;
        _detector.WarframeStopped -= OnWarframeStopped;
        EndSession();
    }
}
