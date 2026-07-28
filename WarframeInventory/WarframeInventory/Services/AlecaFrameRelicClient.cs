using System.Buffers.Binary;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WarframeInventory.Services;

public sealed class AlecaFrameRelicClient
{
    private const int HeaderSize = sizeof(uint);
    private const int RecordSize = 9;
    private const int MalformedRecordSize = 8;
    private const int MaximumRelics = 10_000;
    private readonly HttpClient _http;
    private readonly ILogger<AlecaFrameRelicClient> _logger;

    public AlecaFrameRelicClient(
        HttpClient http,
        ILogger<AlecaFrameRelicClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<AlecaRelicInventory> GetRelicsAsync(
        string publicToken,
        CancellationToken cancellationToken = default)
    {
        publicToken = NormalizePublicToken(publicToken);
        if (publicToken.Length is < 12 or > 512)
            throw new RelicSyncException("El token público no tiene un formato válido.");

        var endpoint =
            $"api/stats/public/getRelicInventory?publicToken={Uri.EscapeDataString(publicToken)}";
        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(
                endpoint,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new RelicSyncException(
                "AlecaFrame tardó demasiado en responder. Intenta nuevamente.");
        }
        catch (HttpRequestException)
        {
            throw new RelicSyncException(
                "No se pudo conectar con AlecaFrame. Revisa tu conexión e intenta nuevamente.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var detail = await ReadErrorAsync(response, cancellationToken);
                throw new RelicSyncException(detail);
            }

            var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (body.Length > 1_000_000)
                throw new RelicSyncException("La respuesta del inventario es demasiado grande.");

            var payload = DecodeTransport(body, response.Content.Headers.ContentType);
            var inventory = DecodePayload(payload);
            if (inventory.SkippedRecords > 0)
            {
                _logger.LogWarning(
                    "AlecaFrame declared {DeclaredCount} relic records; {DecodedCount} were decoded and {SkippedCount} malformed records were skipped.",
                    inventory.DeclaredCount,
                    inventory.Entries.Count,
                    inventory.SkippedRecords);
            }

            return inventory;
        }
    }

    public async Task<AlecaPublicStats> GetPublicStatsAsync(
        string publicToken,
        CancellationToken cancellationToken = default)
    {
        publicToken = NormalizeAndValidateToken(publicToken);
        var endpoint = $"api/stats/public?token={Uri.EscapeDataString(publicToken)}";
        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(
                endpoint,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new RelicSyncException(
                "AlecaFrame tardó demasiado en responder. Intenta nuevamente.");
        }
        catch (HttpRequestException)
        {
            throw new RelicSyncException(
                "No se pudo conectar con AlecaFrame. Revisa tu conexión e intenta nuevamente.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var detail = await ReadErrorAsync(
                    response,
                    cancellationToken,
                    "No se pudieron consultar las estadísticas públicas de AlecaFrame.");
                throw new RelicSyncException(detail);
            }

            AlecaStatsResponse? result;
            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                result = await JsonSerializer.DeserializeAsync<AlecaStatsResponse>(
                    stream,
                    StatsJsonOptions,
                    cancellationToken);
            }
            catch (JsonException exception)
            {
                throw new RelicSyncException(
                    "AlecaFrame devolvió estadísticas con un formato no reconocido.",
                    exception);
            }

            if (result is null)
                throw new RelicSyncException("AlecaFrame devolvió estadísticas vacías.");

            var permissions = (AlecaPublicParts)result.PublicParts;
            var latest = result.GeneralDataPoints?
                .OrderByDescending(x => x.Timestamp)
                .FirstOrDefault();
            if (latest is null || !HasAccountPermission(permissions))
            {
                throw new RelicSyncException(
                    "El enlace solo permite leer reliquias. Crea uno nuevo y selecciona Créditos, Endo, Ducados, Aya, Platino y Datos de cuenta.");
            }

            return new AlecaPublicStats(
                permissions.HasFlag(AlecaPublicParts.Platinum) ? latest.Platinum : null,
                permissions.HasFlag(AlecaPublicParts.Credits) ? latest.Credits : null,
                permissions.HasFlag(AlecaPublicParts.Endo) ? latest.Endo : null,
                permissions.HasFlag(AlecaPublicParts.Ducats) ? latest.Ducats : null,
                permissions.HasFlag(AlecaPublicParts.Aya) ? latest.Aya : null,
                permissions.HasFlag(AlecaPublicParts.AccountData) ? latest.MasteryRank : null,
                permissions.HasFlag(AlecaPublicParts.AccountData) ? latest.CompletionPercentage : null,
                permissions.HasFlag(AlecaPublicParts.AccountData) ? latest.RelicsOpened : null,
                permissions.HasFlag(AlecaPublicParts.AccountData) ? latest.TradeCount : null,
                latest.Timestamp,
                result.LastUpdate,
                result.UsernameWhenPublic,
                permissions);
        }
    }

    public static AlecaRelicInventory DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < HeaderSize)
            throw new RelicSyncException("AlecaFrame devolvió un inventario incompleto.");

        var count = BinaryPrimitives.ReadUInt32LittleEndian(payload);
        if (count > MaximumRelics)
            throw new RelicSyncException("AlecaFrame devolvió una cantidad de reliquias no válida.");

        var expectedLength = checked(HeaderSize + (int)count * RecordSize);
        var entries = new List<AlecaRelicEntry>((int)count);
        var skippedRecords = 0;
        var offset = HeaderSize;

        while (offset < payload.Length)
        {
            if (TryDecodeRecord(payload[offset..], out var entry))
            {
                entries.Add(entry);
                offset += RecordSize;
                continue;
            }

            // AlecaFrame calcula el encabezado antes de validar cada variante. Una era o
            // refinamiento desconocido produce un registro de 8 bytes, pero sigue contando
            // como uno de 9. Lo omitimos y continuamos en el siguiente límite válido.
            if (LooksLikeMalformedRecord(payload[offset..]))
            {
                skippedRecords++;
                offset += MalformedRecordSize;
                continue;
            }

            throw new RelicSyncException(
                $"El inventario recibido no se pudo recuperar de forma segura ({payload.Length} bytes).");
        }

        if (entries.Count == 0 && count > 0)
            throw new RelicSyncException("AlecaFrame no devolvió ninguna reliquia reconocible.");

        var representedRecords = entries.Count + skippedRecords;
        if (representedRecords > count)
            throw new RelicSyncException(
                $"El inventario recibido no coincide con su encabezado ({payload.Length} bytes).");

        // AlecaFrame también puede contar una reliquia y omitirla por completo cuando
        // su nombre interno no tiene las tres partes esperadas. No deja bytes que
        // permitan identificarla, por lo que solo podemos contabilizarla como ausente.
        var missingRecords = (int)count - representedRecords;
        skippedRecords += missingRecords;

        var expectedDeficit = checked(missingRecords * RecordSize
                                      + (skippedRecords - missingRecords));
        if (expectedLength - payload.Length != expectedDeficit)
            throw new RelicSyncException(
                $"El inventario recibido no coincide con su encabezado ({payload.Length} bytes).");

        return new AlecaRelicInventory(entries, (int)count, skippedRecords);
    }

    private static bool TryDecodeRecord(
        ReadOnlySpan<byte> payload,
        out AlecaRelicEntry entry)
    {
        entry = default!;
        if (payload.Length < RecordSize)
            return false;

        var era = payload[0];
        var refinement = payload[1];
        if (era > 4 || refinement > 6 || !TryDecodeCode(payload.Slice(2, 3), out var code))
            return false;

        var quantity = BinaryPrimitives.ReadUInt32LittleEndian(
            payload.Slice(5, sizeof(uint)));
        if (quantity > int.MaxValue)
            return false;

        entry = new AlecaRelicEntry(
            EraName(era),
            code,
            RefinementName(refinement),
            (int)quantity);
        return true;
    }

    private static bool LooksLikeMalformedRecord(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < MalformedRecordSize || payload[0] > 6)
            return false;
        if (!TryDecodeCode(payload.Slice(1, 3), out _))
            return false;
        var quantity = BinaryPrimitives.ReadUInt32LittleEndian(
            payload.Slice(4, sizeof(uint)));
        return quantity <= int.MaxValue;
    }

    private static bool TryDecodeCode(ReadOnlySpan<byte> bytes, out string code)
    {
        code = Encoding.ASCII.GetString(bytes).Trim('\0', ' ').ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code)
            || code.Any(character => !char.IsAsciiLetterOrDigit(character)))
            return false;

        return code is "I" or "II" or "III" or "IV"
               || (char.IsAsciiLetter(code[0])
                   && code.Skip(1).All(char.IsAsciiDigit));
    }

    private static string NormalizePublicToken(string value)
    {
        value = value.Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return value;

        foreach (var pair in uri.Query.TrimStart('?')
                     .Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0)
                continue;
            var key = Uri.UnescapeDataString(pair[..separator]);
            if (!key.Equals("token", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("publicToken", StringComparison.OrdinalIgnoreCase))
                continue;
            return Uri.UnescapeDataString(pair[(separator + 1)..]);
        }

        var lastSegment = uri.Segments.LastOrDefault()?.Trim('/');
        return string.IsNullOrWhiteSpace(lastSegment) ? value : Uri.UnescapeDataString(lastSegment);
    }

    private static string NormalizeAndValidateToken(string publicToken)
    {
        publicToken = NormalizePublicToken(publicToken);
        if (publicToken.Length is < 12 or > 512)
            throw new RelicSyncException("El token público no tiene un formato válido.");
        return publicToken;
    }

    private static byte[] DecodeTransport(byte[] body, MediaTypeHeaderValue? contentType)
    {
        if (body.Length == 0)
            throw new RelicSyncException("AlecaFrame devolvió una respuesta vacía.");

        var isJson = contentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase)
                     == true;
        if (isJson || body[0] == (byte)'"')
        {
            try
            {
                return JsonSerializer.Deserialize<byte[]>(body)
                       ?? throw new RelicSyncException("AlecaFrame devolvió datos vacíos.");
            }
            catch (JsonException exception)
            {
                throw new RelicSyncException(
                    "No se pudo interpretar la respuesta de AlecaFrame.",
                    exception);
            }
        }

        return body;
    }

    private static async Task<string> ReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken,
        string? fallback = null)
    {
        var generic = response.StatusCode switch
        {
            System.Net.HttpStatusCode.TooManyRequests =>
                "AlecaFrame limitó temporalmente las consultas. Espera un minuto.",
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                "El token no es válido o no tiene permiso para leer reliquias.",
            _ => fallback ?? "No se pudo consultar el inventario de AlecaFrame."
        };

        try
        {
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(json);
            var detail = document.RootElement.TryGetProperty("detail", out var property)
                ? property.GetString()
                : null;
            if (detail?.Contains("invalid token", StringComparison.OrdinalIgnoreCase) == true)
                return "El token público de AlecaFrame no es válido o fue revocado.";
        }
        catch
        {
            // El mensaje genérico evita exponer respuestas externas completas.
        }

        return generic;
    }

    private static bool HasAccountPermission(AlecaPublicParts permissions)
        => (permissions & (AlecaPublicParts.Platinum
                           | AlecaPublicParts.Ducats
                           | AlecaPublicParts.Endo
                           | AlecaPublicParts.Credits
                           | AlecaPublicParts.AccountData
                           | AlecaPublicParts.Aya)) != 0;

    private static readonly JsonSerializerOptions StatsJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static string EraName(byte value) => value switch
    {
        0 => "LITH",
        1 => "MESO",
        2 => "NEO",
        3 => "AXI",
        4 => "REQUIEM",
        _ => throw new RelicSyncException("Era de reliquia desconocida.")
    };

    private static string RefinementName(byte value) => value switch
    {
        0 => "Intacta",
        1 or 4 => "Excepcional",
        2 or 5 => "Perfecta",
        3 or 6 => "Radiante",
        _ => throw new RelicSyncException("Refinamiento desconocido.")
    };
}

