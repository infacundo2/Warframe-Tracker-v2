using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WarframeInventory.Data;
using WarframeInventory.Models;

namespace WarframeInventory.Services;

public sealed class InventoryToolsService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public InventoryToolsService(IDbContextFactory<ApplicationDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<string> ExportAsync(string userId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var payload = new InventoryTransfer(
            1,
            DateTime.UtcNow,
            await db.UserWarframes.AsNoTracking().Where(x => x.UserId == userId)
                .Select(x => new OwnedEntry(x.WarframeUnique, x.Owned ? 1 : 0)).ToListAsync(ct),
            await db.UserWeapons.AsNoTracking().Where(x => x.UserId == userId)
                .Select(x => new OwnedEntry(x.WeaponUnique, x.Owned ? 1 : 0)).ToListAsync(ct),
            await db.UserMods.AsNoTracking().Where(x => x.UserId == userId)
                .Select(x => new OwnedEntry(x.ModUnique, x.Quantity > 0 ? x.Quantity : x.Owned ? 1 : 0))
                .ToListAsync(ct),
            await db.UserRelics.AsNoTracking().Where(x => x.UserId == userId)
                .Select(x => new OwnedEntry(x.RelicUnique, x.Quantity)).ToListAsync(ct),
            await db.UserComponents.AsNoTracking().Where(x => x.UserId == userId)
                .Select(x => new ComponentEntry(x.ParentUnique, x.ComponentName, x.Quantity > 0
                    ? x.Quantity : x.Owned ? 1 : 0)).ToListAsync(ct),
            (await db.UserWarframes.AsNoTracking().Where(x => x.UserId == userId)
                .Select(x => new StateEntry("warframe", x.WarframeUnique, x.OwnershipState))
                .ToListAsync(ct))
            .Concat(await db.UserWeapons.AsNoTracking().Where(x => x.UserId == userId)
                .Select(x => new StateEntry("weapon", x.WeaponUnique, x.OwnershipState))
                .ToListAsync(ct)).ToList());
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public async Task<string> ExportCsvAsync(string userId, CancellationToken ct = default)
    {
        var transfer = JsonSerializer.Deserialize<InventoryTransfer>(await ExportAsync(userId, ct))!;
        var lines = new List<string> { "category,unique_name,parent_unique,display_name,quantity" };
        lines.AddRange(transfer.Warframes.Select(x => Csv("warframe", x.UniqueName, "", "", x.Quantity)));
        lines.AddRange(transfer.Weapons.Select(x => Csv("weapon", x.UniqueName, "", "", x.Quantity)));
        lines.AddRange(transfer.Mods.Select(x => Csv("mod", x.UniqueName, "", "", x.Quantity)));
        lines.AddRange(transfer.Relics.Select(x => Csv("relic", x.UniqueName, "", "", x.Quantity)));
        lines.AddRange(transfer.Components.Select(x =>
            Csv("component", "", x.ParentUnique, x.ComponentName, x.Quantity)));
        lines.AddRange((transfer.States ?? []).Select(x =>
            Csv($"{x.Category}-state", x.UniqueName, "", x.State, 0)));
        return string.Join(Environment.NewLine, lines);
    }

    public async Task<ImportSummary> ImportAsync(
        string userId, string json, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Deserialize<InventoryTransfer>(json)
            ?? throw new InvalidOperationException("El archivo no contiene un inventario válido.");
        if (payload.Version != 1)
            throw new InvalidOperationException("La versión del archivo no es compatible.");
        if (payload.Warframes.Count + payload.Weapons.Count + payload.Mods.Count
            + payload.Relics.Count + payload.Components.Count > 100_000)
            throw new InvalidOperationException("El archivo excede el límite permitido.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var changes = 0;
        foreach (var entry in payload.Warframes.Where(Valid))
        {
            var row = await db.UserWarframes.FirstOrDefaultAsync(
                x => x.UserId == userId && x.WarframeUnique == entry.UniqueName, ct);
            if (row is null)
                db.UserWarframes.Add(new UserWarframe { UserId = userId,
                    WarframeUnique = entry.UniqueName, Owned = entry.Quantity > 0 });
            else row.Owned = entry.Quantity > 0;
            changes++;
        }
        foreach (var entry in payload.Weapons.Where(Valid))
        {
            var row = await db.UserWeapons.FirstOrDefaultAsync(
                x => x.UserId == userId && x.WeaponUnique == entry.UniqueName, ct);
            if (row is null)
                db.UserWeapons.Add(new UserWeapon { UserId = userId,
                    WeaponUnique = entry.UniqueName, Owned = entry.Quantity > 0 });
            else row.Owned = entry.Quantity > 0;
            changes++;
        }
        foreach (var entry in payload.Mods.Where(Valid))
        {
            var row = await db.UserMods.FirstOrDefaultAsync(
                x => x.UserId == userId && x.ModUnique == entry.UniqueName, ct);
            if (row is null)
                db.UserMods.Add(new UserMod { UserId = userId, ModUnique = entry.UniqueName,
                    Quantity = Math.Max(0, entry.Quantity), Owned = entry.Quantity > 0 });
            else { row.Quantity = Math.Max(0, entry.Quantity); row.Owned = entry.Quantity > 0; }
            changes++;
        }
        foreach (var entry in payload.Relics.Where(Valid))
        {
            var row = await db.UserRelics.FirstOrDefaultAsync(
                x => x.UserId == userId && x.RelicUnique == entry.UniqueName, ct);
            if (row is null)
                db.UserRelics.Add(new UserRelic { UserId = userId, RelicUnique = entry.UniqueName,
                    Quantity = Math.Max(0, entry.Quantity) });
            else row.Quantity = Math.Max(0, entry.Quantity);
            changes++;
        }
        foreach (var entry in payload.Components.Where(x =>
                     !string.IsNullOrWhiteSpace(x.ParentUnique)
                     && !string.IsNullOrWhiteSpace(x.ComponentName)))
        {
            var row = await db.UserComponents.FirstOrDefaultAsync(x => x.UserId == userId
                && x.ParentUnique == entry.ParentUnique
                && x.ComponentName == entry.ComponentName, ct);
            if (row is null)
                db.UserComponents.Add(new UserComponent { UserId = userId,
                    ParentUnique = entry.ParentUnique, ComponentName = entry.ComponentName,
                    Quantity = Math.Max(0, entry.Quantity), Owned = entry.Quantity > 0 });
            else { row.Quantity = Math.Max(0, entry.Quantity); row.Owned = entry.Quantity > 0; }
            changes++;
        }
        foreach (var entry in payload.States ?? [])
        {
            var state = NormalizeState(entry.State);
            if (entry.Category == "warframe")
            {
                var row = await db.UserWarframes.FirstOrDefaultAsync(x =>
                    x.UserId == userId && x.WarframeUnique == entry.UniqueName, ct);
                if (row is not null) { row.OwnershipState = state; row.Owned = state == "built"; }
            }
            else if (entry.Category == "weapon")
            {
                var row = await db.UserWeapons.FirstOrDefaultAsync(x =>
                    x.UserId == userId && x.WeaponUnique == entry.UniqueName, ct);
                if (row is not null) { row.OwnershipState = state; row.Owned = state == "built"; }
            }
        }
        await db.SaveChangesAsync(ct);
        return new ImportSummary(changes);
    }

    private static bool Valid(OwnedEntry entry)
        => !string.IsNullOrWhiteSpace(entry.UniqueName) && entry.UniqueName.Length <= 255
           && entry.Quantity is >= 0 and <= 9999;

    private static string Csv(
        string category, string unique, string parent, string display, int quantity)
        => string.Join(",", new[] { category, unique, parent, display }
            .Select(value => $"\"{value.Replace("\"", "\"\"")}\"")
            .Append(quantity.ToString()));
    private static string NormalizeState(string? state)
        => state is "blueprint" or "set" or "built" ? state : "missing";
}

public sealed record InventoryTransfer(
    int Version, DateTime ExportedUtc,
    List<OwnedEntry> Warframes, List<OwnedEntry> Weapons, List<OwnedEntry> Mods,
    List<OwnedEntry> Relics, List<ComponentEntry> Components,
    List<StateEntry>? States = null);
public sealed record OwnedEntry(string UniqueName, int Quantity);
public sealed record ComponentEntry(string ParentUnique, string ComponentName, int Quantity);
public sealed record StateEntry(string Category, string UniqueName, string State);
public sealed record ImportSummary(int ChangedEntries);
