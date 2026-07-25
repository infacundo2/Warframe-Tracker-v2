using System.ComponentModel.DataAnnotations.Schema;

namespace WarframeInventory.Models
{
    public class Warframe
    {
        public int Id { get; set; }
        public string UniqueName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageName { get; set; }
        public int Health { get; set; }
        public int Armor { get; set; }
        public bool Owned { get; set; } = false;

        // 🔹 Guardamos los componentes como JSON serializado
        public string? ComponentsJson { get; set; }

        // 🔹 Propiedades temporales (no se guardan en la base)
        [NotMapped]
        public string TempImage { get; set; } = "_content/MudBlazor/images/placeholder.png";

        [NotMapped]
        public bool Loaded { get; set; } = false;
    }

    // -----------------------------
    // Clases auxiliares del JSON
    // -----------------------------
    public class WarframeComponent
    {
        public string Name { get; set; } = string.Empty;
        public string UniqueName { get; set; } = string.Empty;
        public int ItemCount { get; set; } = 1;
        public string? ImageName { get; set; }
        public List<DropLocation> Drops { get; set; } = new();

        public List<RelicLink>? RelicLinks { get; set; }
        public bool Owned { get; set; }
        public int Quantity { get; set; }

    }

        public class RelicLink
        {
            public string Name { get; set; } = "";
            public string UniqueName { get; set; } = "";
            public bool Vaulted { get; set; }
            public int IntactQuantity { get; set; }
            public int ExceptionalQuantity { get; set; }
            public int FlawlessQuantity { get; set; }
            public int RadiantQuantity { get; set; }
            public int TotalQuantity =>
                IntactQuantity + ExceptionalQuantity + FlawlessQuantity + RadiantQuantity;
        }


    public class DropLocation
    {
        public double Chance { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Rarity { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
}
