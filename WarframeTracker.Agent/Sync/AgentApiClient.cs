using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using WarframeTracker.Agent.Configuration;
using WarframeTracker.Agent.Inventory;

namespace WarframeTracker.Agent.Sync;

public sealed record PairingStart(Guid PairingId, string Code, string Verifier,
    DateTime ExpiresUtc, string ConnectUrl);
public sealed record PairingToken(string Status, string? Token, Guid? DeviceId,
    string? UserName, DateTime? ExpiresUtc);
public sealed record RemoteAgentStatus(Guid DeviceId, string DeviceName, string UserName,
    DateTime? LastSeenUtc, long LastSequence, bool AutomaticSyncAllowed);
public sealed record RemotePreview(Guid BatchId, string Status, int DistinctItems,
    int TotalQuantity, int UnknownItems, int Changes, bool IsAuthoritative);
public sealed record RemoteApply(Guid BatchId, string Status, int ChangedRecords,
    DateTime? AppliedUtc);

public sealed class AgentApiClient
{
    private readonly HttpClient _http;
    private readonly TrackerAgentOptions _options;
    public AgentApiClient(HttpClient http, IOptions<TrackerAgentOptions> options)
        => (_http, _options) = (http, options.Value);

    public async Task<PairingStart> StartPairingAsync(CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync("api/agent/v1/pairing/start",
            new { deviceName = _options.DeviceName }, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PairingStart>(cancellationToken: ct))!;
    }
    public async Task<PairingToken> ExchangeAsync(Guid id, string verifier, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync("api/agent/v1/pairing/token",
            new { pairingId = id, verifier }, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PairingToken>(cancellationToken: ct))!;
    }
    public async Task<RemoteAgentStatus?> StatusAsync(string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/agent/v1/status");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RemoteAgentStatus>(cancellationToken: ct);
    }
    public async Task<RemotePreview> PreviewAsync(string token, InventorySnapshot snapshot,
        CancellationToken ct)
    {
        using var request = Authorized(HttpMethod.Post, "api/agent/v1/inventory/preview", token);
        request.Content = JsonContent.Create(snapshot);
        using var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new AgentApiException("unauthorized", false);
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            throw new AgentApiException(await ReadErrorAsync(response, ct), false);
        if ((int)response.StatusCode >= 500 || response.StatusCode == (System.Net.HttpStatusCode)429)
            throw new AgentApiException("server_temporarily_unavailable", true);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RemotePreview>(cancellationToken: ct))!;
    }
    public async Task<RemoteApply> ApplyAsync(string token, Guid batchId, CancellationToken ct)
    {
        using var request = Authorized(HttpMethod.Post, "api/agent/v1/inventory/apply", token);
        request.Content = JsonContent.Create(new { batchId });
        using var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            throw new AgentApiException(await ReadErrorAsync(response, ct), false);
        if ((int)response.StatusCode >= 500 || response.StatusCode == (System.Net.HttpStatusCode)429)
            throw new AgentApiException("server_temporarily_unavailable", true);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RemoteApply>(cancellationToken: ct))!;
    }
    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(cancellationToken: ct);
        return body?.GetValueOrDefault("error") ?? "request_rejected";
    }
}

public sealed class AgentApiException(string code, bool retryable) : Exception(code)
{
    public string Code => Message;
    public bool Retryable { get; } = retryable;
}