public sealed record AlecaRelicEntry(
    string Era,
    string Code,
    string Refinement,
    int Quantity);

public sealed record AlecaRelicInventory(
    IReadOnlyList<AlecaRelicEntry> Entries,
    int DeclaredCount,
    int SkippedRecords)
{
    public bool IsAuthoritative => SkippedRecords == 0;
}

[Flags]
public enum AlecaPublicParts
{
    None = 0,
    Trades = 1,
    Platinum = 2,
    Ducats = 4,
    Endo = 8,
    Credits = 16,
    AccountData = 32,
    Aya = 64,
    Relics = 128
}

public sealed record AlecaPublicStats(
    int? Platinum,
    long? Credits,
    int? Endo,
    int? Ducats,
    int? Aya,
    int? MasteryRank,
    int? CompletionPercentage,
    int? RelicsOpened,
    int? TradeCount,
    DateTime SourceTimestamp,
    DateTime LastUpdate,
    string? PublicUsername,
    AlecaPublicParts Permissions);

internal sealed class AlecaStatsResponse
{
    public List<AlecaStatsDataPoint>? GeneralDataPoints { get; set; }
    public DateTime LastUpdate { get; set; }
    public int PublicParts { get; set; }
    public string? UsernameWhenPublic { get; set; }
}

internal sealed class AlecaStatsDataPoint
{
    [JsonPropertyName("ts")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("plat")]
    public int Platinum { get; set; }

    public long Credits { get; set; }
    public int Endo { get; set; }
    public int Ducats { get; set; }
    public int Aya { get; set; }

    [JsonPropertyName("mr")]
    public int MasteryRank { get; set; }

    [JsonPropertyName("percentageCompletion")]
    public int CompletionPercentage { get; set; }

    [JsonPropertyName("relicOpened")]
    public int RelicsOpened { get; set; }

    [JsonPropertyName("trades")]
    public int TradeCount { get; set; }
}

public sealed class RelicSyncException : Exception
{
    public RelicSyncException(string message) : base(message) { }
    public RelicSyncException(string message, Exception innerException)
        : base(message, innerException) { }
}
