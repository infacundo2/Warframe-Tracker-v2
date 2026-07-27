namespace WarframeInventory.Models;

public sealed class RelicSyncProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string Provider { get; set; } = "AlecaFrame";
    public string? ProtectedToken { get; set; }
    public DateTime? LastPreviewUtc { get; set; }
    public DateTime? LastSyncUtc { get; set; }
    public int LastSourceCount { get; set; }
    public int LastMatchedCount { get; set; }
    public string LastStatus { get; set; } = "Never";
    public string? LastError { get; set; }
}
