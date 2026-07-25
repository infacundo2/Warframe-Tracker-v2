using Microsoft.EntityFrameworkCore;
using WarframeInventory.Data;
using WarframeInventory.Models;

namespace WarframeInventory.Services;

public sealed class UserGoalService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public UserGoalService(IDbContextFactory<ApplicationDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<bool> IsGoalAsync(
        string userId, string targetType, string targetUnique, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.UserGoals.AsNoTracking().AnyAsync(
            x => x.UserId == userId
                 && x.TargetType == targetType
                 && x.TargetUnique == targetUnique, ct);
    }

    public async Task<bool> ToggleAsync(
        string userId,
        string targetType,
        string targetUnique,
        string displayName,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var existing = await db.UserGoals.FirstOrDefaultAsync(
            x => x.UserId == userId
                 && x.TargetType == targetType
                 && x.TargetUnique == targetUnique, ct);

        if (existing is not null)
        {
            db.UserGoals.Remove(existing);
            await db.SaveChangesAsync(ct);
            return false;
        }

        db.UserGoals.Add(new UserGoal
        {
            UserId = userId,
            TargetType = targetType,
            TargetUnique = targetUnique,
            DisplayName = displayName,
            Priority = 2
        });
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task UpdateAsync(
        string userId, int goalId, int priority, bool completed, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var goal = await db.UserGoals.FirstOrDefaultAsync(
            x => x.Id == goalId && x.UserId == userId, ct);
        if (goal is null)
            return;

        goal.Priority = Math.Clamp(priority, 1, 3);
        goal.IsCompleted = completed;
        goal.CompletedUtc = completed ? goal.CompletedUtc ?? DateTime.UtcNow : null;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(
        string userId, int goalId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var goal = await db.UserGoals.FirstOrDefaultAsync(
            x => x.Id == goalId && x.UserId == userId, ct);
        if (goal is null)
            return;

        db.UserGoals.Remove(goal);
        await db.SaveChangesAsync(ct);
    }
}
