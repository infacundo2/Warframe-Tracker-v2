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
        Assert.Equal("gaCrItU7eDdHkPO/cCAT48xTuEwVKVnRBOJlFk+2UGs=", first.ContentHash);
        Assert.Equal(32, Convert.FromBase64String(first.ContentHash).Length);
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

    [Fact]
    public void Content_comparison_ignores_envelope_but_detects_inventory_changes()
    {
        var first = Snapshot(false, new InventoryEntry("Suits", "A", 1));
        var newEnvelope = InventorySnapshotTools.Normalize(first with
        {
            BatchId = Guid.NewGuid(),
            Sequence = first.Sequence + 1,
            CapturedUtc = first.CapturedUtc.AddMinutes(1)
        });
        var changed = InventorySnapshotTools.Normalize(newEnvelope with
        {
            Items = [new InventoryEntry("Suits", "A", 2)]
        });

        Assert.NotEqual(first.ContentHash, newEnvelope.ContentHash);
        Assert.True(InventorySnapshotTools.HasSameContent(first, newEnvelope));
        Assert.False(InventorySnapshotTools.HasSameContent(first, changed));
    }

    [Fact]
    public void Content_comparison_detects_account_and_authority_changes()
    {
        var first = Snapshot(false, new InventoryEntry("Suits", "A", 1));
        Assert.False(InventorySnapshotTools.HasSameContent(first,
            first with { Account = new InventoryAccount(Credits: 10) }));
        Assert.False(InventorySnapshotTools.HasSameContent(first,
            first with { IsAuthoritative = true }));
    }
    private static InventorySnapshot Snapshot(bool authoritative, params InventoryEntry[] items)
        => InventorySnapshotTools.Normalize(new(Guid.NewGuid(), 1, DateTime.UtcNow,
            authoritative, "", items));
}
