using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WarframeTracker.Agent.Inventory;

public sealed record InventoryEntry(string Section, string UniqueName, int Quantity,
    long? Experience = null);
public sealed record InventoryAccount(long? Credits = null, long? Endo = null,
    long? Platinum = null, long? Ducats = null, long? Aya = null, int? MasteryRank = null);
public sealed record InventorySnapshot(Guid BatchId, long Sequence, DateTime CapturedUtc,
    bool IsAuthoritative, string ContentHash, IReadOnlyList<InventoryEntry> Items,
    InventoryAccount? Account = null);
public sealed record InventoryDifference(string Kind, string Section, string UniqueName,
    int PreviousQuantity, int NewQuantity);

public static class InventorySnapshotTools
{
    public static bool HasSameContent(InventorySnapshot? left, InventorySnapshot right)
    {
        if (left is null) return false;
        var normalizedLeft = Normalize(left);
        var normalizedRight = Normalize(right);
        return normalizedLeft.IsAuthoritative == normalizedRight.IsAuthoritative
               && Equals(normalizedLeft.Account, normalizedRight.Account)
               && normalizedLeft.Items.SequenceEqual(normalizedRight.Items);
    }

    public static InventorySnapshot Normalize(InventorySnapshot snapshot)
    {
        var items = snapshot.Items
            .Where(x => !string.IsNullOrWhiteSpace(x.Section)
                        && !string.IsNullOrWhiteSpace(x.UniqueName))
            .GroupBy(x => (Section: x.Section.Trim(), Unique: x.UniqueName.Trim()))
            .Select(x => new InventoryEntry(x.Key.Section, x.Key.Unique,
                Math.Clamp(x.Sum(y => Math.Max(0, y.Quantity)), 0, 1_000_000),
                x.Max(y => y.Experience)))
            .OrderBy(x => x.Section, StringComparer.Ordinal)
            .ThenBy(x => x.UniqueName, StringComparer.Ordinal).ToArray();
        var canonical = JsonSerializer.Serialize(new
        {
            snapshot.Sequence, CapturedUtc = snapshot.CapturedUtc.ToUniversalTime(),
            snapshot.IsAuthoritative, Items = items, snapshot.Account
        });
        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return snapshot with { Items = items, ContentHash = hash };
    }

    public static IReadOnlyList<InventoryDifference> Compare(
        InventorySnapshot? previous, InventorySnapshot current)
    {
        var before = previous?.Items.ToDictionary(x => $"{x.Section}\0{x.UniqueName}",
            x => x, StringComparer.Ordinal) ?? new Dictionary<string, InventoryEntry>();
        var after = current.Items.ToDictionary(x => $"{x.Section}\0{x.UniqueName}",
            x => x, StringComparer.Ordinal);
        var result = new List<InventoryDifference>();
        foreach (var item in after.Values)
        {
            var key = $"{item.Section}\0{item.UniqueName}";
            var old = before.GetValueOrDefault(key)?.Quantity ?? 0;
            if (old != item.Quantity)
                result.Add(new(old == 0 ? "Added" : "Changed", item.Section,
                    item.UniqueName, old, item.Quantity));
        }
        if (current.IsAuthoritative)
        {
            foreach (var item in before.Values.Where(x => !after.ContainsKey($"{x.Section}\0{x.UniqueName}")))
                result.Add(new("Removed", item.Section, item.UniqueName, item.Quantity, 0));
        }
        return result;
    }
}
