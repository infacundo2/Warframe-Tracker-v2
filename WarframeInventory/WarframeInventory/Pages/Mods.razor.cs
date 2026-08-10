using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using WarframeInventory.Data;
using WarframeInventory.Models;

namespace WarframeInventory.Pages;

public partial class Mods
{
    private List<ModViewModel> pagedMods = new();
    private bool loading = true;
    private string loadError = "";
    private string? searchTerm = "";
    private int currentPage = 1;
    private int totalPages = 1;
    private const int pageSize = 10;
    private string userId = "";
    private string compatibilityFilter = "all";
    private string polarityFilter = "all";
    private string rarityFilter = "all";
    private string collectionFilter = "all";
    private string ownedFilter = "all";
    private List<string> compatibilities = [];
    private List<string> polarities = [];
    private List<string> rarities = [];
    private List<DuplicateModView> duplicateMods = [];

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthState.GetAuthenticationStateAsync();
        var user = authState.User;
        userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

        var facets = await Cache.GetOrCreateAsync("catalog:mods:facets", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            var compatibilityValues = await Db.Mods.AsNoTracking().Where(x => x.CompatName != null && x.CompatName != "")
                .Select(x => x.CompatName!).Distinct().OrderBy(x => x).ToListAsync();
            var polarityValues = await Db.Mods.AsNoTracking().Where(x => x.Polarity != null && x.Polarity != "")
                .Select(x => x.Polarity!).Distinct().OrderBy(x => x).ToListAsync();
            var rarityValues = await Db.Mods.AsNoTracking().Where(x => x.Rarity != null && x.Rarity != "")
                .Select(x => x.Rarity!).Distinct().OrderBy(x => x).ToListAsync();
            return new ModFacets(compatibilityValues, polarityValues, rarityValues);
        });
        compatibilities = facets?.Compatibilities ?? [];
        polarities = facets?.Polarities ?? [];
        rarities = facets?.Rarities ?? [];
        if (!string.IsNullOrWhiteSpace(userId))
        {
            duplicateMods = await (
                from inventory in Db.UserMods.AsNoTracking()
                join mod in Db.Mods.AsNoTracking() on inventory.ModUnique equals mod.UniqueName
                where inventory.UserId == userId && inventory.Quantity > 1
                orderby inventory.Quantity descending
                select new DuplicateModView
                {
                    UniqueName = mod.UniqueName,
                    Name = mod.Name,
                    Rarity = mod.Rarity ?? "",
                    Quantity = inventory.Quantity
                }).ToListAsync();
            foreach (var duplicate in duplicateMods)
                duplicate.EstimatedEndo = duplicate.ExtraCopies * DissolveValue(duplicate.Rarity);
        }
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        currentPage = 1;
        await LoadPageAsync(currentPage);
    }

    private async Task LoadPageAsync(int page)
    {
        try
        {
            loading = true;
            loadError = "";
            StateHasChanged();

            var query = Db.Mods.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.ToLower();
                query = query.Where(m => EF.Functions.Like(m.Name, $"%{term}%"));
            }
            if (compatibilityFilter != "all")
                query = query.Where(x => x.CompatName == compatibilityFilter);
            if (polarityFilter != "all")
                query = query.Where(x => x.Polarity == polarityFilter);
            if (rarityFilter != "all")
                query = query.Where(x => x.Rarity == rarityFilter);
            query = collectionFilter switch
            {
                "primed" => query.Where(x => x.IsPrime),
                "augment" => query.Where(x => x.IsAugment),
                "galvanized" => query.Where(x => x.Name.StartsWith("Galvanized")),
                "archon" => query.Where(x => x.Name.StartsWith("Archon")),
                _ => query
            };
            if (ownedFilter != "all" && !string.IsNullOrWhiteSpace(userId))
            {
                var ownedKeys = Db.UserMods.Where(x => x.UserId == userId && (x.Owned || x.Quantity > 0))
                    .Select(x => x.ModUnique);
                query = ownedFilter == "owned" ? query.Where(x => ownedKeys.Contains(x.UniqueName))
                    : query.Where(x => !ownedKeys.Contains(x.UniqueName));
            }

            var totalCount = await query.CountAsync();
            totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            currentPage = Math.Clamp(page, 1, totalPages);

            var mods = await query
                .OrderBy(m => m.Name)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new
                {
                    m.Id, m.UniqueName, m.Name, m.Description, m.Polarity, m.Rarity, m.ImageName
                })
                .ToListAsync();
            var pageKeys = mods.Select(x => x.UniqueName).ToList();
            var ownedList = await Db.UserMods.AsNoTracking()
                .Where(u => u.UserId == userId && pageKeys.Contains(u.ModUnique))
                .ToListAsync();
            var ownedByKey = ownedList.ToDictionary(x => x.ModUnique, StringComparer.OrdinalIgnoreCase);

            pagedMods = mods.Select(m => new ModViewModel
            {
                Id = m.Id,
                UniqueName = m.UniqueName,
                Name = m.Name,
                Description = m.Description,
                Polarity = m.Polarity,
                Rarity = m.Rarity,
                ImageName = m.ImageName,
                IsOwned = ownedByKey.TryGetValue(m.UniqueName, out var inventory) && (inventory.Owned || inventory.Quantity > 0),
                Quantity = ownedByKey.GetValueOrDefault(m.UniqueName)?.Quantity ?? 0
            }).ToList();

            loading = false;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error cargando mods: {ex}");
            loadError = "No fue posible cargar los mods. Reintenta en unos segundos.";
            loading = false;
            StateHasChanged();
        }
    }

    private async Task ToggleOwnedAsync(ModViewModel mod, bool value)
    {
        Console.WriteLine($"🟡 ToggleOwnedAsync → {mod.Name} = {value}");
        Console.WriteLine($"   Usuario actual: {userId}");

        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                Console.WriteLine("❌ userId vacío. No se guardará nada.");
                return;
            }

            var existing = await Db.UserMods
                .FirstOrDefaultAsync(x => x.UserId == userId && x.ModUnique == mod.UniqueName);

            if (existing == null)
            {
                Console.WriteLine($"➕ No existía registro, creando nuevo.");
                existing = new UserMod
                {
                    UserId = userId,
                    ModUnique = mod.UniqueName,
                    Owned = value
                };
                Db.UserMods.Add(existing);
            }
            else
            {
                Console.WriteLine($"🔁 Actualizando existente: {existing.ModUnique} → {value}");
                existing.Owned = value;
            }

            var result = await Db.SaveChangesAsync();
            Console.WriteLine($"✅ SaveChanges resultado: {result} filas afectadas.");
            mod.IsOwned = value;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error al guardar {mod.Name}: {ex}");
        }
    }

    private async Task OnPageChanged(int newPage)
    {
        if (newPage == currentPage || newPage < 1 || newPage > totalPages)
            return;
        await LoadPageAsync(newPage);
    }

    private async Task ChangeFilterAsync(string filter, string value)
    {
        switch (filter)
        {
            case "compat": compatibilityFilter = value; break;
            case "polarity": polarityFilter = value; break;
            case "rarity": rarityFilter = value; break;
            case "collection": collectionFilter = value; break;
            case "owned": ownedFilter = value; break;
        }
        await RefreshAsync();
    }

    private static string GetImageUrl(string? imageName)
        => string.IsNullOrWhiteSpace(imageName)
            ? "/images/item-placeholder.svg"
            : $"https://cdn.warframestat.us/img/{imageName}";
    private static int DissolveValue(string rarity) => rarity.ToLowerInvariant() switch
    {
        "legendary" or "legendaria" => 20,
        "rare" or "rara" => 15,
        "uncommon" or "poco común" => 10,
        _ => 5
    };

    private class ModViewModel
    {
        public int Id { get; set; }
        public string UniqueName { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? Polarity { get; set; }
        public string? Rarity { get; set; }
        public string? ImageName { get; set; }
        public bool IsOwned { get; set; }
        public int Quantity { get; set; }
    }
    private sealed class DuplicateModView
    {
        public string UniqueName { get; set; } = "";
        public string Name { get; set; } = "";
        public string Rarity { get; set; } = "";
        public int Quantity { get; set; }
        public int ExtraCopies => Math.Max(0, Quantity - 1);
        public int EstimatedEndo { get; set; }
    }
    private sealed record ModFacets(List<string> Compatibilities, List<string> Polarities, List<string> Rarities);
}
