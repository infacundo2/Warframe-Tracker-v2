namespace WarframeTracker.Agent.Configuration;

public sealed class TrackerAgentOptions
{
    public const string SectionName = "TrackerAgent";

    public bool Enabled { get; init; } = true;
    public string ProcessName { get; init; } = "Warframe.x64.exe";
    public int ProcessDetectionIntervalSeconds { get; init; } = 5;
    public string ServerBaseUrl { get; init; } = "https://warframe-inventory.onrender.com";
    public string DeviceName { get; init; } = Environment.MachineName;
    public string? DataDirectory { get; init; }
    public string? EELogPath { get; init; }
    public int EELogPollSeconds { get; init; } = 3;
    public int MaximumOfflineBatches { get; init; } = 25;
    public int MaximumSnapshotAgeHours { get; init; } = 24;

    // Reservadas para las fases siguientes. Permanecen apagadas en el skeleton.
    public bool InventoryProviderEnabled { get; init; }
    public bool EELogProviderEnabled { get; init; }
    public bool ExperimentalProviderEnabled { get; init; }
    public bool AutomaticSyncEnabled { get; init; }
}
