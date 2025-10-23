namespace WarframeInventory.Models
{
    public class Mod
    {
        public int Id { get; set; }
        public string UniqueName { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Polarity { get; set; }
        public string ImageName { get; set; }
        public bool Owned { get; set; } = false;
    }
}
