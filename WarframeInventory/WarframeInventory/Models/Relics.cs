using System.Text.Json;

namespace WarframeInventory.Models
{
    public class Relic
    {
        public int Id { get; set; }
        public string UniqueName { get; set; } = "";
        public string Name { get; set; } = "";      // p.ej. "Reliquia Meso A5"
        public string Category { get; set; } = "Relics";
        public string? ImageName { get; set; }      // p.ej. "meso-radiant.png"
        public bool Vaulted { get; set; }
        public bool Tradable { get; set; }

        // Recompensas de la reliquia (rarity, chance, item{name, uniqueName})
        public string? RewardsJson { get; set; }

        public bool Owned { get; set; } = false;

        public static string? ToJson(object? value)
            => value == null ? null : JsonSerializer.Serialize(value);
    }
}
