using System.Text.Json;

namespace WarframeInventory.Models
{
    public class Weapon
    {
        public int Id { get; set; }
        public string UniqueName { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "Weapons";
        public string? Type { get; set; }   // rifle, shotgun, melee, etc
        public string? ImageName { get; set; }
        public bool IsPrime { get; set; }
        public int? MasteryReq { get; set; }

        // La API trae "components" con piezas; lo guardamos como JSON.
        public string? ComponentsJson { get; set; }

        // Algunas armas traen "description"
        public string? Description { get; set; }

        public bool Owned { get; set; } = false;

        public static string? ToJson(object? value)
            => value == null ? null : JsonSerializer.Serialize(value);
    }
}
