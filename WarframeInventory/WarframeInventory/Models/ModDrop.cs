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

        [JsonPropertyName("method")]
        public string? Method { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        // Propiedad auxiliar para mostrar porcentaje formateado
        public string? ChanceFormatted => Chance <= 0
            ? null
            : $"{(Chance <= 1 ? Chance * 100 : Chance):0.##}%";
    }
}
