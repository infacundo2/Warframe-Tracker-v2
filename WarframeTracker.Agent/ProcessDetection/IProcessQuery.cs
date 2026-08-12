namespace WarframeTracker.Agent.ProcessDetection;

public interface IProcessQuery
{
    IReadOnlyCollection<DetectedProcess> FindByName(string processName);
}
