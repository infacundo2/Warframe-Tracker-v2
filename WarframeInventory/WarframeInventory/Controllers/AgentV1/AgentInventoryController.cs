using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WarframeInventory.Contracts.AgentV1;
using WarframeInventory.Security;
using WarframeInventory.Services;

namespace WarframeInventory.Controllers.AgentV1;

[ApiController, EnableRateLimiting("agent-device")]
[Authorize(AuthenticationSchemes = AgentTokenAuthenticationHandler.SchemeName)]
[Route("api/agent/v1/inventory")]
public sealed class AgentInventoryController(AgentInventoryIngestionService ingestion) : ControllerBase
{
    [HttpPost("preview"), RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<AgentInventoryPreviewResponse>> Preview(
        AgentInventorySnapshot snapshot, CancellationToken ct)
    {
        if (!TryIdentity(out var deviceId, out var userId)) return Unauthorized();
        try { return Ok(await ingestion.PreviewAsync(deviceId, userId, snapshot, ct)); }
        catch (AgentInventoryException exception)
        { return Conflict(new { error = exception.Code }); }
    }

    [HttpPost("apply")]
    public async Task<ActionResult<AgentInventoryApplyResponse>> Apply(
        AgentInventoryApplyRequest request, CancellationToken ct)
    {
        if (!TryIdentity(out var deviceId, out var userId)) return Unauthorized();
        try { return Ok(await ingestion.ApplyAsync(deviceId, userId, request.BatchId, ct)); }
        catch (AgentInventoryException exception)
        { return Conflict(new { error = exception.Code }); }
    }
    private bool TryIdentity(out Guid deviceId, out string userId)
    {
        userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        return Guid.TryParse(User.FindFirstValue("agent_device_id"), out deviceId)
               && userId.Length > 0;
    }
}
