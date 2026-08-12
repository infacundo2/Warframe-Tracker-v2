using System.Text.Json;
using Microsoft.Extensions.Options;
using WarframeTracker.Agent.Configuration;
using WarframeTracker.Agent.Inventory;

namespace WarframeTracker.Agent.Providers.Inventory;

// Replaceable, read-only boundary for a future approved source. It never reads
// Warframe memory or traffic. QA may place a normalized snapshot in the inbox.
public sealed class SafeInboxInventoryProvider : IInventoryProvider
{
    private readonly string _path;
    private readonly TrackerAgentOptions _options;
    public string Name => "safe-inbox";
    public SafeInboxInventoryProvider(IOptions<TrackerAgentOptions> options)
    {
        _options = options.Value;
        var root = string.IsNullOrWhiteSpace(_options.DataDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WarframeTracker", "Agent") : Path.GetFullPath(_options.DataDirectory);
        Directory.CreateDirectory(root);
        _path = Path.Combine(root, "inventory-inbox.json");
    }
    public async Task<InventoryProviderResult> CaptureAsync(CancellationToken ct)
    {
        if (!_options.InventoryProviderEnabled || !_options.ExperimentalProviderEnabled)
            return new(false, null, "disabled", false);
        if (!File.Exists(_path)) return new(false, null, "waiting_for_source", true);
        try
        {
            await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read,
                FileShare.Read, 16 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length > 20 * 1024 * 1024)
                return new(false, null, "payload_too_large", false);
            var snapshot = await JsonSerializer.DeserializeAsync<InventorySnapshot>(stream,
                cancellationToken: ct);
            return snapshot is null || snapshot.Items.Count == 0
                ? new(false, null, "empty", false)
                : new(true, InventorySnapshotTools.Normalize(snapshot), "captured", false);
        }
        catch (JsonException) { return new(false, null, "invalid_json", false); }
        catch (IOException) { return new(false, null, "temporarily_unavailable", true); }
    }
}
