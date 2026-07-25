namespace WarframeInventory.Models;

public sealed class UserGoal
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string TargetType { get; set; } = "";
    public string TargetUnique { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Priority { get; set; } = 2;
    public bool IsCompleted { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedUtc { get; set; }
}
