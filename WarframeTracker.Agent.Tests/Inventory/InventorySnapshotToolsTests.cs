using WarframeTracker.Agent.Inventory;
using Xunit;

namespace WarframeTracker.Agent.Tests.Inventory;

public sealed class InventorySnapshotToolsTests
{
    [Fact]
    public void Normalizes_duplicates_and_produces_a_stable_hash()
    {
        var captured = new DateTime(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc);
        var source = new InventorySnapshot(Guid.NewGuid(), 4, captured, false, "",
        [
            new("Upgrades", "/Lotus/A", 2), new("Upgrades", "/Lotus/A", 3),
            new("MiscItems", "/Lotus/B", 1)
        ]);
        var first = InventorySnapshotTools.Normalize(source);
        var second = InventorySnapshotTools.Normalize(source with { BatchId = Guid.NewGuid() });
        Assert.Equal(2, first.Items.Count);
        Assert.Equal(5, first.Items.Single(x => x.UniqueName == "/Lotus/A").Quantity);
        Assert.Equal(first.ContentHash, second.ContentHash);
    }

    [Fact]
    public void Partial_snapshots_never_report_removals()
    {
        var old = Snapshot(true, new InventoryEntry("Suits", "A", 1),
            new InventoryEntry("Suits", "B", 1));
        var partial = Snapshot(false, new InventoryEntry("Suits", "A", 2));
        var authoritative = Snapshot(true, new InventoryEntry("Suits", "A", 2));
        Assert.DoesNotContain(InventorySnapshotTools.Compare(old, partial), x => x.Kind == "Removed");
        Assert.Contains(InventorySnapshotTools.Compare(old, authoritative),
            x => x.Kind == "Removed" && x.UniqueName == "B");
    }
    private static InventorySnapshot Snapshot(bool authoritative, params InventoryEntry[] items)
        => InventorySnapshotTools.Normalize(new(Guid.NewGuid(), 1, DateTime.UtcNow,
            authoritative, "", items));
}
