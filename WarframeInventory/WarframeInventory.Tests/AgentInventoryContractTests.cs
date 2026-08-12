using System.Text.Json;
using WarframeInventory.Contracts.AgentV1;
using Xunit;

namespace WarframeInventory.Tests;

public sealed class AgentInventoryContractTests
{
    [Fact]
    public void Snapshot_accepts_payload_without_client_hash()
    {
        var snapshot = JsonSerializer.Deserialize<AgentInventorySnapshot>("""
            {
              "batchId":"00000000-0000-0000-0000-000000000001",
              "sequence":1,
              "capturedUtc":"2026-08-12T12:00:00Z",
              "isAuthoritative":false,
              "items":[{"section":"QAOnly","uniqueName":"/QA/Probe","quantity":1}],
              "account":null
            }
            """, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(snapshot);
        Assert.Null(snapshot.ContentHash);
        Assert.Single(snapshot.Items);
    }
}
