public class UserMod
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string ModUnique { get; set; } = "";
    public bool Owned { get; set; }

    // Nuevo:
    public int Quantity { get; set; } = 0;
}
