namespace WarframeInventory.Models
{
    public class Relic
    {
        public int Id { get; set; }
        public string UniqueName { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "Relics";
        public string? ImageName { get; set; }

        public bool Vaulted { get; set; }
        public bool Tradable { get; set; }

        // 🔹 Guardamos las recompensas (reliquias abren ítems)
        public string? RewardsJson { get; set; }

        public bool Owned { get; set; } = false;
    }
}
