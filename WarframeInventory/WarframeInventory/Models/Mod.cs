using System.Text.Json;
using System.Text.Json.Serialization;

namespace WarframeInventory.Models
{
    public class Mod
    {
        public int Id { get; set; }
        public string UniqueName { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "Mods";
        public string? CompatName { get; set; }
        public string? ImageName { get; set; }
        public bool IsAugment { get; set; }
        public bool IsPrime { get; set; }
        public string? Polarity { get; set; }
        public string? Rarity { get; set; }
        public int? BaseDrain { get; set; }
        public int? FusionLimit { get; set; }

        // Descripción puede venir como string o estructura. Guardamos texto plano.
        public string? Description { get; set; }

        // Nivel -> lista de textos. Lo persistimos como JSON por simplicidad.
        public string? LevelStatsJson { get; set; }

        public bool Owned { get; set; } = false;

        public static string? ToJson(object? value)
            => value == null ? null : JsonSerializer.Serialize(value);
    }
}
