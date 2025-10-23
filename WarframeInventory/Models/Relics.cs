namespace WarframeInventory.Models
{
    public class Relic
    {
        public int Id { get; set; }
        public string UniqueName { get; set; }
        public string Name { get; set; }
        public string Tier { get; set; }
        public string State { get; set; }
        public bool Owned { get; set; } = false;
    }
}
