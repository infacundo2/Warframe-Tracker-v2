using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WarframeInventory.Data;
using WarframeInventory.Models;

namespace WarframeInventory.Services;

public sealed class DesktopInventorySyncService
{
    private const int MaximumPayloadBytes = 20 * 1024 * 1024;
    private static readonly TimeSpan CaptureLifetime = TimeSpan.FromMinutes(30);
    private static readonly HashSet<string> WarframeSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "Suits", "SpaceSuits", "MechSuits"
    };
    private static readonly HashSet<string> WeaponSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "LongGuns", "Pistols", "Melee", "SpaceGuns", "SpaceMelee",
        "OperatorAmps", "SentinelWeapons"
    };
    private static readonly HashSet<string> ModSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "Upgrades"
    };
    private static readonly HashSet<string> ResourceSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "MiscItems", "Recipes", "Consumables"
    };
    private const string AyaUniqueName = "/Lotus/Types/Items/MiscItems/SchismKey";
    private const string DucatsUniqueName = "/Lotus/Types/Items/MiscItems/PrimeBucks";
    private const long WarframeMasteryXp = 900_000;
    private const long WeaponMasteryXp = 450_000;

    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly object _gate = new();
    private StagedCapture? _capture;

    public DesktopInventorySyncService(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public DesktopCaptureReceipt Stage(string rawJson, string source)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            throw new DesktopInventoryException("La captura llegó vacía.");
        if (System.Text.Encoding.UTF8.GetByteCount(rawJson) > MaximumPayloadBytes)
            throw new DesktopInventoryException("La captura supera el límite seguro de 20 MB.");

        var parsed = Parse(rawJson);
        if (parsed.Items.Count == 0)
            throw new DesktopInventoryException(
                "La captura no contiene entradas ItemType/ItemCount reconocibles.");

        var capture = new StagedCapture(
            Guid.NewGuid(),
            DateTime.UtcNow,
            string.IsNullOrWhiteSpace(source) ? "unknown" : source.Trim(),
            parsed);
        lock (_gate)
            _capture = capture;

        return new DesktopCaptureReceipt(
            capture.Id,
            capture.ReceivedUtc,
            parsed.Items.Count,
            parsed.Items.Sum(x => x.Quantity),
            parsed.Sections.OrderBy(x => x).ToArray(),
            parsed.IsAuthoritative);
    }

    public DesktopCaptureStatus GetStatus()
    {
        lock (_gate)
        {
            if (_capture is null)
                return DesktopCaptureStatus.Empty;
            if (DateTime.UtcNow - _capture.ReceivedUtc > CaptureLifetime)
            {
                _capture = null;
                return DesktopCaptureStatus.Empty;
            }

            return new DesktopCaptureStatus(
                true,
                _capture.Id,
                _capture.ReceivedUtc,
                _capture.Source,
                _capture.Inventory.Items.Count,
                _capture.Inventory.IsAuthoritative);
        }
    }

    public async Task<DesktopInventoryPreview> PreviewAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new DesktopInventoryException("Debes iniciar sesión para analizar la captura.");

        var capture = CurrentCapture();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var warframes = await db.Warframes.AsNoTracking().ToListAsync(cancellationToken);
        var weapons = await db.Weapons.AsNoTracking().ToListAsync(cancellationToken);
        var mods = await db.Mods.AsNoTracking().ToListAsync(cancellationToken);
        var relics = await db.Relics.AsNoTracking().ToListAsync(cancellationToken);
        if (warframes.Count == 0 || weapons.Count == 0
            || mods.Count == 0 || relics.Count == 0)
        {
            throw new DesktopInventoryException(
                "El catálogo local todavía se está descargando. Espera un minuto y vuelve a analizar.");
        }

        var warframeByUnique = warframes.ToDictionary(x => x.UniqueName, StringComparer.Ordinal);
        var weaponByUnique = weapons.ToDictionary(x => x.UniqueName, StringComparer.Ordinal);
        var modByUnique = mods.ToDictionary(x => x.UniqueName, StringComparer.Ordinal);
        var relicByUnique = relics.ToDictionary(x => x.UniqueName, StringComparer.Ordinal);
        var componentByUnique = BuildComponentMap(warframes, weapons);

        var matchedWarframes = new Dictionary<string, DesktopMatchedItem>(StringComparer.Ordinal);
        var matchedWeapons = new Dictionary<string, DesktopMatchedItem>(StringComparer.Ordinal);
        var matchedMods = new Dictionary<string, DesktopMatchedItem>(StringComparer.Ordinal);
        var matchedRelics = new Dictionary<string, DesktopMatchedItem>(StringComparer.Ordinal);
        var matchedComponents = new Dictionary<string, DesktopMatchedComponent>(StringComparer.Ordinal);
        var resources = new Dictionary<string, DesktopMatchedItem>(StringComparer.Ordinal);
        var masteredWarframes = new Dictionary<string, DesktopMasteredItem>(StringComparer.Ordinal);
        var masteredWeapons = new Dictionary<string, DesktopMasteredItem>(StringComparer.Ordinal);
        var unknown = new List<string>();

        foreach (var item in capture.Inventory.Items)
        {
            if (WarframeSections.Contains(item.Section)
                && warframeByUnique.TryGetValue(item.UniqueName, out var warframe))
            {
                matchedWarframes[item.UniqueName] = new(item.UniqueName, warframe.Name, item.Quantity);
            }
            else if (WeaponSections.Contains(item.Section)
                     && weaponByUnique.TryGetValue(item.UniqueName, out var weapon))
            {
                matchedWeapons[item.UniqueName] = new(item.UniqueName, weapon.Name, item.Quantity);
            }
            else if (ModSections.Contains(item.Section)
                     && modByUnique.TryGetValue(item.UniqueName, out var mod))
            {
                matchedMods[item.UniqueName] = new(item.UniqueName, mod.Name, item.Quantity);
            }
            else if (relicByUnique.TryGetValue(item.UniqueName, out var relic))
            {
                matchedRelics[item.UniqueName] = new(item.UniqueName, relic.Name, item.Quantity);
            }
            else if (componentByUnique.TryGetValue(item.UniqueName, out var component))
            {
                matchedComponents[item.UniqueName] = component with { Quantity = item.Quantity };
            }
            else if (ResourceSections.Contains(item.Section))
            {
                resources[item.UniqueName] = new(
                    item.UniqueName,
                    FriendlyName(item.UniqueName),
                    item.Quantity);
            }
            else
            {
                unknown.Add(item.UniqueName);
            }
        }

        foreach (var (uniqueName, xp) in capture.Inventory.Experience)
        {
            if (xp >= WarframeMasteryXp && warframeByUnique.TryGetValue(uniqueName, out var warframe))
                masteredWarframes[uniqueName] = new(uniqueName, warframe.Name, xp);
            else if (xp >= WeaponMasteryXp && weaponByUnique.TryGetValue(uniqueName, out var weapon))
                masteredWeapons[uniqueName] = new(uniqueName, weapon.Name, xp);
        }

        var currentWarframes = await db.UserWarframes.AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToDictionaryAsync(x => x.WarframeUnique, cancellationToken);
        var currentWeapons = await db.UserWeapons.AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToDictionaryAsync(x => x.WeaponUnique, cancellationToken);
        var currentMods = await db.UserMods.AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToDictionaryAsync(x => x.ModUnique, cancellationToken);
        var currentRelics = await db.UserRelics.AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToDictionaryAsync(x => x.RelicUnique, cancellationToken);
        var currentComponents = await db.UserComponents.AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToDictionaryAsync(
                x => $"{x.ParentUnique}\0{x.ComponentName}",
                StringComparer.Ordinal,
                cancellationToken);
        var currentResources = await db.UserResources.AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToDictionaryAsync(x => x.ResourceUnique, cancellationToken);

        var changes = new List<DesktopInventoryChange>();
        AddOwnedChanges(changes, "Warframe", matchedWarframes, currentWarframes
            .ToDictionary(x => x.Key, x => x.Value.Owned ? 1 : 0, StringComparer.Ordinal));
        AddOwnedChanges(changes, "Arma", matchedWeapons, currentWeapons
            .ToDictionary(x => x.Key, x => x.Value.Owned ? 1 : 0, StringComparer.Ordinal));
        AddMasteryChanges(changes, "Maestría Warframe", masteredWarframes, currentWarframes
            .ToDictionary(x => x.Key, x => x.Value.Mastered, StringComparer.Ordinal));
        AddMasteryChanges(changes, "Maestría Arma", masteredWeapons, currentWeapons
            .ToDictionary(x => x.Key, x => x.Value.Mastered, StringComparer.Ordinal));
        AddQuantityChanges(changes, "Mod", matchedMods, currentMods
            .ToDictionary(x => x.Key, x => x.Value.Quantity, StringComparer.Ordinal));
        AddQuantityChanges(changes, "Reliquia", matchedRelics, currentRelics
            .ToDictionary(x => x.Key, x => x.Value.Quantity, StringComparer.Ordinal));
        foreach (var component in matchedComponents.Values)
        {
            var componentKey = $"{component.ParentUnique}\0{component.Name}";
            var previous = currentComponents.TryGetValue(componentKey, out var storedComponent)
                ? storedComponent.Quantity
                : 0;
            if (previous != component.Quantity)
            {
                changes.Add(new DesktopInventoryChange(
                    "Componente",
                    component.UniqueName,
                    component.Name,
                    previous,
                    component.Quantity));
            }
        }
        AddQuantityChanges(changes, "Recurso", resources, currentResources
            .ToDictionary(x => x.Key, x => x.Value.Quantity, StringComparer.Ordinal));

        return new DesktopInventoryPreview(
            userId,
            capture.Id,
            DateTime.UtcNow,
            capture.ReceivedUtc,
            capture.Source,
            capture.Inventory.IsAuthoritative,
            capture.Inventory.Account,
            matchedWarframes,
            matchedWeapons,
            masteredWarframes,
            masteredWeapons,
            matchedMods,
            matchedRelics,
            matchedComponents,
            resources,
            unknown.OrderBy(x => x).Take(250).ToArray(),
            changes.OrderBy(x => x.Category).ThenBy(x => x.Name).ToArray());
    }

    public async Task<DesktopApplyResult> ApplyAsync(
        string userId,
        DesktopInventoryPreview preview,
        CancellationToken cancellationToken = default)
    {
        if (preview.UserId != userId)
            throw new DesktopInventoryException("La vista previa pertenece a otro usuario.");
        if (DateTime.UtcNow - preview.CreatedUtc > CaptureLifetime)
            throw new DesktopInventoryException("La vista previa venció. Captura otra vez el inventario.");
        var capture = CurrentCapture();
        if (capture.Id != preview.CaptureId)
            throw new DesktopInventoryException("Hay una captura más reciente. Analízala antes de aplicar.");

        // Cada reintento usa un contexto nuevo y toda la aplicación queda dentro de
        // una única transacción atómica. Esto es obligatorio con EnableRetryOnFailure.
        await using var strategyDb = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var strategy = strategyDb.Database.CreateExecutionStrategy();
        var changed = await strategy.ExecuteAsync(async () =>
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database
                .BeginTransactionAsync(cancellationToken);

            var attemptChanged = 0;
            attemptChanged += await ApplyWarframesAsync(db, userId, preview, cancellationToken);
            attemptChanged += await ApplyWeaponsAsync(db, userId, preview, cancellationToken);
            attemptChanged += await ApplyModsAsync(db, userId, preview, cancellationToken);
            attemptChanged += await ApplyRelicsAsync(db, userId, preview, cancellationToken);
            attemptChanged += await ApplyComponentsAsync(db, userId, preview, cancellationToken);
            attemptChanged += await ApplyResourcesAsync(db, userId, preview, cancellationToken);
            ApplyAccount(db, userId, preview.Account);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return attemptChanged;
        });

        // Se descarta una sola vez y únicamente después de un commit exitoso.
        lock (_gate)
        {
            if (_capture?.Id == preview.CaptureId)
                _capture = null;
        }

        return new DesktopApplyResult(changed, DateTime.UtcNow);
    }

    private StagedCapture CurrentCapture()
    {
        lock (_gate)
        {
            if (_capture is null || DateTime.UtcNow - _capture.ReceivedUtc > CaptureLifetime)
            {
                _capture = null;
                throw new DesktopInventoryException(
                    "No hay una captura reciente. Provoca una pantalla de carga dentro de Warframe.");
            }
            return _capture;
        }
    }

    private static ParsedInventory Parse(string rawJson)
    {
        using var initial = JsonDocument.Parse(rawJson, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
            MaxDepth = 128
        });
        var root = initial.RootElement;
        JsonDocument? unwrapped = null;
        if (root.ValueKind == JsonValueKind.String)
        {
            unwrapped = JsonDocument.Parse(root.GetString() ?? "{}");
            root = unwrapped.RootElement;
        }

        var items = new Dictionary<(string Section, string Unique), int>();
        var experience = new Dictionary<string, long>(StringComparer.Ordinal);
        var sections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Walk(root, "Root", items, sections, experience);
        var account = new DesktopAccountValues(
            FindLong(root, "RegularCredits", "Credits"),
            FindLong(root, "FusionPoints", "Endo"),
            FindLong(root, "PremiumCredits", "Platinum"),
            FindLong(root, "PrimeBucks", "Ducats") ?? FindItemQuantity(items, DucatsUniqueName),
            FindLong(root, "Aya") ?? FindItemQuantity(items, AyaUniqueName),
            ToNullableInt(FindLong(root, "PlayerLevel", "MasteryRank")));
        unwrapped?.Dispose();

        var entries = items.Select(x => new ParsedItem(x.Key.Section, x.Key.Unique, x.Value))
            .OrderBy(x => x.Section).ThenBy(x => x.UniqueName).ToArray();
        var hasEquipment = sections.Overlaps(WarframeSections)
                           || sections.Overlaps(WeaponSections);
        var hasInventory = sections.Contains("MiscItems") || sections.Contains("Upgrades");
        return new ParsedInventory(entries, sections, experience, account, hasEquipment && hasInventory);
    }

    private static void Walk(
        JsonElement element,
        string section,
        IDictionary<(string Section, string Unique), int> items,
        ISet<string> sections,
        IDictionary<string, long> experience)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (TryGetString(element, "ItemType", out var unique)
                && !string.IsNullOrWhiteSpace(unique))
            {
                if (TryGetLong(element, "XP", out var xp))
                    experience[unique] = Math.Max(
                        experience.TryGetValue(unique, out var previousXp) ? previousXp : 0,
                        xp);

                // XPInfo is historical Codex/mastery data, not an owned inventory stack.
                if (!section.Equals("XPInfo", StringComparison.OrdinalIgnoreCase))
                {
                    var quantity = TryGetInt(element, "ItemCount", out var count) ? Math.Max(0, count) : 1;
                    var key = (section, unique);
                    items[key] = checked((items.TryGetValue(key, out var previous) ? previous : 0)
                                         + quantity);
                    sections.Add(section);
                }
            }
            foreach (var property in element.EnumerateObject())
                Walk(property.Value, property.Name, items, sections, experience);
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
                Walk(child, section, items, sections, experience);
        }
    }

    private static bool TryGetString(JsonElement element, string name, out string value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.String)
            {
                value = property.Value.GetString() ?? "";
                return true;
            }
        }
        value = "";
        return false;
    }

    private static bool TryGetInt(JsonElement element, string name, out int value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                continue;
            if (property.Value.TryGetInt32(out value))
                return true;
        }
        value = 0;
        return false;
    }

    private static bool TryGetLong(JsonElement element, string name, out long value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                && property.Value.TryGetInt64(out value))
                return true;
        }
        value = 0;
        return false;
    }

    private static long? FindItemQuantity(
        IReadOnlyDictionary<(string Section, string Unique), int> items,
        string uniqueName)
    {
        var matches = items.Where(x => x.Key.Unique.Equals(uniqueName, StringComparison.Ordinal))
            .Select(x => (long)x.Value).ToArray();
        return matches.Length == 0 ? null : matches.Sum();
    }

    private static int? ToNullableInt(long? value)
        => value is null ? null : checked((int)value.Value);

    private static long? FindLong(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return null;
        foreach (var property in root.EnumerateObject())
        {
            if (!names.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                continue;
            if (property.Value.TryGetInt64(out var value))
                return value;
        }
        return null;
    }

    private static Dictionary<string, DesktopMatchedComponent> BuildComponentMap(
        IEnumerable<Warframe> warframes,
        IEnumerable<Weapon> weapons)
    {
        var result = new Dictionary<string, DesktopMatchedComponent>(StringComparer.Ordinal);
        foreach (var parent in warframes.Select(x => (x.UniqueName, x.Name, x.ComponentsJson))
                     .Concat(weapons.Select(x => (x.UniqueName, x.Name, x.ComponentsJson))))
        {
            if (string.IsNullOrWhiteSpace(parent.ComponentsJson))
                continue;
            try
            {
                using var document = JsonDocument.Parse(parent.ComponentsJson);
                AddComponents(document.RootElement, parent.UniqueName, result);
            }
            catch (JsonException)
            {
                // A malformed catalog component must not invalidate the full capture.
            }
        }
        return result;
    }

    private static void AddComponents(
        JsonElement element,
        string parentUnique,
        IDictionary<string, DesktopMatchedComponent> output)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (TryGetString(element, "uniqueName", out var unique)
                && TryGetString(element, "name", out var name)
                && !string.IsNullOrWhiteSpace(unique))
            {
                output.TryAdd(unique, new(unique, parentUnique, name, 0));
            }
            foreach (var property in element.EnumerateObject())
                AddComponents(property.Value, parentUnique, output);
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
                AddComponents(child, parentUnique, output);
        }
    }

    private static string FriendlyName(string uniqueName)
    {
        var segment = uniqueName.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()
                      ?? uniqueName;
        return string.Concat(segment.Select((character, index) =>
            index > 0 && char.IsUpper(character) && !char.IsUpper(segment[index - 1])
                ? " " + character
                : character.ToString()));
    }

    private static void AddOwnedChanges(
        ICollection<DesktopInventoryChange> output,
        string category,
        IReadOnlyDictionary<string, DesktopMatchedItem> incoming,
        IReadOnlyDictionary<string, int> current)
    {
        foreach (var item in incoming.Values)
        {
            var previous = current.GetValueOrDefault(item.UniqueName);
            if (previous == 0)
                output.Add(new(category, item.UniqueName, item.Name, previous, 1));
        }
    }

    private static void AddQuantityChanges(
        ICollection<DesktopInventoryChange> output,
        string category,
        IReadOnlyDictionary<string, DesktopMatchedItem> incoming,
        IReadOnlyDictionary<string, int> current)
    {
        foreach (var item in incoming.Values)
        {
            var previous = current.GetValueOrDefault(item.UniqueName);
            if (previous != item.Quantity)
                output.Add(new(category, item.UniqueName, item.Name, previous, item.Quantity));
        }
    }

    private static void AddMasteryChanges(
        ICollection<DesktopInventoryChange> output,
        string category,
        IReadOnlyDictionary<string, DesktopMasteredItem> incoming,
        IReadOnlyDictionary<string, bool> current)
    {
        foreach (var item in incoming.Values.Where(x => !current.GetValueOrDefault(x.UniqueName)))
            output.Add(new(category, item.UniqueName, item.Name, 0, 1));
    }

    private static async Task<int> ApplyWarframesAsync(
        ApplicationDbContext db, string userId, DesktopInventoryPreview preview,
        CancellationToken cancellationToken)
    {
        var stored = await db.UserWarframes.Where(x => x.UserId == userId)
            .ToDictionaryAsync(x => x.WarframeUnique, cancellationToken);
        var changed = 0;
        if (preview.IsAuthoritative)
        {
            foreach (var entry in stored.Values.Where(x => x.Owned
                         && !preview.Warframes.ContainsKey(x.WarframeUnique)))
            {
                entry.Owned = false;
                entry.OwnershipState = "missing";
                changed++;
            }
        }
        foreach (var item in preview.Warframes.Values)
        {
            if (!stored.TryGetValue(item.UniqueName, out var entry))
            {
                entry = new UserWarframe
                {
                    UserId = userId, WarframeUnique = item.UniqueName,
                    Owned = true, OwnershipState = "built"
                };
                db.UserWarframes.Add(entry);
                stored[item.UniqueName] = entry;
                changed++;
            }
            else if (!entry.Owned || entry.OwnershipState != "built")
            {
                entry.Owned = true;
                entry.OwnershipState = "built";
                changed++;
            }
        }
        foreach (var item in preview.MasteredWarframes.Values)
        {
            if (!stored.TryGetValue(item.UniqueName, out var entry))
            {
                entry = new UserWarframe
                {
                    UserId = userId, WarframeUnique = item.UniqueName,
                    Owned = preview.Warframes.ContainsKey(item.UniqueName),
                    OwnershipState = preview.Warframes.ContainsKey(item.UniqueName) ? "built" : "missing"
                };
                db.UserWarframes.Add(entry);
                stored[item.UniqueName] = entry;
            }
            if (!entry.Mastered || entry.MasteryXp < item.Xp)
            {
                entry.Mastered = true;
                entry.MasteryXp = Math.Max(entry.MasteryXp, item.Xp);
                changed++;
            }
        }
        return changed;
    }

    private static async Task<int> ApplyWeaponsAsync(
        ApplicationDbContext db, string userId, DesktopInventoryPreview preview,
        CancellationToken cancellationToken)
    {
        var stored = await db.UserWeapons.Where(x => x.UserId == userId)
            .ToDictionaryAsync(x => x.WeaponUnique, cancellationToken);
        var changed = 0;
        if (preview.IsAuthoritative)
        {
            foreach (var entry in stored.Values.Where(x => x.Owned
                         && !preview.Weapons.ContainsKey(x.WeaponUnique)))
            {
                entry.Owned = false;
                entry.OwnershipState = "missing";
                changed++;
            }
        }
        foreach (var item in preview.Weapons.Values)
        {
            if (!stored.TryGetValue(item.UniqueName, out var entry))
            {
                entry = new UserWeapon
                {
                    UserId = userId, WeaponUnique = item.UniqueName,
                    Owned = true, OwnershipState = "built"
                };
                db.UserWeapons.Add(entry);
                stored[item.UniqueName] = entry;
                changed++;
            }
            else if (!entry.Owned || entry.OwnershipState != "built")
            {
                entry.Owned = true;
                entry.OwnershipState = "built";
                changed++;
            }
        }
        foreach (var item in preview.MasteredWeapons.Values)
        {
            if (!stored.TryGetValue(item.UniqueName, out var entry))
            {
                entry = new UserWeapon
                {
                    UserId = userId, WeaponUnique = item.UniqueName,
                    Owned = preview.Weapons.ContainsKey(item.UniqueName),
                    OwnershipState = preview.Weapons.ContainsKey(item.UniqueName) ? "built" : "missing"
                };
                db.UserWeapons.Add(entry);
                stored[item.UniqueName] = entry;
            }
            if (!entry.Mastered || entry.MasteryXp < item.Xp)
            {
                entry.Mastered = true;
                entry.MasteryXp = Math.Max(entry.MasteryXp, item.Xp);
                changed++;
            }
        }
        return changed;
    }

    private static async Task<int> ApplyModsAsync(
        ApplicationDbContext db, string userId, DesktopInventoryPreview preview,
        CancellationToken cancellationToken)
    {
        var stored = await db.UserMods.Where(x => x.UserId == userId)
            .ToDictionaryAsync(x => x.ModUnique, cancellationToken);
        var changed = 0;
        foreach (var item in preview.Mods.Values)
        {
            if (!stored.TryGetValue(item.UniqueName, out var entry))
            {
                db.UserMods.Add(new UserMod
                {
                    UserId = userId, ModUnique = item.UniqueName,
                    Owned = item.Quantity > 0, Quantity = item.Quantity
                });
                changed++;
            }
            else if (entry.Quantity != item.Quantity || entry.Owned != (item.Quantity > 0))
            {
                entry.Quantity = item.Quantity;
                entry.Owned = item.Quantity > 0;
                changed++;
            }
        }
        return changed;
    }

    private static async Task<int> ApplyRelicsAsync(
        ApplicationDbContext db, string userId, DesktopInventoryPreview preview,
        CancellationToken cancellationToken)
    {
        var stored = await db.UserRelics.Where(x => x.UserId == userId)
            .ToDictionaryAsync(x => x.RelicUnique, cancellationToken);
        var changed = 0;
        foreach (var item in preview.Relics.Values)
        {
            if (!stored.TryGetValue(item.UniqueName, out var entry))
            {
                db.UserRelics.Add(new UserRelic
                    { UserId = userId, RelicUnique = item.UniqueName, Quantity = item.Quantity });
                changed++;
            }
            else if (entry.Quantity != item.Quantity)
            {
                entry.Quantity = item.Quantity;
                changed++;
            }
        }
        return changed;
    }

    private static async Task<int> ApplyComponentsAsync(
        ApplicationDbContext db, string userId, DesktopInventoryPreview preview,
        CancellationToken cancellationToken)
    {
        var stored = await db.UserComponents.Where(x => x.UserId == userId)
            .ToDictionaryAsync(
                x => $"{x.ParentUnique}\0{x.ComponentName}",
                StringComparer.Ordinal,
                cancellationToken);
        var changed = 0;
        foreach (var item in preview.Components.Values)
        {
            var key = $"{item.ParentUnique}\0{item.Name}";
            if (!stored.TryGetValue(key, out var entry))
            {
                db.UserComponents.Add(new UserComponent
                {
                    UserId = userId, ParentUnique = item.ParentUnique,
                    ComponentName = item.Name, Owned = item.Quantity > 0,
                    Quantity = item.Quantity
                });
                changed++;
            }
            else if (entry.Quantity != item.Quantity || entry.Owned != (item.Quantity > 0))
            {
                entry.Quantity = item.Quantity;
                entry.Owned = item.Quantity > 0;
                changed++;
            }
        }
        return changed;
    }

    private static async Task<int> ApplyResourcesAsync(
        ApplicationDbContext db, string userId, DesktopInventoryPreview preview,
        CancellationToken cancellationToken)
    {
        var stored = await db.UserResources.Where(x => x.UserId == userId)
            .ToDictionaryAsync(x => x.ResourceUnique, cancellationToken);
        var changed = 0;
        foreach (var item in preview.Resources.Values)
        {
            if (!stored.TryGetValue(item.UniqueName, out var entry))
            {
                db.UserResources.Add(new UserResource
                {
                    UserId = userId, ResourceUnique = item.UniqueName,
                    DisplayName = item.Name, Quantity = item.Quantity
                });
                changed++;
            }
            else if (entry.Quantity != item.Quantity || entry.DisplayName != item.Name)
            {
                entry.Quantity = item.Quantity;
                entry.DisplayName = item.Name;
                changed++;
            }
        }
        return changed;
    }

    private static void ApplyAccount(
        ApplicationDbContext db,
        string userId,
        DesktopAccountValues account)
    {
        if (account.Credits is null && account.Endo is null && account.Platinum is null
            && account.Ducats is null && account.Aya is null && account.MasteryRank is null)
            return;
        var snapshot = db.AlecaAccountSnapshots.Local.FirstOrDefault(x => x.UserId == userId)
                       ?? db.AlecaAccountSnapshots.SingleOrDefault(x => x.UserId == userId);
        if (snapshot is null)
        {
            snapshot = new AlecaAccountSnapshot { UserId = userId };
            db.AlecaAccountSnapshots.Add(snapshot);
        }
        if (account.Credits is not null) snapshot.Credits = account.Credits;
        if (account.Endo is not null) snapshot.Endo = checked((int)account.Endo.Value);
        if (account.Platinum is not null) snapshot.Platinum = checked((int)account.Platinum.Value);
        if (account.Ducats is not null) snapshot.Ducats = checked((int)account.Ducats.Value);
        if (account.Aya is not null) snapshot.Aya = checked((int)account.Aya.Value);
        if (account.MasteryRank is not null) snapshot.MasteryRank = account.MasteryRank;
        snapshot.SyncedUtc = DateTime.UtcNow;
    }

    private sealed record StagedCapture(
        Guid Id,
        DateTime ReceivedUtc,
        string Source,
        ParsedInventory Inventory);

    private sealed record ParsedInventory(
        IReadOnlyList<ParsedItem> Items,
        IReadOnlySet<string> Sections,
        IReadOnlyDictionary<string, long> Experience,
        DesktopAccountValues Account,
        bool IsAuthoritative);

    private sealed record ParsedItem(string Section, string UniqueName, int Quantity);
}

