using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WarframeInventory.Data;
using WarframeInventory.Models;

namespace WarframeInventory.Services
{
    public class DataSyncService
    {
        private readonly ApplicationDbContext _db;
        private readonly WarframeApiService _api;

        public DataSyncService(ApplicationDbContext db, WarframeApiService api)
        {
            _db = db;
            _api = api;
        }

        public async Task SyncAllAsync()
        {
            await _db.Database.EnsureCreatedAsync();
            await SyncWarframesAsync();
            await SyncWeaponsAsync();
            await SyncModsAsync();
            await SyncRelicsAsync();
        }

        // -------------------------------
        // 🔹 WARFRAMES
        // -------------------------------
        public async Task SyncWarframesAsync()
        {
            try
            {
                var items = await _api.GetWarframesAsync();
                if (items.Count == 0) return;

                foreach (var x in items)
                {
                    var existing = await _db.Warframes.FirstOrDefaultAsync(w => w.UniqueName == x.UniqueName);

                    // Si el API devuelve lista de componentes, los convertimos a JSON
                    string? componentsJson = x.ComponentsJson;
                    if (componentsJson == null && x is { })
                    {
                        try
                        {
                            // Si el objeto API trae la propiedad Components (lista), la serializamos
                            var compsProp = typeof(Warframe).GetProperty("Components");
                            if (compsProp != null)
                            {
                                var compsValue = compsProp.GetValue(x);
                                if (compsValue != null)
                                    componentsJson = System.Text.Json.JsonSerializer.Serialize(compsValue);
                            }
                        }
                        catch { componentsJson = null; }
                    }

                    if (existing == null)
                    {
                        _db.Warframes.Add(new Warframe
                        {
                            UniqueName = x.UniqueName,
                            Name = x.Name,
                            Description = x.Description,
                            ImageName = x.ImageName,
                            Health = x.Health,
                            Armor = x.Armor,
                            ComponentsJson = componentsJson
                        });
                    }
                    else
                    {
                        existing.Name = x.Name;
                        existing.Description = x.Description;
                        existing.ImageName = x.ImageName;
                        existing.Health = x.Health;
                        existing.Armor = x.Armor;
                        existing.ComponentsJson = componentsJson;
                    }
                }

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error SyncWarframesAsync: {ex.Message}");
            }
        }


        // -------------------------------
        // 🔹 WEAPONS
        // -------------------------------
        public async Task SyncWeaponsAsync()
        {
            try
            {
                var items = await _api.GetWeaponsAsync();
                if (items.Count == 0) return;

                foreach (var x in items)
                {
                    var existing = await _db.Weapons.FirstOrDefaultAsync(w => w.UniqueName == x.UniqueName);
                    if (existing == null)
                        _db.Weapons.Add(x);
                    else
                    {
                        existing.Name = x.Name;
                        existing.Category = x.Category;
                        existing.Type = x.Type;
                        existing.ImageName = x.ImageName;
                        existing.IsPrime = x.IsPrime;
                        existing.MasteryReq = x.MasteryReq;
                        existing.ComponentsJson = x.ComponentsJson;
                        existing.Description = x.Description;
                    }
                }
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error SyncWeaponsAsync: {ex.Message}");
            }
        }

        // -------------------------------
        // 🔹 MODS
        // -------------------------------
        public async Task SyncModsAsync()
        {
            try
            {
                var items = await _api.GetModsAsync();
                if (items.Count == 0) return;

                foreach (var x in items)
                {
                    var existing = await _db.Mods.FirstOrDefaultAsync(w => w.UniqueName == x.UniqueName);
                    if (existing == null)
                        _db.Mods.Add(x);
                    else
                    {
                        existing.Name = x.Name;
                        existing.Category = x.Category;
                        existing.CompatName = x.CompatName;
                        existing.ImageName = x.ImageName;
                        existing.IsAugment = x.IsAugment;
                        existing.IsPrime = x.IsPrime;
                        existing.Polarity = x.Polarity;
                        existing.Rarity = x.Rarity;
                        existing.BaseDrain = x.BaseDrain;
                        existing.FusionLimit = x.FusionLimit;
                        existing.Description = x.Description;
                        existing.LevelStatsJson = x.LevelStatsJson;
                    }
                }
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error SyncModsAsync: {ex.Message}");
            }
        }

        // -------------------------------
        // 🔹 RELICS
        // -------------------------------
        public async Task SyncRelicsAsync()
        {
            try
            {
                var items = await _api.GetRelicsAsync();
                if (items.Count == 0) return;

                foreach (var x in items)
                {
                    var existing = await _db.Relics.FirstOrDefaultAsync(w => w.UniqueName == x.UniqueName);
                    if (existing == null)
                        _db.Relics.Add(x);
                    else
                    {
                        existing.Name = x.Name;
                        existing.Category = x.Category;
                        existing.ImageName = x.ImageName;
                        existing.Vaulted = x.Vaulted;
                        existing.Tradable = x.Tradable;
                        existing.RewardsJson = x.RewardsJson;
                    }
                }
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error SyncRelicsAsync: {ex.Message}");
            }
        }
    }
}
