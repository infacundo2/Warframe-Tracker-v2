using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;

namespace WarframeInventory.Services;

public sealed class ResourceCatalogService
{
    private const string CacheKey = "catalog:resources:en:v2";
    private static readonly Regex LocationPattern = new(
        @"(?:Location|Ubicaci[oó]n):\s*(?<location>[^\r\n]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<ResourceRecommendation>>
        Recommendations = new Dictionary<string, IReadOnlyList<ResourceRecommendation>>(
            StringComparer.Ordinal)
        {
            ["/Lotus/Types/Items/MiscItems/OrokinCell"] =
            [
                new("General Sargas Ruk", "Boss", "Tethys, Saturn",
                    "Highlighted Orokin Cell source.", 2.58),
                new("Lieutenant Lech Kril", "Boss", "War, Ceres",
                    "Highlighted Orokin Cell source.", 2.58),
                new("Other bosses and the Stalker", "Boss", "Origin System",
                    "They can drop Orokin Cells; the rate may depend on the encounter.", null)
            ],
            ["/Lotus/Types/Items/MiscItems/NeuralSensor"] =
            [
                new("Alad V", "Boss", "Themisto, Jupiter",
                    "Short, popular route; the exact resource rate is not published.", null)
            ],
            ["/Lotus/Types/Items/MiscItems/Neurode"] =
            [
                new("Lephantis", "Boss", "Magnacidium, Deimos",
                    "Can drop Neurodes; the exact rate is not published.", null)
            ],
            ["/Lotus/Types/Items/MiscItems/ControlModule"] =
            [
                new("The Hyena Pack", "Boss", "Psamathe, Neptune",
                    "Control Module source; the exact rate is not published.", null)
            ],
            ["/Lotus/Types/Items/MiscItems/Gallium"] =
            [
                new("Tyl Regor", "Boss", "Titania, Uranus",
                    "Regional Gallium source; the exact rate is not published.", null),
                new("Lieutenant Lech Kril", "Boss", "War, Mars",
                    "Regional Gallium source; the exact rate is not published.", null)
            ],
            ["/Lotus/Types/Items/MiscItems/Morphic"] =
            [
                new("Captain Vor", "Boss", "Tolstoj, Mercury",
                    "Regional Morphics source; the exact rate is not published.", null)
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
            "items/?language=en", cancellationToken) ?? [];
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
                "Rare resource",
                recommendation.Chance!.Value,
                "Enemy"))
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
           || location.Contains("Rotación", StringComparison.OrdinalIgnoreCase)
           || location.Contains("Caches", StringComparison.OrdinalIgnoreCase)
           || location.Contains("Alijos", StringComparison.OrdinalIgnoreCase)
           || location.Contains("Mission", StringComparison.OrdinalIgnoreCase)
           || location.Contains("Misión", StringComparison.OrdinalIgnoreCase)
            ? "Mission"
            : "Enemy";

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
