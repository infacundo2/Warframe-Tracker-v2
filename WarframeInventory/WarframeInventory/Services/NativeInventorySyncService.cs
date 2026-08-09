using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WarframeInventory.Data;

namespace WarframeInventory.Services;

/// <summary>
/// Keeps an isolated, expiring inventory capture pipeline for each authenticated
/// Native user. The Electron pipeline remains unchanged and local-only.
/// </summary>
public sealed class NativeInventorySyncService
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(1);
    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public NativeInventorySyncService(
        IMemoryCache cache,
        IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _cache = cache;
        _dbFactory = dbFactory;
    }

    public DesktopCaptureReceipt Stage(string userId, string rawJson, string source)
        => GetSession(userId).Stage(rawJson, source);

    public DesktopCaptureStatus GetStatus(string userId)
        => GetSession(userId).GetStatus();

    public Task<DesktopInventoryPreview> PreviewAsync(
        string userId,
        CancellationToken cancellationToken = default)
        => GetSession(userId).PreviewAsync(userId, cancellationToken);

    public Task<DesktopApplyResult> ApplyAsync(
        string userId,
        DesktopInventoryPreview preview,
        CancellationToken cancellationToken = default)
        => GetSession(userId).ApplyAsync(userId, preview, cancellationToken);

    private DesktopInventorySyncService GetSession(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new DesktopInventoryException("Debes iniciar sesión para sincronizar el inventario.");

        return _cache.GetOrCreate(
            $"native-inventory-session:{userId}",
            entry =>
            {
                entry.SetSlidingExpiration(SessionLifetime);
                return new DesktopInventorySyncService(_dbFactory);
            })!;
    }
}
