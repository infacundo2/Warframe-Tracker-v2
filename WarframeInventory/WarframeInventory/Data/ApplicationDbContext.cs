using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WarframeInventory.Models;

namespace WarframeInventory.Data
{
    // Heredamos de IdentityDbContext para incluir soporte de usuarios y roles
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Warframe> Warframes => Set<Warframe>();
        public DbSet<Mod> Mods => Set<Mod>();
        public DbSet<Weapon> Weapons => Set<Weapon>();
        public DbSet<Relic> Relics => Set<Relic>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // textos largos como LONGTEXT en MySQL
            modelBuilder.Entity<Mod>().Property(p => p.Description).HasColumnType("longtext");
            modelBuilder.Entity<Mod>().Property(p => p.LevelStatsJson).HasColumnType("longtext");

            modelBuilder.Entity<Weapon>().Property(p => p.Description).HasColumnType("longtext");
            modelBuilder.Entity<Weapon>().Property(p => p.ComponentsJson).HasColumnType("longtext");

            modelBuilder.Entity<Relic>().Property(p => p.RewardsJson).HasColumnType("longtext");

            modelBuilder.Entity<Warframe>().Property(p => p.Description).HasColumnType("longtext");

            // índices básicos
            modelBuilder.Entity<Warframe>().HasIndex(x => x.UniqueName).IsUnique(false);
            modelBuilder.Entity<Mod>().HasIndex(x => x.UniqueName).IsUnique(false);
            modelBuilder.Entity<Weapon>().HasIndex(x => x.UniqueName).IsUnique(false);
            modelBuilder.Entity<Relic>().HasIndex(x => x.UniqueName).IsUnique(false);

            // 🔹 Ignorar submodelos no persistentes
            modelBuilder.Ignore<WarframeComponent>();
            modelBuilder.Ignore<DropLocation>();
        }
    }
}
