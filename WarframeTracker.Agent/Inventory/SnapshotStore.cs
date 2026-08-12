using System.Text.Json;
using Microsoft.Extensions.Options;
using WarframeTracker.Agent.Configuration;

namespace WarframeTracker.Agent.Inventory;

public sealed class SnapshotStore
{
    private readonly string _snapshotPath;
    private readonly string _queueDirectory;
    private readonly int _limit;
    public SnapshotStore(IOptions<TrackerAgentOptions> options)
    {
        var root = string.IsNullOrWhiteSpace(options.Value.DataDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WarframeTracker", "Agent") : Path.GetFullPath(options.Value.DataDirectory);
        Directory.CreateDirectory(root);
        _queueDirectory = Path.Combine(root, "queue");
        Directory.CreateDirectory(_queueDirectory);
        _snapshotPath = Path.Combine(root, "last-snapshot.json");
        _limit = options.Value.MaximumOfflineBatches;
    }
    public Task<InventorySnapshot?> ReadLatestAsync(CancellationToken ct)
        => ReadAsync(_snapshotPath, ct);
    public async Task SaveLatestAsync(InventorySnapshot value, CancellationToken ct)
        => await WriteAtomicAsync(_snapshotPath, value, ct);
    public async Task EnqueueAsync(InventorySnapshot value, CancellationToken ct)
    {
        await WriteAtomicAsync(Path.Combine(_queueDirectory,
            $"{value.Sequence:D20}-{value.BatchId:N}.json"), value, ct);
        foreach (var stale in Directory.EnumerateFiles(_queueDirectory, "*.json")
                     .OrderByDescending(x => x).Skip(_limit)) File.Delete(stale);
    }
    public async Task<IReadOnlyList<(string Path, InventorySnapshot Snapshot)>> ReadQueueAsync(CancellationToken ct)
    {
        var result = new List<(string, InventorySnapshot)>();
        foreach (var path in Directory.EnumerateFiles(_queueDirectory, "*.json").OrderBy(x => x))
        {
            var value = await ReadAsync(path, ct);
            if (value is not null) result.Add((path, value));
        }
        return result;
    }
    public void RemoveQueued(string path)
    {
        var full = Path.GetFullPath(path);
        if (Path.GetDirectoryName(full) == Path.GetFullPath(_queueDirectory) && File.Exists(full))
            File.Delete(full);
    }
    private static async Task<InventorySnapshot?> ReadAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<InventorySnapshot>(stream, cancellationToken: ct);
    }
    private static async Task WriteAtomicAsync(string path, InventorySnapshot value, CancellationToken ct)
    {
        var temporary = path + ".tmp";
        await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write,
                         FileShare.None, 16 * 1024, FileOptions.WriteThrough | FileOptions.Asynchronous))
            await JsonSerializer.SerializeAsync(stream, value, cancellationToken: ct);
        File.Move(temporary, path, true);
    }
}
