using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WarframeTracker.Agent.Configuration;
using WarframeTracker.Agent.Inventory;
using WarframeTracker.Agent.Sync;
using Xunit;

namespace WarframeTracker.Agent.Tests.Sync;

public sealed class AgentApiClientTests
{
    [Fact]
    public async Task Preview_omits_local_content_hash_from_transport()
    {
        string? sentJson = null;
        using var http = new HttpClient(new StubHandler(async request =>
        {
            sentJson = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"batchId":"00000000-0000-0000-0000-000000000001","status":"previewed","distinctItems":1,"totalQuantity":1,"unknownItems":1,"changes":0,"isAuthoritative":false}
                    """, Encoding.UTF8, "application/json")
            };
        })) { BaseAddress = new Uri("https://example.invalid/") };
        var options = Options.Create(new TrackerAgentOptions
            { ServerBaseUrl = "https://example.invalid" });
        var client = new AgentApiClient(http, options);
        var snapshot = InventorySnapshotTools.Normalize(new(
            Guid.Parse("00000000-0000-0000-0000-000000000001"), 1, DateTime.UtcNow,
            false, "", [new InventoryEntry("QAOnly", "/QA/Probe", 1)]));

        var result = await client.PreviewAsync("token", snapshot, CancellationToken.None);

        using var json = JsonDocument.Parse(sentJson!);
        Assert.False(json.RootElement.TryGetProperty("contentHash", out _));
        Assert.False(json.RootElement.TryGetProperty("ContentHash", out _));
        Assert.Equal(snapshot.BatchId, json.RootElement.GetProperty("batchId").GetGuid());
        Assert.Equal("previewed", result.Status);
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) => callback(request);
    }
}
