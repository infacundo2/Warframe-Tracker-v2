using System.Diagnostics;

namespace WarframeTracker.Agent.ProcessDetection;

public sealed class SystemProcessQuery : IProcessQuery
{
    public IReadOnlyCollection<DetectedProcess> FindByName(string processName)
    {
        var normalizedName = NormalizeProcessName(processName);
        var processes = Process.GetProcessesByName(normalizedName);
        try
        {
            return processes
                .Select(process => new DetectedProcess(process.Id))
                .OrderBy(process => process.ProcessId)
                .ToArray();
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }
    }

    internal static string NormalizeProcessName(string processName)
    {
        var trimmed = processName.Trim();
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^4]
            : trimmed;
    }
}
