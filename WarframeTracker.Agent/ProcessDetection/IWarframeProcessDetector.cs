namespace WarframeTracker.Agent.ProcessDetection;

public interface IWarframeProcessDetector
{
    event EventHandler<WarframeProcessEvent>? WarframeStarted;
    event EventHandler<WarframeProcessEvent>? WarframeStopped;

    int? CurrentProcessId { get; }
}
