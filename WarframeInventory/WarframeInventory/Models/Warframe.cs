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
    }

    // Estas clases sirven para deserializar el JSON
    public class WarframeComponent
    {
        public string Name { get; set; } = string.Empty;
        public string? ImageName { get; set; }
        public List<DropLocation> Drops { get; set; } = new();
    }

    public class DropLocation
    {
        public double Chance { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Rarity { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
}
