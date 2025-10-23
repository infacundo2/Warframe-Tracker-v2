namespace WarframeInventory.Models
{
    public class Weapon
    {
        public int Id { get; set; }
        public string UniqueName { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ImageName { get; set; }
        public string Type { get; set; }
        public bool Owned { get; set; } = false;
    }
}
