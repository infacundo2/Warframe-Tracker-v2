using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using WarframeInventory.Contracts.AgentV1;
using WarframeInventory.Data;
using WarframeInventory.Security;
using WarframeInventory.Services;

namespace WarframeInventory.Controllers.AgentV1;

[ApiController]
[Route("api/agent/v1")]
public sealed class AgentPairingController : ControllerBase
{
    private readonly AgentTokenService _tokens;
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    public AgentPairingController(AgentTokenService tokens,
        IDbContextFactory<ApplicationDbContext> dbFactory)
        => (_tokens, _dbFactory) = (tokens, dbFactory);

    [AllowAnonymous, EnableRateLimiting("agent-pairing")]
    [HttpPost("pairing/start")]
    public async Task<ActionResult<PairingStartResponse>> Start(
        PairingStartRequest request, CancellationToken ct)
    {
        var publicBase = $"{Request.Scheme}://{Request.Host}";
        return Ok(await _tokens.StartPairingAsync(request.DeviceName, publicBase, ct));
    }

    [AllowAnonymous, EnableRateLimiting("agent-pairing")]
    [HttpPost("pairing/token")]
    public async Task<ActionResult<PairingTokenResponse>> Token(
        PairingTokenRequest request, CancellationToken ct)
        => Ok(await _tokens.ExchangeAsync(request.PairingId, request.Verifier, ct));

    [Authorize(AuthenticationSchemes = AgentTokenAuthenticationHandler.SchemeName)]
    [EnableRateLimiting("agent-device")]
    [HttpGet("status")]
    public async Task<ActionResult<AgentStatusResponse>> Status(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("agent_device_id"), out var deviceId))
            return Unauthorized();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var row = await db.AgentDevices.AsNoTracking()
            .Where(x => x.Id == deviceId && x.RevokedUtc == null)
            .Join(db.Users, device => device.UserId, user => user.Id,
                (device, user) => new { Device = device, UserName = user.UserName ?? "" })
            .FirstOrDefaultAsync(ct);
        if (row is null) return Unauthorized();
        await _tokens.TouchAsync(deviceId, ct);
        return Ok(new AgentStatusResponse(deviceId, row.Device.Name, row.UserName,
            DateTime.UtcNow, row.Device.LastSequence, true));
    }
}
