namespace WarframeInventory.Models;

public sealed class InventoryEvent
{
    public long Id { get; set; }
    public string UserId { get; set; } = "";
    public string Category { get; set; } = "";
    public string TargetUnique { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Action { get; set; } = "Updated";
    public int PreviousValue { get; set; }
    public int NewValue { get; set; }
    public DateTime OccurredUtc { get; set; } = DateTime.UtcNow;
    public bool IsReverted { get; set; }
}
