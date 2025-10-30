// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text.Json;
// using System.Text.Json.Serialization;
// using System.Threading.Tasks;
// using Microsoft.AspNetCore.Components;
// using Microsoft.EntityFrameworkCore;
// using WarframeInventory.Data;
// using WarframeInventory.Models;

// namespace WarframeInventory.Pages
// {

//     public partial class ModDetail : ComponentBase
//     {
//         [Parameter] public string? uniqueName { get; set; }

//         [Inject] private ApplicationDbContext Db { get; set; } = default!;

//         private Mod? mod;
//         private List<ModLevel>? levelStats;
//         private List<ModDrop>? drops;
//         private bool loading = true;

//         protected override async Task OnInitializedAsync()
//         {
//             try
//             {
//                 if (string.IsNullOrWhiteSpace(uniqueName))
//                     return;

//                 var decodedName = Uri.UnescapeDataString(uniqueName);
//                 mod = await Db.Mods.AsNoTracking().FirstOrDefaultAsync(m => m.UniqueName == decodedName);

//                 // --- NIVELES ---
//                 if (mod != null && !string.IsNullOrWhiteSpace(mod.LevelStatsJson))
//                 {
//                     try
//                     {
//                         var doc = JsonDocument.Parse(mod.LevelStatsJson);
//                         levelStats = new();

//                         int lvl = 0;
//                         foreach (var lvlEntry in doc.RootElement.EnumerateArray())
//                         {
//                             lvl++;
//                             var desc = lvlEntry.TryGetProperty("stats", out var s)
//                                 ? string.Join(", ", s.EnumerateArray().Select(e => e.GetString()))
//                                 : "";
//                             levelStats.Add(new ModLevel { Level = lvl, Stats = desc });
//                         }
//                     }
//                     catch (Exception ex)
//                     {
//                         Console.WriteLine($"Error deserializando niveles del mod: {ex.Message}");
//                     }
//                 }

//                 // --- DROPS ---
//                 if (mod != null && !string.IsNullOrWhiteSpace(mod.DropsJson))
//                 {
//                     try
//                     {
//                         drops = JsonSerializer.Deserialize<List<ModDrop>>(mod.DropsJson,
//                             new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

//                         Console.WriteLine($"✅ {drops?.Count} ubicaciones cargadas para {mod.Name}");
//                     }
//                     catch (Exception ex)
//                     {
//                         Console.WriteLine($"Error deserializando drops del mod: {ex.Message}");
//                     }
//                 }
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine($"Error cargando detalle del mod: {ex.Message}");
//             }

//             loading = false;
//         }

//         private static string GetImageUrl(string? imageName)
//             => string.IsNullOrWhiteSpace(imageName)
//                 ? "_content/MudBlazor/images/placeholder.png"
//                 : $"https://cdn.warframestat.us/img/{imageName}";

//         private class ModLevel
//         {
//             public int Level { get; set; }
//             public string Stats { get; set; } = "";
//         }

//         private class ModDrop
//         {
//             [JsonPropertyName("location")]
//             public string Location { get; set; } = "";

//             [JsonPropertyName("rarity")]
//             public string Rarity { get; set; } = "";

//             [JsonPropertyName("type")]
//             public string Type { get; set; } = "";

//             [JsonPropertyName("chance")]
//             public double Chance { get; set; }
//         }
//     }
// }
