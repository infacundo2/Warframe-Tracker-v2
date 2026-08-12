namespace WarframeTracker.Agent.Providers.EELog;

/// <summary>
/// Presenta solamente categorías seguras derivadas de EE.log. Nunca conserva ni
/// escribe el contenido original de sus líneas.
/// </summary>
public sealed class EELogActivityMonitor : BackgroundService
{
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(5);
    private readonly EELogEventStream _events;
    private readonly ILogger<EELogActivityMonitor> _logger;
    private readonly object _gate = new();
    private readonly Dictionary<EELogEventKind, DateTimeOffset> _lastReported = [];

    public EELogActivityMonitor(EELogEventStream events, ILogger<EELogActivityMonitor> logger)
        => (_events, _logger) = (events, logger);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _events.Received += OnReceived;
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Cierre normal del host.
        }
        finally
        {
            _events.Received -= OnReceived;
        }
    }

    private void OnReceived(object? sender, EELogEvent value)
    {
        lock (_gate)
        {
            if (_lastReported.TryGetValue(value.Kind, out var previous)
                && value.OccurredUtc - previous < DuplicateWindow)
                return;
            _lastReported[value.Kind] = value.OccurredUtc;
        }

        var summary = value.Kind switch
        {
            EELogEventKind.SessionStarted => "Inicio de sesión detectado",
            EELogEventKind.MissionStarted => "Inicio o transición de actividad detectada",
            EELogEventKind.MissionEnded => "Fin o salida de actividad detectada",
            EELogEventKind.HubEntered => "Entrada a Repetidor o hub detectada",
            EELogEventKind.HubExited => "Salida de Repetidor o hub detectada",
            EELogEventKind.InventoryHint => "Carga o cambio de inventario detectado",
            _ => null
        };

        if (summary is not null)
            _logger.LogInformation("[EE.log] {Summary}. No se guardó contenido original.", summary);
    }
}
