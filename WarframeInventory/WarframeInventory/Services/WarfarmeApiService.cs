using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using WarframeInventory.Models;

namespace WarframeInventory.Services
{
    public class WarframeApiService
    {
        private readonly HttpClient _http;

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public WarframeApiService(HttpClient http) => _http = http;

        // -------------------------------
        // 🔹 Helper: convertir descripción o estructuras a texto
        // -------------------------------
        private static string? AsStringFlexible(JsonNode? node)
        {
            if (node is null) return null;
            if (node is JsonValue v && v.TryGetValue<string>(out var s)) return s;
            return node.ToJsonString();
        }

        private static string? ToJson(object? o) => o == null ? null : JsonSerializer.Serialize(o);

        // -------------------------------
        // 🔹 WARFRAMES (con componentes serializados)
        // -------------------------------
        public async Task<List<Warframe>> GetWarframesAsync()
        {
            var url = "https://api.warframestat.us/warframes/?language=es";
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();

            var array = await resp.Content.ReadFromJsonAsync<JsonArray>(_jsonOpts) ?? [];
            var list = new List<Warframe>();

            foreach (var n in array.OfType<JsonObject>())
            {
                // Parsear componentes
                var comps = n["components"];
                string? compsJson = null;

                if (comps is JsonArray compArray)
                {
                    var parsed = compArray
                        .OfType<JsonObject>()
                        .Select(c => new WarframeComponent
                        {
                            Name = c["name"]?.GetValue<string>() ?? "",
                            ImageName = c["imageName"]?.GetValue<string>(),
                            Drops = c["drops"] is JsonArray drops
                                ? drops.OfType<JsonObject>().Select(d => new DropLocation
                                {
                                    Chance = d["chance"]?.GetValue<double?>() ?? 0,
                                    Location = d["location"]?.GetValue<string>() ?? "",
                                    Rarity = d["rarity"]?.GetValue<string>() ?? "",
                                    Type = d["type"]?.GetValue<string>() ?? ""
                                }).ToList()
                                : new List<DropLocation>()
                        }).ToList();

                    compsJson = JsonSerializer.Serialize(parsed);
                }

                // Crear Warframe
                list.Add(new Warframe
                {
                    UniqueName = n["uniqueName"]?.GetValue<string?>() ?? "",
                    Name = n["name"]?.GetValue<string?>() ?? "",
                    Description = AsStringFlexible(n["description"]),
                    ImageName = n["imageName"]?.GetValue<string?>(),
                    Health = n["health"]?.GetValue<int?>() ?? 0,
                    Armor = n["armor"]?.GetValue<int?>() ?? 0,
                    ComponentsJson = compsJson,
                    Owned = false
                });
            }

            return list;
        }

        // -------------------------------
        // 🔹 MODS
        // -------------------------------
        public async Task<List<Mod>> GetModsAsync()
        {
            var url = "https://api.warframestat.us/mods/?language=es";
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();

            var array = await resp.Content.ReadFromJsonAsync<JsonArray>(_jsonOpts) ?? [];
            var list = new List<Mod>();

            foreach (var n in array.OfType<JsonObject>())
            {
                var levelStats = n["levelStats"];
                string? levelStatsJson = levelStats == null ? null : levelStats.ToJsonString();
                string? dropsJson = n["drops"]?.ToJsonString(); // 🔹 nuevo

                var descText = AsStringFlexible(n["description"]);

                list.Add(new Mod
                {
                    UniqueName   = n["uniqueName"]?.GetValue<string?>() ?? "",
                    Name         = n["name"]?.GetValue<string?>() ?? "",
                    Category     = n["category"]?.GetValue<string?>() ?? "Mods",
                    CompatName   = n["compatName"]?.GetValue<string?>(),
                    ImageName    = n["imageName"]?.GetValue<string?>(),
                    IsAugment    = n["isAugment"]?.GetValue<bool?>() ?? false,
                    IsPrime      = n["isPrime"]?.GetValue<bool?>() ?? false,
                    Polarity     = n["polarity"]?.GetValue<string?>(),
                    Rarity       = n["rarity"]?.GetValue<string?>(),
                    BaseDrain    = n["baseDrain"]?.GetValue<int?>(),
                    FusionLimit  = n["fusionLimit"]?.GetValue<int?>(),
                    Description  = descText,
                    LevelStatsJson = levelStatsJson,
                    DropsJson    = dropsJson, // 🔹 guardamos drops
                    Owned = false
                });
            }
            return list;
        }


        // -------------------------------
        // 🔹 WEAPONS
        // -------------------------------
        public async Task<List<Weapon>> GetWeaponsAsync()
        {
            var url = "https://api.warframestat.us/weapons/?language=es";
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();

            var array = await resp.Content.ReadFromJsonAsync<JsonArray>(_jsonOpts) ?? [];
            var list = new List<Weapon>();

            foreach (var n in array.OfType<JsonObject>())
            {
                var comps = n["components"];
                list.Add(new Weapon
                {
                    UniqueName = n["uniqueName"]?.GetValue<string?>() ?? "",
                    Name = n["name"]?.GetValue<string?>() ?? "",
                    Category = n["category"]?.GetValue<string?>() ?? "Weapons",
                    Type = n["type"]?.GetValue<string?>(),
                    ImageName = n["imageName"]?.GetValue<string?>(),
                    IsPrime = n["isPrime"]?.GetValue<bool?>() ?? false,
                    MasteryReq = n["masteryReq"]?.GetValue<int?>(),
                    ComponentsJson = comps?.ToJsonString(),
                    Description = n["description"]?.GetValue<string?>(),
                    Owned = false
                });
            }
            return list;
        }

        // -------------------------------
        // 🔹 RELICS
        // -------------------------------
         public async Task<List<Relic>> GetRelicsAsync()
        {
            var url = "https://api.warframestat.us/items/?language=es";
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();

            var array = await resp.Content.ReadFromJsonAsync<JsonArray>(_jsonOpts) ?? [];
            var list = new List<Relic>();

            foreach (var n in array.OfType<JsonObject>())
            {
                var category = n["category"]?.GetValue<string?>();

                // 🔹 Solo procesar reliquias normales o requiem
                if (category is not ("Relics" or "Requiem Relics"))
                    continue;

                // 🔹 Leer correctamente el campo 'rewards' (no 'drops')
                var rewardsArray = n["rewards"] as JsonArray;
                string? rewardsJson = null;

                if (rewardsArray != null)
                {
                    try
                    {
                        rewardsJson = rewardsArray.ToJsonString();
                    }
                    catch
                    {
                        // Si hay problema en serialización, lo deja en null
                        rewardsJson = null;
                    }
                }

                // 🔹 Crear y agregar la reliquia
                list.Add(new Relic
                {
                    UniqueName = n["uniqueName"]?.GetValue<string?>() ?? "",
                    Name = n["name"]?.GetValue<string?>() ?? "",
                    Category = category ?? "Relics",
                    ImageName = n["imageName"]?.GetValue<string?>(),
                    Vaulted = n["vaulted"]?.GetValue<bool?>() ?? false,
                    Tradable = n["tradable"]?.GetValue<bool?>() ?? false,
                    RewardsJson = rewardsJson,
                    Owned = false
                });
            }

            return list;
        }



    }
}
                