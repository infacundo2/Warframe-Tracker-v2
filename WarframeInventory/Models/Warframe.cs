namespace WarframeInventory.Models
{
    public class Warframe
    {
        public int Id { get; set; }
        public string UniqueName { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ImageName { get; set; }
        public int Health { get; set; }
        public int Armor { get; set; }
        public bool Owned { get; set; } = false;
    }
}