public sealed record DesktopCaptureReceipt(
    Guid CaptureId,
    DateTime ReceivedUtc,
    int DistinctItems,
    int TotalQuantity,
    IReadOnlyList<string> Sections,
    bool IsAuthoritative);

public sealed record DesktopCaptureStatus(
    bool HasCapture,
    Guid? CaptureId,
    DateTime? ReceivedUtc,
    string Source,
    int DistinctItems,
    bool IsAuthoritative)
{
    public static readonly DesktopCaptureStatus Empty =
        new(false, null, null, "", 0, false);
}

public sealed record DesktopAccountValues(
    long? Credits,
    long? Endo,
    long? Platinum,
    long? Ducats,
    long? Aya,
    int? MasteryRank);

public sealed record DesktopMatchedItem(
    string UniqueName,
    string Name,
    int Quantity);

public sealed record DesktopMasteredItem(
    string UniqueName,
    string Name,
    long Xp);

public sealed record DesktopMatchedComponent(
    string UniqueName,
    string ParentUnique,
    string Name,
    int Quantity);

public sealed record DesktopInventoryChange(
    string Category,
    string UniqueName,
    string Name,
    int PreviousQuantity,
    int NewQuantity);

public sealed record DesktopInventoryPreview(
    string UserId,
    Guid CaptureId,
    DateTime CreatedUtc,
    DateTime CapturedUtc,
    string Source,
    bool IsAuthoritative,
    DesktopAccountValues Account,
    IReadOnlyDictionary<string, DesktopMatchedItem> Warframes,
    IReadOnlyDictionary<string, DesktopMatchedItem> Weapons,
    IReadOnlyDictionary<string, DesktopMasteredItem> MasteredWarframes,
    IReadOnlyDictionary<string, DesktopMasteredItem> MasteredWeapons,
    IReadOnlyDictionary<string, DesktopMatchedItem> Mods,
    IReadOnlyDictionary<string, DesktopMatchedItem> Relics,
    IReadOnlyDictionary<string, DesktopMatchedComponent> Components,
    IReadOnlyDictionary<string, DesktopMatchedItem> Resources,
    IReadOnlyList<string> UnknownItems,
    IReadOnlyList<DesktopInventoryChange> Changes);

public sealed record DesktopApplyResult(int ChangedRecords, DateTime AppliedUtc);

public sealed class DesktopInventoryException : Exception
{
    public DesktopInventoryException(string message) : base(message)
    {
    }
}
