using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WarframeInventory.Data;
using WarframeInventory.Models;

namespace WarframeInventory.Services;

public sealed class BuildService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    public BuildService(IDbContextFactory<ApplicationDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<BuildWorkspace> LoadAsync(string userId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var targets = (await db.Warframes.AsNoTracking().OrderBy(x => x.Name)
                .Select(x => new BuildTarget("warframe", x.UniqueName, x.Name)).ToListAsync(ct))
            .Concat(await db.Weapons.AsNoTracking().OrderBy(x => x.Name)
                .Select(x => new BuildTarget("weapon", x.UniqueName, x.Name)).ToListAsync(ct))
            .ToList();
        var ownedMods = await db.UserMods.AsNoTracking()
            .Where(x => x.UserId == userId && (x.Owned || x.Quantity > 0))
            .Select(x => x.ModUnique).ToListAsync(ct);
        var mods = await db.Mods.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new BuildMod(x.UniqueName, x.Name, x.BaseDrain ?? 0,
                x.Polarity ?? "", ownedMods.Contains(x.UniqueName))).ToListAsync(ct);
        var builds = await db.SavedBuilds.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.UpdatedUtc).ToListAsync(ct);
        return new BuildWorkspace(targets, mods, builds);
    }

    public async Task SaveAsync(
        string userId, int id, string name, BuildTarget target, string tags,
        int capacity, int forma, IReadOnlyList<BuildSlot> slots, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var build = id == 0 ? null : await db.SavedBuilds
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (build is null)
        {
            build = new SavedBuild { UserId = userId };
            db.SavedBuilds.Add(build);
        }
        build.Name = name.Trim()[..Math.Min(name.Trim().Length, 120)];
        build.TargetType = target.Type;
        build.TargetUnique = target.UniqueName;
        build.TargetName = target.Name;
        build.Tags = tags.Trim()[..Math.Min(tags.Trim().Length, 255)];
        build.Capacity = Math.Clamp(capacity, 0, 90);
        build.FormaCount = Math.Clamp(forma, 0, 100);
        build.ModsJson = JsonSerializer.Serialize(slots);
        build.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string userId, int id, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var build = await db.SavedBuilds.FirstOrDefaultAsync(
            x => x.Id == id && x.UserId == userId, ct);
        if (build is null) return;
        db.SavedBuilds.Remove(build);
        await db.SaveChangesAsync(ct);
    }
}

public sealed record BuildWorkspace(
    IReadOnlyList<BuildTarget> Targets,
    IReadOnlyList<BuildMod> Mods,
    IReadOnlyList<SavedBuild> Builds);
public sealed record BuildTarget(string Type, string UniqueName, string Name);
public sealed record BuildMod(string UniqueName, string Name, int Drain, string Polarity, bool Owned);
public sealed record BuildSlot(string ModUnique, string Name, int Drain, string Polarity, bool Owned);
