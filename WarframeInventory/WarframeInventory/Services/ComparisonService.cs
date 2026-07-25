using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WarframeInventory.Data;
using WarframeInventory.Models;

namespace WarframeInventory.Services;

public sealed class ComparisonService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public ComparisonService(IDbContextFactory<ApplicationDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<IReadOnlyList<CompareOption>> OptionsAsync(
        string type, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return type == "warframe"
            ? await db.Warframes.AsNoTracking().OrderBy(x => x.Name)
                .Select(x => new CompareOption(x.UniqueName, x.Name)).ToListAsync(ct)
            : await db.Weapons.AsNoTracking().OrderBy(x => x.Name)
                .Select(x => new CompareOption(x.UniqueName, x.Name)).ToListAsync(ct);
    }

    public async Task<CompareItem?> LoadAsync(
        string type, string uniqueName, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        if (type == "warframe")
        {
            var item = await db.Warframes.AsNoTracking()
                .FirstOrDefaultAsync(x => x.UniqueName == uniqueName, ct);
            return item is null ? null : new CompareItem(item.Name, item.ImageName, [
                new("Salud", item.Health.ToString()),
                new("Armadura", item.Armor.ToString()),
                new("Componentes", ComponentCount(item.ComponentsJson).ToString()),
                new("Estado", item.Owned ? "Poseído" : "Pendiente")
            ], $"/relations/warframe/{Uri.EscapeDataString(item.UniqueName)}");
        }

        var weapon = await db.Weapons.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UniqueName == uniqueName, ct);
        return weapon is null ? null : new CompareItem(weapon.Name, weapon.ImageName, [
            new("Tipo", weapon.Type ?? "Sin datos"),
            new("Maestría", weapon.MasteryReq?.ToString() ?? "Sin datos"),
            new("Prime", weapon.IsPrime ? "Sí" : "No"),
            new("Componentes", ComponentCount(weapon.ComponentsJson).ToString()),
            new("Estado", weapon.Owned ? "Poseída" : "Pendiente")
        ], $"/relations/weapon/{Uri.EscapeDataString(weapon.UniqueName)}");
    }

    public async Task<RelationMap?> RelationsAsync(
        string type, string uniqueName, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var source = type == "warframe"
            ? await db.Warframes.AsNoTracking().Where(x => x.UniqueName == uniqueName)
                .Select(x => new { x.Name, x.ComponentsJson }).FirstOrDefaultAsync(ct)
            : await db.Weapons.AsNoTracking().Where(x => x.UniqueName == uniqueName)
                .Select(x => new { x.Name, x.ComponentsJson }).FirstOrDefaultAsync(ct);
        if (source is null)
            return null;

        var components = Deserialize(source.ComponentsJson);
        var rewardNames = components.Select(x => x.Name).ToList();
        var rewards = await db.RelicRewards.AsNoTracking()
            .Where(x => rewardNames.Contains(x.ItemName))
            .ToListAsync(ct);
        var relicKeys = rewards.Select(x => x.RelicUnique).Distinct().ToList();
        var relics = await db.Relics.AsNoTracking()
            .Where(x => relicKeys.Contains(x.UniqueName))
            .ToDictionaryAsync(x => x.UniqueName, ct);

        return new RelationMap(source.Name, components.Select(component =>
            new RelationComponent(component.Name, rewards
                .Where(x => x.ItemName == component.Name)
                .GroupBy(x => relics.TryGetValue(x.RelicUnique, out var relic)
                    ? relic.Name : x.RelicUnique)
                .Select(x =>
                {
                    var first = x.First();
                    var relic = relics.GetValueOrDefault(first.RelicUnique);
                    return new RelationRelic(
                        x.Key,
                        first.RelicUnique,
                        relic?.Vaulted ?? false,
                        first.Chance);
                }).ToList())).ToList());
    }

    private static int ComponentCount(string? json) => Deserialize(json).Count;
    private static List<WarframeComponent> Deserialize(string? json)
    {
        try
        {
            return string.IsNullOrWhiteSpace(json) ? [] :
                JsonSerializer.Deserialize<List<WarframeComponent>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch (JsonException) { return []; }
    }
}

public sealed record CompareOption(string UniqueName, string Name);
public sealed record CompareItem(
    string Name, string? ImageName, IReadOnlyList<CompareStat> Stats, string RelationRoute);
public sealed record CompareStat(string Label, string Value);
public sealed record RelationMap(string Name, IReadOnlyList<RelationComponent> Components);
public sealed record RelationComponent(string Name, IReadOnlyList<RelationRelic> Relics);
public sealed record RelationRelic(
    string Name, string UniqueName, bool Vaulted, double IntactChance);
