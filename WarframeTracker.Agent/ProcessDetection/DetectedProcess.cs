namespace WarframeTracker.Agent.ProcessDetection;

public sealed record DetectedProcess(int ProcessId);

public sealed record WarframeProcessEvent(
    int ProcessId,
    DateTimeOffset DetectedUtc);
