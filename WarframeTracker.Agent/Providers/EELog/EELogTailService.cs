using System.Text;
using Microsoft.Extensions.Options;
using WarframeTracker.Agent.Configuration;
using WarframeTracker.Agent.Diagnostics;

namespace WarframeTracker.Agent.Providers.EELog;

public sealed class EELogTailService : BackgroundService
{
    private readonly TrackerAgentOptions _options;
    private readonly AgentStatus _status;
    private readonly EELogEventStream _events;
    private readonly ILogger<EELogTailService> _logger;
    private long _offset;
    private string _partial = "";

    public EELogTailService(IOptions<TrackerAgentOptions> options, AgentStatus status,
        EELogEventStream events, ILogger<EELogTailService> logger)
        => (_options, _status, _events, _logger) = (options.Value, status, events, logger);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EELogProviderEnabled)
        {
            _logger.LogInformation("[EE.log] Proveedor desactivado.");
            return;
        }
        var path = ResolvePath();
        _logger.LogInformation("[EE.log] Lector incremental listo en {Directory}.",
            Path.GetDirectoryName(path));
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_status.Current.State == AgentState.WarframeRunning)
                await ReadNewDataAsync(path, stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(_options.EELogPollSeconds, 2, 30)),
                stoppingToken);
        }
    }

    internal async Task ReadNewDataAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return;
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 16 * 1024, FileOptions.Asynchronous);
            if (stream.Length < _offset) { _offset = 0; _partial = ""; }
            stream.Seek(_offset, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8, true, 16 * 1024, leaveOpen: true);
            var text = _partial + await reader.ReadToEndAsync(ct);
            _offset = stream.Position;
            var lines = text.Split('\n');
            _partial = text.EndsWith('\n') ? "" : lines[^1];
            var completeCount = text.EndsWith('\n') ? lines.Length : lines.Length - 1;
            for (var index = 0; index < completeCount; index++)
            {
                var kind = Classify(lines[index]);
                if (kind != EELogEventKind.Unknown)
                    _events.Publish(new EELogEvent(kind, DateTimeOffset.UtcNow));
            }
        }
        catch (IOException exception)
        {
            _logger.LogDebug("[EE.log] Archivo temporalmente no disponible: {ErrorType}",
                exception.GetType().Name);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("[EE.log] Windows denegó el acceso de lectura.");
        }
    }

    internal static EELogEventKind Classify(string line)
    {
        if (line.Contains("GameRules", StringComparison.OrdinalIgnoreCase)
            && line.Contains("mission", StringComparison.OrdinalIgnoreCase))
            return line.Contains("destroy", StringComparison.OrdinalIgnoreCase)
                || line.Contains("shutdown", StringComparison.OrdinalIgnoreCase)
                ? EELogEventKind.MissionEnded : EELogEventKind.MissionStarted;
        if (line.Contains("Inventory", StringComparison.OrdinalIgnoreCase)
            || line.Contains("Loadout", StringComparison.OrdinalIgnoreCase))
            return EELogEventKind.InventoryHint;
        if (line.Contains("Logged in", StringComparison.OrdinalIgnoreCase))
            return EELogEventKind.SessionStarted;
        return EELogEventKind.Unknown;
    }

    private string ResolvePath() => !string.IsNullOrWhiteSpace(_options.EELogPath)
        ? Path.GetFullPath(_options.EELogPath)
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Warframe", "EE.log");
}
