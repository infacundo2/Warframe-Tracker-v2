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

        private static string? BuildModAcquisitionJson(JsonObject item)
        {
            var methods = new List<ModDrop>();

            if (item["drops"] is JsonArray drops)
            {
                methods.AddRange(drops.OfType<JsonObject>().Select(drop => new ModDrop
                {
                    Chance = drop["chance"]?.GetValue<double?>() ?? 0,
                    Location = drop["location"]?.GetValue<string?>(),
                    Rarity = drop["rarity"]?.GetValue<string?>(),
                    Type = drop["type"]?.GetValue<string?>(),
                    Method = "Drop"
                }));
            }

            if (item["transmutable"]?.GetValue<bool?>() == true)
            {
                methods.Add(new ModDrop
                {
                    Location = "Transmutación de mods",
                    Type = "Método alternativo",
                    Method = "Transmutación"
                });
            }

            if (item["tradable"]?.GetValue<bool?>() == true)
            {
                methods.Add(new ModDrop
                {
                    Location = "Intercambio con otros jugadores",
                    Type = "Método alternativo",
                    Method = "Intercambio"
                });
            }

            var wikiUrl = item["wikiaUrl"]?.GetValue<string?>();
            if (!string.IsNullOrWhiteSpace(wikiUrl))
            {
                methods.Add(new ModDrop
                {
                    Location = "Consultar métodos adicionales",
                    Type = "Wiki de Warframe",
                    Method = "Guía",
                    Url = wikiUrl
                });
            }

            return methods.Count == 0 ? null : JsonSerializer.Serialize(methods);
        }

        // -------------------------------
        // 🔹 WARFRAMES (con componentes serializados)
        // -------------------------------
        public async Task<List<Warframe>> GetWarframesAsync(CancellationToken cancellationToken = default)
        {
            var url = "https://api.warframestat.us/warframes/?language=es";
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            resp.EnsureSuccessStatusCode();

            var array = await resp.Content.ReadFromJsonAsync<JsonArray>(_jsonOpts, cancellationToken) ?? [];
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
                            UniqueName = c["uniqueName"]?.GetValue<string>() ?? "",
                            ItemCount = Math.Max(1, c["itemCount"]?.GetValue<int?>() ?? 1),
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
        public async Task<List<Mod>> GetModsAsync(CancellationToken cancellationToken = default)
        {
            var url = "https://api.warframestat.us/mods/?language=es";
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            resp.EnsureSuccessStatusCode();

            var array = await resp.Content.ReadFromJsonAsync<JsonArray>(_jsonOpts, cancellationToken) ?? [];
            var list = new List<Mod>();

            foreach (var n in array.OfType<JsonObject>())
            {
                var levelStats = n["levelStats"];
                string? levelStatsJson = levelStats == null ? null : levelStats.ToJsonString();
                string? dropsJson = BuildModAcquisitionJson(n);

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
        public async Task<List<Weapon>> GetWeaponsAsync(CancellationToken cancellationToken = default)
        {
            var url = "https://api.warframestat.us/weapons/?language=es";
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            resp.EnsureSuccessStatusCode();

            var array = await resp.Content.ReadFromJsonAsync<JsonArray>(_jsonOpts, cancellationToken) ?? [];
            var list = new List<Weapon>();

            foreach (var n in array.OfType<JsonObject>())
            {
                var comps = n["components"];
                string? compsJson = null;

                if (comps is JsonArray compArray)
                {
                    var parsed = compArray
                        .OfType<JsonObject>()
                        .Select(c => new WarframeComponent
                        {
                            Name = c["name"]?.GetValue<string>() ?? "",
                            UniqueName = c["uniqueName"]?.GetValue<string>() ?? "",
                            ItemCount = Math.Max(1, c["itemCount"]?.GetValue<int?>() ?? 1),
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

                list.Add(new Weapon
                {
                    UniqueName = n["uniqueName"]?.GetValue<string?>() ?? "",
                    Name = n["name"]?.GetValue<string?>() ?? "",
                    Category = n["category"]?.GetValue<string?>() ?? "Weapons",
                    Type = n["type"]?.GetValue<string?>(),
                    ImageName = n["imageName"]?.GetValue<string?>(),
                    IsPrime = n["isPrime"]?.GetValue<bool?>() ?? false,
                    MasteryReq = n["masteryReq"]?.GetValue<int?>(),
                    ComponentsJson = compsJson,
                    Description = n["description"]?.GetValue<string?>(),
                    Owned = false
                });
            }

            return list;
        }


        // -------------------------------
        // 🔹 RELICS
        // -------------------------------
        public async Task<List<RelicImport>> GetRelicsAsync(CancellationToken cancellationToken = default)
        {
            var url = "https://api.warframestat.us/items/?language=es";
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            resp.EnsureSuccessStatusCode();

            var array = await resp.Content.ReadFromJsonAsync<JsonArray>(_jsonOpts, cancellationToken) ?? [];
            var list = new List<RelicImport>();

            foreach (var n in array.OfType<JsonObject>())
            {
                var category = n["category"]?.GetValue<string?>();
                if (category is not ("Relics" or "Requiem Relics"))
                    continue;

                var name = n["name"]?.GetValue<string?>() ?? "(sin nombre)";
                //Console.WriteLine($"\n🔹 Procesando: {name}");

                // --- Recompensas ---
                List<object>? rewards = null;
                var normalizedRewards = new List<RelicReward>();
                if (n["rewards"] is JsonArray rewardsArray)
                {
                    rewards = new List<object>();
                    foreach (var rewardNode in rewardsArray.OfType<JsonObject>())
                    {
                        var item = rewardNode["item"] as JsonObject;
                        if (item is null)
                            continue;

                        var relicUnique = n["uniqueName"]?.GetValue<string?>() ?? "";
                        var itemName = item["name"]?.GetValue<string?>() ?? "";
                        var marketUrlName = item["warframeMarket"]?["urlName"]?.GetValue<string?>();
                        if (string.IsNullOrWhiteSpace(itemName)
                            && string.IsNullOrWhiteSpace(marketUrlName))
                            continue;

                        // La API reutiliza a veces el identificador de la variante Bronze en
                        // las recompensas Silver/Gold/Platinum. La identidad lógica debe ser
                        // estable entre refinamientos para conservar sus seis probabilidades.
                        var itemUnique = !string.IsNullOrWhiteSpace(marketUrlName)
                            ? $"market:{marketUrlName}"
                            : $"name:{NormalizeRewardName(itemName)}";
                        if (string.IsNullOrWhiteSpace(itemUnique))
                            continue;

                        rewards.Add(JsonSerializer.Deserialize<object>(rewardNode.ToJsonString())!);
                        normalizedRewards.Add(new RelicReward
                        {
                            RelicUnique = relicUnique,
                            ItemUnique = itemUnique,
                            ItemName = string.IsNullOrWhiteSpace(itemName) ? itemUnique : itemName,
                            Rarity = rewardNode["rarity"]?.GetValue<string?>(),
                            Chance = rewardNode["chance"]?.GetValue<double?>() ?? 0,
                            MarketUrlName = marketUrlName
                        });
                    }
                }

                // --- Drops (ubicaciones) ---
                List<object>? drops = null;
                if (n["drops"] is JsonArray dropsArray)
                {
                    drops = new List<object>();
                    //Console.WriteLine($"   ✅ Encontrado campo 'drops' con {dropsArray.Count} ubicaciones");

                    foreach (var drop in dropsArray.OfType<JsonObject>())
                    {
                        var loc = drop["location"]?.GetValue<string?>();
                        if (loc != null)
                        {
                            drops.Add(new
                            {
                                location = loc,
                                rarity = drop["rarity"]?.GetValue<string?>(),
                                chance = drop["chance"]?.GetValue<double?>(),
                                type = drop["type"]?.GetValue<string?>(),
                                rotation = drop["rotation"]?.GetValue<string?>()
                            });
                        }
                    }

                    //Console.WriteLine($"   → Total procesadas: {drops.Count}");
                }
                else
                {
                    //Console.WriteLine($"   ⚠️ No existe 'drops' en JSON de esta reliquia");
                }

                // --- Agregar ---
                var relic = new Relic
                {
                    UniqueName = n["uniqueName"]?.GetValue<string?>() ?? "",
                    Name = name,
                    Category = category ?? "Relics",
                    ImageName = n["imageName"]?.GetValue<string?>(),
                    Vaulted = n["vaulted"]?.GetValue<bool?>() ?? false,
                    Tradable = n["tradable"]?.GetValue<bool?>() ?? false,
                    RewardsJson = rewards != null ? JsonSerializer.Serialize(rewards) : null,
                    DropsJson = drops != null ? JsonSerializer.Serialize(drops) : null,
                    Owned = false
                };
                list.Add(new RelicImport(relic, normalizedRewards));
            }

            //Console.WriteLine($"✅ Total reliquias guardadas: {list.Count}");
            return list;
        }

        private static string NormalizeRewardName(string value)
            => string.Join(' ', value.Split(
                    [' ', '\t', '\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries))
                .Trim()
                .ToUpperInvariant();
    }

    public sealed record RelicImport(Relic Relic, IReadOnlyCollection<RelicReward> Rewards);
}
