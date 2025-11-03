using System.Text.Json.Serialization;

namespace WarframeInventory.Models
{
    public class ModDrop
    {
        [JsonPropertyName("chance")]
        public double Chance { get; set; }

        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [JsonPropertyName("rarity")]
        public string? Rarity { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        // Propiedad auxiliar para mostrar porcentaje formateado
        public string ChanceFormatted => $"{Chance * 100:0.##}%";
    }
}
