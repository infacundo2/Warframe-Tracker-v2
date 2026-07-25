namespace WarframeInventory.Models;

public sealed class RelicReward
{
    public int Id { get; set; }
    public string RelicUnique { get; set; } = "";
    public string ItemUnique { get; set; } = "";
    public string ItemName { get; set; } = "";
    public string? Rarity { get; set; }
    public double Chance { get; set; }
    public string? MarketUrlName { get; set; }
}

public sealed class DataSyncState
{
    public int Id { get; set; } = 1;
    public DateTime? LastSuccessUtc { get; set; }
    public DateTime? LastAttemptUtc { get; set; }
    public string Status { get; set; } = "Never";
    public string? Error { get; set; }
}
