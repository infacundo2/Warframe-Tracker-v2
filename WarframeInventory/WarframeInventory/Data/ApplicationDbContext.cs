using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WarframeInventory.Models;

namespace WarframeInventory.Data
{
    // Incluye soporte de usuarios (Identity) + tus entidades de Warframe
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        // =============================
        // 🔹 ENTIDADES PRINCIPALES
        // =============================
        public DbSet<Warframe> Warframes => Set<Warframe>();
        public DbSet<Mod> Mods => Set<Mod>();
        public DbSet<Weapon> Weapons => Set<Weapon>();
        public DbSet<Relic> Relics => Set<Relic>();
       

        // =============================
        // 🔹 ENTIDADES DE INVENTARIO DE USUARIO
        // =============================
        public DbSet<UserWarframe> UserWarframes => Set<UserWarframe>();
        public DbSet<UserWeapon> UserWeapons => Set<UserWeapon>();
        public DbSet<UserComponent> UserComponents => Set<UserComponent>();
        public DbSet<UserRelic> UserRelics => Set<UserRelic>();
        public DbSet<UserMod> UserMods => Set<UserMod>();
        public DbSet<RelicReward> RelicRewards => Set<RelicReward>();
        public DbSet<DataSyncState> DataSyncStates => Set<DataSyncState>();
        public DbSet<UserGoal> UserGoals => Set<UserGoal>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // =============================
            // TEXTOS LARGOS (MySQL LONGTEXT)
            // =============================
            modelBuilder.Entity<Mod>().Property(p => p.Description).HasColumnType("longtext");
            modelBuilder.Entity<Mod>().Property(p => p.LevelStatsJson).HasColumnType("longtext");
            modelBuilder.Entity<Weapon>().Property(p => p.Description).HasColumnType("longtext");
            modelBuilder.Entity<Weapon>().Property(p => p.ComponentsJson).HasColumnType("longtext");
            modelBuilder.Entity<Relic>().Property(p => p.RewardsJson).HasColumnType("longtext");
            modelBuilder.Entity<Warframe>().Property(p => p.Description).HasColumnType("longtext");

            // =============================
            // ÍNDICES PRINCIPALES
            // =============================
            modelBuilder.Entity<Warframe>().HasIndex(x => x.UniqueName).IsUnique();
            modelBuilder.Entity<Mod>().HasIndex(x => x.UniqueName).IsUnique();
            modelBuilder.Entity<Weapon>().HasIndex(x => x.UniqueName).IsUnique();
            modelBuilder.Entity<Relic>().HasIndex(x => x.UniqueName).IsUnique();
            modelBuilder.Entity<Warframe>().Property(x => x.UniqueName).HasMaxLength(255);
            modelBuilder.Entity<Mod>().Property(x => x.UniqueName).HasMaxLength(255);
            modelBuilder.Entity<Weapon>().Property(x => x.UniqueName).HasMaxLength(255);
            modelBuilder.Entity<Relic>().Property(x => x.UniqueName).HasMaxLength(255);

            // =============================
            // IGNORAR MODELOS NO PERSISTENTES
            // =============================
            modelBuilder.Ignore<WarframeComponent>();
            modelBuilder.Ignore<DropLocation>();

            // =============================
            // CONFIGURAR TABLAS DE USUARIO
            // =============================
            modelBuilder.Entity<UserWarframe>().HasIndex(x => new { x.UserId, x.WarframeUnique }).IsUnique();
            modelBuilder.Entity<UserWeapon>().HasIndex(x => new { x.UserId, x.WeaponUnique }).IsUnique();
            modelBuilder.Entity<UserComponent>().HasIndex(x => new { x.UserId, x.ParentUnique, x.ComponentName }).IsUnique();
            modelBuilder.Entity<UserRelic>().HasIndex(x => new { x.UserId, x.RelicUnique }).IsUnique();
            modelBuilder.Entity<UserMod>().HasIndex(x => new { x.UserId, x.ModUnique }).IsUnique();
            modelBuilder.Entity<UserGoal>()
                .HasIndex(x => new { x.UserId, x.TargetType, x.TargetUnique })
                .IsUnique();
            modelBuilder.Entity<UserGoal>().Property(x => x.TargetType).HasMaxLength(32);
            modelBuilder.Entity<UserGoal>().Property(x => x.TargetUnique).HasMaxLength(255);
            modelBuilder.Entity<UserGoal>().Property(x => x.DisplayName).HasMaxLength(255);

            modelBuilder.Entity<UserWarframe>().HasOne<Microsoft.AspNetCore.Identity.IdentityUser>()
                .WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<UserWeapon>().HasOne<Microsoft.AspNetCore.Identity.IdentityUser>()
                .WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<UserComponent>().HasOne<Microsoft.AspNetCore.Identity.IdentityUser>()
                .WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<UserRelic>().HasOne<Microsoft.AspNetCore.Identity.IdentityUser>()
                .WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<UserMod>().HasOne<Microsoft.AspNetCore.Identity.IdentityUser>()
                .WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<UserGoal>().HasOne<Microsoft.AspNetCore.Identity.IdentityUser>()
                .WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RelicReward>().HasIndex(x => new { x.RelicUnique, x.ItemUnique }).IsUnique();
            modelBuilder.Entity<RelicReward>().HasOne<Relic>()
                .WithMany().HasForeignKey(x => x.RelicUnique).HasPrincipalKey(x => x.UniqueName)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<DataSyncState>().HasKey(x => x.Id);
        }
    }
}
