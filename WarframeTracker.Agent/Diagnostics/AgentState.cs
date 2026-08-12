namespace WarframeTracker.Agent.Diagnostics;

public enum AgentState
{
    Starting,
    Disabled,
    WaitingForWarframe,
    WarframeRunning,
    Stopping,
    Stopped
}

public sealed record AgentStatusSnapshot(
    AgentState State,
    int? WarframeProcessId,
    DateTimeOffset ChangedUtc);
