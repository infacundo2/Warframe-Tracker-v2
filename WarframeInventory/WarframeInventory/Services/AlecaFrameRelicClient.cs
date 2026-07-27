using System.Buffers.Binary;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace WarframeInventory.Services;

public sealed class AlecaFrameRelicClient
{
    private const int HeaderSize = sizeof(uint);
    private const int RecordSize = 9;
    private const int MaximumRelics = 10_000;
    private readonly HttpClient _http;

    public AlecaFrameRelicClient(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<AlecaRelicEntry>> GetRelicsAsync(
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
            return DecodePayload(payload);
        }
    }

    public static IReadOnlyList<AlecaRelicEntry> DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < HeaderSize)
            throw new RelicSyncException("AlecaFrame devolvió un inventario incompleto.");

        var count = BinaryPrimitives.ReadUInt32LittleEndian(payload);
        if (count > MaximumRelics)
            throw new RelicSyncException("AlecaFrame devolvió una cantidad de reliquias no válida.");

        var expectedLength = checked(HeaderSize + (int)count * RecordSize);
        if (payload.Length != expectedLength)
            throw new RelicSyncException(
                $"El inventario recibido tiene un tamaño inesperado ({payload.Length} bytes).");

        var entries = new List<AlecaRelicEntry>((int)count);
        for (var index = 0; index < count; index++)
        {
            var offset = HeaderSize + index * RecordSize;
            var era = payload[offset];
            var refinement = payload[offset + 1];
            if (era > 4 || refinement > 6)
                throw new RelicSyncException("El inventario contiene una variante desconocida.");

            var code = Encoding.ASCII.GetString(payload.Slice(offset + 2, 3))
                .Trim('\0', ' ');
            if (string.IsNullOrWhiteSpace(code)
                || code.Any(character => !char.IsLetterOrDigit(character)))
                throw new RelicSyncException("El inventario contiene un nombre de reliquia no válido.");

            var quantity = BinaryPrimitives.ReadUInt32LittleEndian(
                payload.Slice(offset + 5, sizeof(uint)));
            if (quantity > int.MaxValue)
                throw new RelicSyncException("Una cantidad de reliquias excede el límite admitido.");

            entries.Add(new AlecaRelicEntry(
                EraName(era),
                code.ToUpperInvariant(),
                RefinementName(refinement),
                (int)quantity));
        }

        return entries;
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
        CancellationToken cancellationToken)
    {
        var generic = response.StatusCode switch
        {
            System.Net.HttpStatusCode.TooManyRequests =>
                "AlecaFrame limitó temporalmente las consultas. Espera un minuto.",
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                "El token no es válido o no tiene permiso para leer reliquias.",
            _ => "No se pudo consultar el inventario de AlecaFrame."
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

public sealed class RelicSyncException : Exception
{
    public RelicSyncException(string message) : base(message) { }
    public RelicSyncException(string message, Exception innerException)
        : base(message, innerException) { }
}
