using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;

namespace WarframeInventory.Services;

public sealed class ResourceCatalogService
{
    private const string CacheKey = "catalog:resources:es:v1";
    private static readonly Regex LocationPattern = new(
        @"Ubicaci[oó]n:\s*(?<location>[^\r\n]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<ResourceRecommendation>>
        Recommendations = new Dictionary<string, IReadOnlyList<ResourceRecommendation>>(
            StringComparer.Ordinal)
        {
            ["/Lotus/Types/Items/MiscItems/OrokinCell"] =
            [
                new("General Sargas Ruk", "Jefe", "Tethys, Saturno",
                    "Fuente destacada de Células Orokin.", 2.58),
                new("Teniente Lech Kril", "Jefe", "War, Ceres",
                    "Fuente destacada de Células Orokin.", 2.58),
                new("Otros jefes y el Stalker", "Jefe", "Sistema Origen",
                    "Pueden soltar Células Orokin; la tasa puede depender del encuentro.", null)
            ],
            ["/Lotus/Types/Items/MiscItems/NeuralSensor"] =
            [
                new("Alad V", "Jefe", "Themisto, Júpiter",
                    "Ruta corta y popular; la tasa exacta del recurso no está publicada.", null)
            ],
            ["/Lotus/Types/Items/MiscItems/Neurode"] =
            [
                new("Lephantis", "Jefe", "Magnacidium, Deimos",
                    "Puede entregar Neurodos; la tasa exacta no está publicada.", null)
            ],
            ["/Lotus/Types/Items/MiscItems/ControlModule"] =
            [
                new("La Manada de Hienas", "Jefe", "Psamathe, Neptuno",
                    "Fuente de Módulos de Control; la tasa exacta no está publicada.", null)
            ],
            ["/Lotus/Types/Items/MiscItems/Gallium"] =
            [
                new("Tyl Regor", "Jefe", "Titania, Urano",
                    "Fuente regional de Galio; la tasa exacta no está publicada.", null),
                new("Teniente Lech Kril", "Jefe", "War, Marte",
                    "Fuente regional de Galio; la tasa exacta no está publicada.", null)
            ],
            ["/Lotus/Types/Items/MiscItems/Morphic"] =
            [
                new("Capitán Vor", "Jefe", "Tolstoj, Mercurio",
                    "Fuente regional de Mórficos; la tasa exacta no está publicada.", null)
            ]
        };

    public ResourceCatalogService(HttpClient http, IMemoryCache cache)
    {
        _http = http;
        _cache = cache;
    }

    public async Task<IReadOnlyList<ResourceInfo>> GetResourcesAsync(
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<ResourceInfo>? cached)
            && cached is not null)
            return cached;

        var items = await _http.GetFromJsonAsync<JsonArray>(
            "items/?language=es", cancellationToken) ?? [];
        var resources = items
            .OfType<JsonObject>()
            .Select(ParseResource)
            .Where(resource => resource is not null)
            .Cast<ResourceInfo>()
            .GroupBy(resource => resource.UniqueName, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(resource => resource.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        _cache.Set(CacheKey, resources, TimeSpan.FromHours(12));
        return resources;
    }

    public async Task<ResourceInfo?> GetResourceAsync(
        string uniqueName,
        CancellationToken cancellationToken = default)
        => (await GetResourcesAsync(cancellationToken))
            .FirstOrDefault(resource => resource.UniqueName == uniqueName);

    private static ResourceInfo? ParseResource(JsonObject item)
    {
        var uniqueName = Text(item, "uniqueName");
        var name = Text(item, "name");
        var description = Text(item, "description");
        var type = Text(item, "type");
        if (!uniqueName.StartsWith("/Lotus/Types/Items/MiscItems/", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(name)
            || (!type.Equals("Resource", StringComparison.OrdinalIgnoreCase)
                && !LocationPattern.IsMatch(description)))
            return null;

        var location = LocationPattern.Match(description).Groups["location"].Value.Trim();
        var cleanDescription = LocationPattern.Replace(description, "").Trim();
        var drops = item["drops"] is JsonArray dropNodes
            ? dropNodes.OfType<JsonObject>()
                .Select(drop => new ResourceDrop(
                    Text(drop, "location"),
                    Text(drop, "type"),
                    Text(drop, "rarity"),
                    Number(drop, "chance"),
                    SourceKind(Text(drop, "location"))))
                .Where(drop => !string.IsNullOrWhiteSpace(drop.Location))
                .Distinct()
                .OrderByDescending(drop => drop.Chance)
                .ThenBy(drop => drop.Location, StringComparer.CurrentCultureIgnoreCase)
                .ToList()
            : [];

        var recommendations = Recommendations.GetValueOrDefault(uniqueName, []);
        drops.AddRange(recommendations
            .Where(recommendation => recommendation.Chance is not null)
            .Select(recommendation => new ResourceDrop(
                $"{recommendation.Name} · {recommendation.Location}",
                name,
                "Recurso raro",
                recommendation.Chance!.Value,
                "Enemigo"))
            .Where(recommended => drops.All(drop =>
                !drop.Location.Equals(recommended.Location, StringComparison.OrdinalIgnoreCase))));
        drops = drops.OrderByDescending(drop => drop.Chance)
            .ThenBy(drop => drop.Location, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var category = !string.IsNullOrWhiteSpace(location)
            ? "Planetario"
            : drops.Count > 0 ? "Drop específico" : "Especial";
        return new ResourceInfo(
            uniqueName,
            name,
            cleanDescription,
            Text(item, "imageName"),
            type,
            category,
            location,
            drops,
            recommendations);
    }

    private static string SourceKind(string location)
        => location.Contains('/', StringComparison.Ordinal)
           || location.Contains("Rotation", StringComparison.OrdinalIgnoreCase)
           || location.Contains("Caches", StringComparison.OrdinalIgnoreCase)
           || location.Contains("Mission", StringComparison.OrdinalIgnoreCase)
            ? "Misión"
            : "Enemigo";

    private static string Text(JsonObject node, string property)
        => node[property] is JsonValue value
           && value.TryGetValue<string>(out var text)
            ? text ?? ""
            : "";

    private static double Number(JsonObject node, string property)
        => node[property] is JsonValue value
           && value.TryGetValue<double>(out var number)
            ? number
            : 0d;
}

public sealed record ResourceInfo(
    string UniqueName,
    string Name,
    string Description,
    string ImageName,
    string Type,
    string Category,
    string LocationSummary,
    IReadOnlyList<ResourceDrop> Drops,
    IReadOnlyList<ResourceRecommendation> Recommendations);

public sealed record ResourceDrop(
    string Location,
    string Type,
    string Rarity,
    double Chance,
    string SourceKind);

public sealed record ResourceRecommendation(
    string Name,
    string SourceKind,
    string Location,
    string Note,
    double? Chance);
