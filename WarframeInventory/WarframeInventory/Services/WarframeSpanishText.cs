using System.Text.RegularExpressions;

namespace WarframeInventory.Services;

/// <summary>
/// Traduce los campos que warframestat.us aún entrega en inglés.
/// No debe utilizarse para identificadores, claves de relación ni URL de Market.
/// </summary>
public static partial class WarframeSpanishText
{
    private static readonly (string English, string Spanish)[] LocationTerms =
    [
        ("Elite Sanctuary Onslaught", "Masacre del Santuario de élite"),
        ("Sanctuary Onslaught", "Masacre del Santuario"),
        ("Recover The Orokin Archive", "Recuperar el archivo Orokin"),
        ("Plains of Eidolon", "Llanuras de Eidolon"),
        ("Duviri Experience", "Experiencia de Duviri"),
        ("Arcana Isolation Vault", "Bóveda de aislamiento Arcana"),
        ("Isolation Vault", "Bóveda de aislamiento"),
        ("Infested Salvage", "Salvamento infestado"),
        ("Mobile Defense", "Defensa móvil"),
        ("Void Armageddon", "Armagedón del Vacío"),
        ("Void Cascade", "Cascada del Vacío"),
        ("Void Flood", "Inundación del Vacío"),
        ("Legacyte Harvest", "Cosecha de Legacyte"),
        ("Plague Star", "Estrella de la Plaga"),
        ("Cambion Drift", "Deriva Cambion"),
        ("Orb Vallis", "Valles del Orbe"),
        ("Kuva Fortress", "Fortaleza Kuva"),
        ("Kuva Flood", "Inundación Kuva"),
        ("Kuva Siphon", "Sifón Kuva"),
        ("Veil Proxima", "Próxima del Velo"),
        ("Earth", "Tierra"),
        ("Mercury", "Mercurio"),
        ("Mars", "Marte"),
        ("Phobos", "Fobos"),
        ("Jupiter", "Júpiter"),
        ("Saturn", "Saturno"),
        ("Uranus", "Urano"),
        ("Neptune", "Neptuno"),
        ("Pluto", "Plutón"),
        ("Void", "Vacío"),
        ("Defense", "Defensa"),
        ("Survival", "Supervivencia"),
        ("Capture", "Captura"),
        ("Exterminate", "Exterminio"),
        ("Excavation", "Excavación"),
        ("Interception", "Intercepción"),
        ("Disruption", "Disrupción"),
        ("Defection", "Deserción"),
        ("Assassination", "Asesinato"),
        ("Sabotage", "Sabotaje"),
        ("Rescue", "Rescate"),
        ("Spy", "Espionaje"),
        ("Skirmish", "Escaramuza"),
        ("Alchemy", "Alquimia"),
        ("Pursuit", "Persecución"),
        ("Rush", "Carrera"),
        ("Caches", "Alijos"),
        ("Cache", "Alijo"),
        ("Bounty", "Recompensa"),
        ("Mission Reward", "Recompensa de misión"),
        ("Steel Path", "Camino de Acero"),
        ("Single Squad", "Escuadrón único"),
        ("Squad VS Squad", "Escuadrón contra escuadrón"),
        ("Another Betrayer", "Otro traidor"),
        ("Family Reunion", "Reunión familiar"),
        ("Table For Two", "Mesa para dos"),
        ("The Aftermath", "Las secuelas"),
        ("Time's Up", "Se acabó el tiempo"),
        ("Hot Mess", "Caos total"),
        ("Lone Story", "Historia solitaria"),
        ("The Circuit", "El Circuito"),
        ("Faceoff", "Enfrentamiento"),
        ("Rotation", "Rotación"),
        ("Level", "Nivel")
    ];

