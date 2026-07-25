using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WarframeInventory.Data;
using WarframeInventory.Models;

namespace WarframeInventory.Pages;

public partial class Mods
{
    private List<ModViewModel> pagedMods = new();
    private bool loading = true;
    private string? searchTerm = "";
    private int currentPage = 1;
    private int totalPages = 1;
    private const int pageSize = 10;
    private string userId = "";

    protected override async Task OnInitializedAsync()
    {
        Console.WriteLine("============== DEBUG MODS ==============");
        var authState = await AuthState.GetAuthenticationStateAsync();
        var user = authState.User;
        userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        Console.WriteLine($"👤 Usuario autenticado: {user.Identity?.Name} ({userId})");

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
            StateHasChanged();

            var query = Db.Mods.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.ToLower();
                query = query.Where(m => EF.Functions.Like(m.Name, $"%{term}%"));
            }

            var totalCount = await query.CountAsync();
            totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            currentPage = Math.Clamp(page, 1, totalPages);

            var mods = await query
                .OrderBy(m => m.Name)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            Console.WriteLine($"📦 Mods cargados: {mods.Count}");
            var pageKeys = mods.Select(x => x.UniqueName).ToList();
            var ownedList = await Db.UserMods.AsNoTracking()
                .Where(u => u.UserId == userId && pageKeys.Contains(u.ModUnique))
                .ToListAsync();

            Console.WriteLine($"📊 Registros de usuario encontrados: {ownedList.Count}");
            foreach (var o in ownedList)
                Console.WriteLine($"   -> {o.ModUnique}, Owned={o.Owned}");

            pagedMods = mods.Select(m => new ModViewModel
            {
                Id = m.Id,
                UniqueName = m.UniqueName,
                Name = m.Name,
                Description = m.Description,
                Polarity = m.Polarity,
                Rarity = m.Rarity,
                ImageName = m.ImageName,
                IsOwned = ownedList.Any(o => o.ModUnique == m.UniqueName && o.Owned)
            }).ToList();

            loading = false;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error cargando mods: {ex}");
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

    private static string GetImageUrl(string? imageName)
        => string.IsNullOrWhiteSpace(imageName)
            ? "/images/item-placeholder.svg"
            : $"https://cdn.warframestat.us/img/{imageName}";

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
    }
}
