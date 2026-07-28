namespace WarframeInventory.Models;

public sealed class AlecaAccountSnapshot
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public int? Platinum { get; set; }
    public long? Credits { get; set; }
    public int? Endo { get; set; }
    public int? Ducats { get; set; }
    public int? Aya { get; set; }
    public int? MasteryRank { get; set; }
    public int? CompletionPercentage { get; set; }
    public int? RelicsOpened { get; set; }
    public int? TradeCount { get; set; }
    public string? PublicUsername { get; set; }
    public int Permissions { get; set; }
    public DateTime SourceTimestampUtc { get; set; }
    public DateTime SyncedUtc { get; set; }
}
