using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WarframeInventory.Data;
using WarframeInventory.Models;

namespace WarframeInventory.Services;

public sealed class BuildableAnalysisService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public BuildableAnalysisService(IDbContextFactory<ApplicationDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<IReadOnlyList<BuildableCandidate>> AnalyzeAsync(
        string userId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var inventory = await db.UserComponents.AsNoTracking()
            .Where(x => x.UserId == userId && (x.Owned || x.Quantity > 0))
            .ToListAsync(ct);
        var byParent = inventory
            .GroupBy(x => x.ParentUnique)
            .ToDictionary(x => x.Key, x => x.ToDictionary(y => y.ComponentName,
                StringComparer.OrdinalIgnoreCase));
        var ownedFrames = (await db.UserWarframes.AsNoTracking()
            .Where(x => x.UserId == userId && x.Owned)
            .Select(x => x.WarframeUnique)
            .ToListAsync(ct)).ToHashSet();
        var ownedWeapons = (await db.UserWeapons.AsNoTracking()
            .Where(x => x.UserId == userId && x.Owned)
            .Select(x => x.WeaponUnique)
            .ToListAsync(ct)).ToHashSet();
        var result = new List<BuildableCandidate>();

        var frames = await db.Warframes.AsNoTracking()
            .Select(x => new { x.UniqueName, x.Name, x.ImageName, x.ComponentsJson })
            .ToListAsync(ct);
        foreach (var frame in frames.Where(x => !ownedFrames.Contains(x.UniqueName)))
            AddCandidate(result, "warframe", frame.UniqueName, frame.Name,
                frame.ImageName, frame.ComponentsJson, byParent);

        var weapons = await db.Weapons.AsNoTracking()
            .Select(x => new { x.UniqueName, x.Name, x.ImageName, x.ComponentsJson })
            .ToListAsync(ct);
        foreach (var weapon in weapons.Where(x => !ownedWeapons.Contains(x.UniqueName)))
            AddCandidate(result, "weapon", weapon.UniqueName, weapon.Name,
                weapon.ImageName, weapon.ComponentsJson, byParent);

        return result
            .Where(x => x.IsReady || x.MissingCount == 1)
            .OrderByDescending(x => x.IsReady)
            .ThenBy(x => x.MissingCount)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private static void AddCandidate(
        ICollection<BuildableCandidate> result,
        string type,
        string unique,
        string name,
        string? imageName,
        string? componentsJson,
        IReadOnlyDictionary<string, Dictionary<string, UserComponent>> byParent)
    {
        var components = DeserializeComponents(componentsJson);
        if (components.Count == 0)
            return;

        byParent.TryGetValue(unique, out var stored);
        var missing = components
            .Where(x => stored is null || !stored.TryGetValue(x.Name, out var item)
                        || (!item.Owned && item.Quantity < Math.Max(1, x.ItemCount)))
            .Select(x => x.ItemCount > 1 ? $"{x.ItemCount} × {x.Name}" : x.Name)
            .ToList();
        var owned = components.Count - missing.Count;
        result.Add(new BuildableCandidate(type, unique, name, imageName,
            components.Count, owned, missing));
    }

    private static List<WarframeComponent> DeserializeComponents(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<WarframeComponent>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

public sealed record BuildableCandidate(
    string TargetType,
    string TargetUnique,
    string Name,
    string? ImageName,
    int ComponentCount,
    int OwnedComponentCount,
    IReadOnlyList<string> MissingComponents)
{
    public int MissingCount => MissingComponents.Count;
    public bool IsReady => MissingCount == 0;
    public int ProgressPercent => (int)Math.Round(OwnedComponentCount * 100d / ComponentCount);
}
