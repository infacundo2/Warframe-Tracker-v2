namespace WarframeTracker.Agent.Providers.EELog;

public enum EELogEventKind { SessionStarted, MissionStarted, MissionEnded, InventoryHint, Unknown }
public sealed record EELogEvent(EELogEventKind Kind, DateTimeOffset OccurredUtc);

public sealed class EELogEventStream
{
    public event EventHandler<EELogEvent>? Received;
    public void Publish(EELogEvent value) => Received?.Invoke(this, value);
}
