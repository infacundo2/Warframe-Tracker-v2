namespace WarframeTracker.Agent.Configuration;

public sealed class TrackerAgentOptions
{
    public const string SectionName = "TrackerAgent";

    public bool Enabled { get; init; } = true;
    public string ProcessName { get; init; } = "Warframe.x64.exe";
    public int ProcessDetectionIntervalSeconds { get; init; } = 5;

    // Reservadas para las fases siguientes. Permanecen apagadas en el skeleton.
    public bool InventoryProviderEnabled { get; init; }
    public bool EELogProviderEnabled { get; init; }
    public bool ExperimentalProviderEnabled { get; init; }
    public bool AutomaticSyncEnabled { get; init; }
}
