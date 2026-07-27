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
        public DbSet<InventoryEvent> InventoryEvents => Set<InventoryEvent>();
        public DbSet<SavedBuild> SavedBuilds => Set<SavedBuild>();
        public DbSet<InventoryMetadata> InventoryMetadata => Set<InventoryMetadata>();
        public DbSet<RelicOpening> RelicOpenings => Set<RelicOpening>();
        public DbSet<RelicSyncProfile> RelicSyncProfiles => Set<RelicSyncProfile>();

        public override int SaveChanges()
        {
            AppendInventoryEvents();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            AppendInventoryEvents();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void AppendInventoryEvents()
        {
            ChangeTracker.DetectChanges();
            var events = new List<InventoryEvent>();

            foreach (var entry in ChangeTracker.Entries<UserWarframe>()
                         .Where(x => x.State is EntityState.Added or EntityState.Modified))
            {
                var previous = entry.State == EntityState.Added
                    ? 0 : (entry.OriginalValues.GetValue<bool>(nameof(UserWarframe.Owned)) ? 1 : 0);
                var current = entry.Entity.Owned ? 1 : 0;
                AddEvent(events, entry.Entity.UserId, "warframe", entry.Entity.WarframeUnique,
                    entry.Entity.WarframeUnique, previous, current);
            }

            foreach (var entry in ChangeTracker.Entries<UserWeapon>()
                         .Where(x => x.State is EntityState.Added or EntityState.Modified))
            {
                var previous = entry.State == EntityState.Added
                    ? 0 : (entry.OriginalValues.GetValue<bool>(nameof(UserWeapon.Owned)) ? 1 : 0);
                var current = entry.Entity.Owned ? 1 : 0;
                AddEvent(events, entry.Entity.UserId, "weapon", entry.Entity.WeaponUnique,
                    entry.Entity.WeaponUnique, previous, current);
            }

            foreach (var entry in ChangeTracker.Entries<UserMod>()
                         .Where(x => x.State is EntityState.Added or EntityState.Modified))
            {
                var previousQuantity = entry.State == EntityState.Added
                    ? 0 : entry.OriginalValues.GetValue<int>(nameof(UserMod.Quantity));
                var previousOwned = entry.State != EntityState.Added
                                    && entry.OriginalValues.GetValue<bool>(nameof(UserMod.Owned));
                var previous = previousQuantity > 0 || previousOwned ? Math.Max(1, previousQuantity) : 0;
                var current = entry.Entity.Quantity > 0 || entry.Entity.Owned
                    ? Math.Max(1, entry.Entity.Quantity) : 0;
                AddEvent(events, entry.Entity.UserId, "mod", entry.Entity.ModUnique,
                    entry.Entity.ModUnique, previous, current);
            }

            foreach (var entry in ChangeTracker.Entries<UserRelic>()
                         .Where(x => x.State is EntityState.Added or EntityState.Modified))
            {
                var previous = entry.State == EntityState.Added
                    ? 0 : entry.OriginalValues.GetValue<int>(nameof(UserRelic.Quantity));
                AddEvent(events, entry.Entity.UserId, "relic", entry.Entity.RelicUnique,
                    entry.Entity.RelicUnique, previous, entry.Entity.Quantity);
            }

            foreach (var entry in ChangeTracker.Entries<UserComponent>()
                         .Where(x => x.State is EntityState.Added or EntityState.Modified))
            {
                var previousQuantity = entry.State == EntityState.Added
                    ? 0 : entry.OriginalValues.GetValue<int>(nameof(UserComponent.Quantity));
                var previousOwned = entry.State != EntityState.Added
                                    && entry.OriginalValues.GetValue<bool>(nameof(UserComponent.Owned));
                var previous = previousQuantity > 0 || previousOwned
                    ? Math.Max(1, previousQuantity) : 0;
                var current = entry.Entity.Quantity > 0 || entry.Entity.Owned
                    ? Math.Max(1, entry.Entity.Quantity) : 0;
                AddEvent(events, entry.Entity.UserId, "component", entry.Entity.ParentUnique,
                    entry.Entity.ComponentName, previous, current);
            }

            if (events.Count > 0)
                InventoryEvents.AddRange(events);
        }

        private static void AddEvent(
            ICollection<InventoryEvent> events,
            string userId,
            string category,
            string targetUnique,
            string displayName,
            int previous,
            int current)
        {
            if (string.IsNullOrWhiteSpace(userId) || previous == current)
                return;

            events.Add(new InventoryEvent
            {
                UserId = userId,
                Category = category,
                TargetUnique = targetUnique,
                DisplayName = displayName,
                Action = current > previous ? "Added" : "Removed",
                PreviousValue = previous,
                NewValue = current
            });
        }

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
            modelBuilder.Entity<UserWarframe>().Property(x => x.OwnershipState).HasMaxLength(24);
            modelBuilder.Entity<UserWeapon>().Property(x => x.OwnershipState).HasMaxLength(24);
            modelBuilder.Entity<UserGoal>()
                .HasIndex(x => new { x.UserId, x.TargetType, x.TargetUnique })
                .IsUnique();
            modelBuilder.Entity<UserGoal>().Property(x => x.TargetType).HasMaxLength(32);
            modelBuilder.Entity<UserGoal>().Property(x => x.TargetUnique).HasMaxLength(255);
            modelBuilder.Entity<UserGoal>().Property(x => x.DisplayName).HasMaxLength(255);
            modelBuilder.Entity<InventoryEvent>()
                .HasIndex(x => new { x.UserId, x.OccurredUtc });
            modelBuilder.Entity<InventoryEvent>().Property(x => x.Category).HasMaxLength(32);
            modelBuilder.Entity<InventoryEvent>().Property(x => x.TargetUnique).HasMaxLength(255);
            modelBuilder.Entity<InventoryEvent>().Property(x => x.DisplayName).HasMaxLength(255);
            modelBuilder.Entity<InventoryEvent>().Property(x => x.Action).HasMaxLength(32);
            modelBuilder.Entity<SavedBuild>().HasIndex(x => new { x.UserId, x.UpdatedUtc });
            modelBuilder.Entity<SavedBuild>().Property(x => x.Name).HasMaxLength(120);
            modelBuilder.Entity<SavedBuild>().Property(x => x.TargetType).HasMaxLength(32);
            modelBuilder.Entity<SavedBuild>().Property(x => x.TargetUnique).HasMaxLength(255);
            modelBuilder.Entity<SavedBuild>().Property(x => x.TargetName).HasMaxLength(255);
            modelBuilder.Entity<SavedBuild>().Property(x => x.Tags).HasMaxLength(255);
            modelBuilder.Entity<SavedBuild>().Property(x => x.ModsJson).HasColumnType("longtext");
            modelBuilder.Entity<InventoryMetadata>()
                .HasIndex(x => new { x.UserId, x.Category, x.TargetUnique }).IsUnique();
            modelBuilder.Entity<InventoryMetadata>().Property(x => x.Category).HasMaxLength(32);
            modelBuilder.Entity<InventoryMetadata>().Property(x => x.TargetUnique).HasMaxLength(255);
            modelBuilder.Entity<InventoryMetadata>().Property(x => x.Tags).HasMaxLength(255);
            modelBuilder.Entity<InventoryMetadata>().Property(x => x.Notes).HasColumnType("longtext");
            modelBuilder.Entity<RelicOpening>()
                .HasIndex(x => new { x.UserId, x.OpenedUtc });
            modelBuilder.Entity<RelicOpening>().Property(x => x.RelicName).HasMaxLength(255);
            modelBuilder.Entity<RelicOpening>().Property(x => x.RelicUnique).HasMaxLength(255);
            modelBuilder.Entity<RelicOpening>().Property(x => x.Refinement).HasMaxLength(32);
            modelBuilder.Entity<RelicOpening>().Property(x => x.RewardUnique).HasMaxLength(255);
            modelBuilder.Entity<RelicOpening>().Property(x => x.RewardName).HasMaxLength(255);
            modelBuilder.Entity<RelicSyncProfile>()
                .HasIndex(x => x.UserId).IsUnique();
            modelBuilder.Entity<RelicSyncProfile>().Property(x => x.Provider).HasMaxLength(32);
            modelBuilder.Entity<RelicSyncProfile>().Property(x => x.ProtectedToken)
                .HasColumnType("longtext");
            modelBuilder.Entity<RelicSyncProfile>().Property(x => x.LastStatus).HasMaxLength(32);
            modelBuilder.Entity<RelicSyncProfile>().Property(x => x.LastError)
                .HasMaxLength(500);

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
            modelBuilder.Entity<InventoryEvent>().HasOne<Microsoft.AspNetCore.Identity.IdentityUser>()
                .WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<SavedBuild>().HasOne<Microsoft.AspNetCore.Identity.IdentityUser>()
                .WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<InventoryMetadata>().HasOne<Microsoft.AspNetCore.Identity.IdentityUser>()
                .WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<RelicOpening>().HasOne<Microsoft.AspNetCore.Identity.IdentityUser>()
                .WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<RelicSyncProfile>().HasOne<Microsoft.AspNetCore.Identity.IdentityUser>()
                .WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RelicReward>().HasIndex(x => new { x.RelicUnique, x.ItemUnique }).IsUnique();
            modelBuilder.Entity<RelicReward>().HasOne<Relic>()
                .WithMany().HasForeignKey(x => x.RelicUnique).HasPrincipalKey(x => x.UniqueName)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<DataSyncState>().HasKey(x => x.Id);
        }
    }
}
