namespace WarframeInventory.Models;

public sealed class RelicOpening
{
    public long Id { get; set; }
    public string UserId { get; set; } = "";
    public string RelicName { get; set; } = "";
    public string RelicUnique { get; set; } = "";
    public string Refinement { get; set; } = "Intacta";
    public string RewardUnique { get; set; } = "";
    public string RewardName { get; set; } = "";
    public DateTime OpenedUtc { get; set; } = DateTime.UtcNow;
}
