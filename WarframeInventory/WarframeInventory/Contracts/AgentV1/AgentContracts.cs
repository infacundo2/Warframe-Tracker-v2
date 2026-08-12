namespace WarframeInventory.Contracts.AgentV1;

public sealed record PairingStartRequest(string DeviceName);
public sealed record PairingStartResponse(Guid PairingId, string Code, string Verifier,
    DateTime ExpiresUtc, string ConnectUrl);
public sealed record PairingTokenRequest(Guid PairingId, string Verifier);
public sealed record PairingTokenResponse(string Status, string? Token, Guid? DeviceId,
    string? UserName, DateTime? ExpiresUtc);
public sealed record AgentStatusResponse(Guid DeviceId, string DeviceName, string UserName,
    DateTime? LastSeenUtc, long LastSequence, bool AutomaticSyncAllowed);

public sealed record AgentInventoryItem(string Section, string UniqueName, int Quantity,
    long? Experience = null);
public sealed record AgentAccountValues(long? Credits, long? Endo, long? Platinum,
    long? Ducats, long? Aya, int? MasteryRank);
public sealed record AgentInventorySnapshot(Guid BatchId, long Sequence, DateTime CapturedUtc,
    bool IsAuthoritative, string ContentHash, IReadOnlyList<AgentInventoryItem> Items,
    AgentAccountValues? Account);
public sealed record AgentInventoryPreviewResponse(Guid BatchId, string Status,
    int DistinctItems, int TotalQuantity, int UnknownItems, int Changes,
    bool IsAuthoritative);
public sealed record AgentInventoryApplyRequest(Guid BatchId);
public sealed record AgentInventoryApplyResponse(Guid BatchId, string Status,
    int ChangedRecords, DateTime? AppliedUtc);
