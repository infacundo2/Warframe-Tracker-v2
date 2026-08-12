namespace WarframeInventory.Models;

public sealed class AgentDevice
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = "";
    public string Name { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public DateTime? LastSeenUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }
    public long LastSequence { get; set; }
}

public sealed class AgentPairing
{
    public Guid Id { get; set; }
    public string CodeHash { get; set; } = "";
    public string VerifierHash { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public DateTime? ApprovedUtc { get; set; }
    public DateTime? ConsumedUtc { get; set; }
    public string? UserId { get; set; }
}

public sealed class InventorySyncBatch
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public string UserId { get; set; } = "";
    public long Sequence { get; set; }
    public string Source { get; set; } = "tracker-agent";
    public string ContentHash { get; set; } = "";
    public bool IsAuthoritative { get; set; }
    public DateTime CapturedUtc { get; set; }
    public DateTime ReceivedUtc { get; set; }
    public DateTime? AppliedUtc { get; set; }
    public string Status { get; set; } = "received";
    public int ChangedRecords { get; set; }
    public string? ErrorCode { get; set; }
}
