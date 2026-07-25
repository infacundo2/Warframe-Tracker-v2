namespace WarframeInventory.Models;

public sealed class InventoryMetadata
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string Category { get; set; } = "";
    public string TargetUnique { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Tags { get; set; } = "";
    public DateTime? AcquiredUtc { get; set; }
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
