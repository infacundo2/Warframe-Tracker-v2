using WarframeTracker.Agent.Inventory;

namespace WarframeTracker.Agent.Providers.Inventory;

public sealed record InventoryProviderResult(bool Success, InventorySnapshot? Snapshot,
    string Status, bool Retryable);

public interface IInventoryProvider
{
    string Name { get; }
    Task<InventoryProviderResult> CaptureAsync(CancellationToken ct);
}