    private static readonly (string English, string Spanish)[] TypeTerms =
    [
        ("Mission Reward", "Recompensa de misión"),
        ("Enemy Drop", "Recompensa de enemigo"),
        ("Archwing Mission", "Misión de Archwing"),
        ("Melee", "Cuerpo a cuerpo"),
        ("Shotgun", "Escopeta"),
        ("Pistol", "Pistola"),
        ("Primary", "Principal"),
        ("Secondary", "Secundaria"),
        ("Weapons", "Armas"),
        ("Weapon", "Arma"),
        ("Blueprint", "Plano"),
        ("Augment", "Aumento"),
        ("Drop", "Recompensa"),
        ("Radiant", "Radiante"),
        ("Exceptional", "Excepcional"),
        ("Flawless", "Perfecta"),
        ("Intact", "Intacta"),
        ("Relics", "Reliquias"),
        ("Relic", "Reliquia"),
        ("Resource", "Recurso"),
        ("Enemies", "Enemigos"),
        ("Enemy", "Enemigo")
    ];

    public static string Location(string? value)
        => ReplaceTerms(value, LocationTerms);

    public static string Type(string? value)
    {
        var translated = ReplaceTerms(Location(value), TypeTerms);
        var relic = Regex.Match(
            translated,
            @"^(?<name>.+?)\s+Reliquia(?<suffix>\s*\(.+\))?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return relic.Success
            ? $"Reliquia {relic.Groups["name"].Value}{relic.Groups["suffix"].Value}"
            : translated;
    }

    public static string Rarity(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "common" or "común" => "Común",
            "uncommon" or "poco común" => "Poco común",
            "rare" or "rara" or "raro" => "Rara",
            "legendary" or "legendaria" or "legendario" => "Legendaria",
            "" or null => "",
            _ => value.Trim()
        };

    public static string Reward(string? value)
    {
        var text = value?.Trim() ?? "";
        if (text.Length == 0)
            return "";

        var translated = TranslateRewardSuffix(text, " Neuroptics Blueprint",
            "Plano de neurópticas de ");
        if (translated != text) return translated;
        translated = TranslateRewardSuffix(text, " Chassis Blueprint",
            "Plano del chasis de ");
        if (translated != text) return translated;
        translated = TranslateRewardSuffix(text, " Systems Blueprint",
            "Plano de sistemas de ");
        if (translated != text) return translated;
        translated = TranslateRewardSuffix(text, " Harness Blueprint",
            "Plano del arnés de ");
        if (translated != text) return translated;

        var suffixes = new (string English, string SpanishPrefix)[]
        {
            (" Blueprint", "Plano de "),
            (" Receiver", "Receptor de "),
            (" Barrel", "Cañón de "),
            (" Stock", "Culata de "),
            (" Blade", "Hoja de "),
            (" Handle", "Empuñadura de "),
            (" Link", "Enlace de "),
            (" Grip", "Empuñadura de "),
            (" String", "Cuerda de "),
            (" Upper Limb", "Brazo superior de "),
            (" Lower Limb", "Brazo inferior de "),
            (" Ornament", "Ornamento de "),
            (" Pouch", "Bolsa de "),
            (" Disc", "Disco de ")
        };
        foreach (var (english, spanishPrefix) in suffixes)
        {
            translated = TranslateRewardSuffix(text, english, spanishPrefix);
            if (translated != text)
                return translated;
        }

        return Type(text);
    }

    private static string TranslateRewardSuffix(
        string value, string englishSuffix, string spanishPrefix)
        => value.EndsWith(englishSuffix, StringComparison.OrdinalIgnoreCase)
            ? spanishPrefix + value[..^englishSuffix.Length].Trim()
            : value;

    private static string ReplaceTerms(
        string? value, IEnumerable<(string English, string Spanish)> terms)
    {
        var result = value?.Trim() ?? "";
        foreach (var (english, spanish) in terms)
        {
            result = Regex.Replace(
                result,
                $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(english)}(?![\p{{L}}\p{{N}}])",
                spanish,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        return result;
    }
}
