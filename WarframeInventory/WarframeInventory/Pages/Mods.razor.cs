using Microsoft.EntityFrameworkCore;
using WarframeInventory.Data;
using WarframeInventory.Models;
namespace WarframeInventory.Pages;


public partial class Mods
{
    private List<Mod> pagedMods = new();
    private bool loading = true;
    private string? searchTerm = "";
    private int currentPage = 1;
    private int totalPages = 1;
    private const int pageSize = 10;

    protected override async Task OnInitializedAsync() => await LoadPageAsync(1);

    private async Task LoadPageAsync(int page)
    {
        loading = true;
        StateHasChanged();

        var query = Db.Mods.AsQueryable();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            string term = searchTerm.ToLower();
            query = query.Where(m => m.Name.ToLower().Contains(term));
        }

        int totalCount = await query.CountAsync();
        totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        if (page < 1) page = 1;
        if (page > totalPages) page = totalPages;
        currentPage = page;

        pagedMods = await query
            .OrderBy(m => m.Name)
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        loading = false;
        StateHasChanged();
    }

    private async Task OnPageChanged(int newPage)
    {
        currentPage = newPage;
        await LoadPageAsync(newPage);
    }

    private static string GetImageUrl(string? imageName)
        => string.IsNullOrWhiteSpace(imageName)
            ? "_content/MudBlazor/images/placeholder.png"
            : $"https://cdn.warframestat.us/img/{imageName}";
}
