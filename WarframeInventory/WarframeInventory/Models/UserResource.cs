namespace WarframeInventory.Models;

public sealed class UserResource
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string ResourceUnique { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Quantity { get; set; }
}
