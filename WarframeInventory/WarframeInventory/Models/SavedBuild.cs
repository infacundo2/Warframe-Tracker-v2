namespace WarframeInventory.Models;

public sealed class SavedBuild
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string Name { get; set; } = "";
    public string TargetType { get; set; } = "warframe";
    public string TargetUnique { get; set; } = "";
    public string TargetName { get; set; } = "";
    public string Tags { get; set; } = "";
    public string ModsJson { get; set; } = "[]";
    public int Capacity { get; set; } = 30;
    public int FormaCount { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
