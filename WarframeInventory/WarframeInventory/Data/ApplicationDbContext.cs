using Microsoft.EntityFrameworkCore;
using WarframeInventory.Models;

namespace WarframeInventory.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Warframe> Warframes { get; set; }
        public DbSet<Weapon> Weapons { get; set; }
        public DbSet<Mod> Mods { get; set; }
        public DbSet<Relic> Relics { get; set; }
    }
}
