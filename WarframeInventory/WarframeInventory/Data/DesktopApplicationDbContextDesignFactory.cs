using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WarframeInventory.Data;

public sealed class DesktopApplicationDbContextDesignFactory
    : IDesignTimeDbContextFactory<DesktopApplicationDbContext>
{
    public DesktopApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DesktopApplicationDbContext>()
            .UseSqlite("Data Source=warframe-tracker-design.db")
            .Options;
        return new DesktopApplicationDbContext(options);
    }
}
