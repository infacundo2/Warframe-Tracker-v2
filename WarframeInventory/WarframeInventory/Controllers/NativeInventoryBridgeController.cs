using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WarframeInventory.Services;

namespace WarframeInventory.Controllers;

[Authorize]
[ApiController]
[EnableRateLimiting("native-inventory")]
[Route("api/native-inventory")]
public sealed class NativeInventoryBridgeController : ControllerBase
{
    private readonly NativeInventorySyncService _sync;
    private readonly ILogger<NativeInventoryBridgeController> _logger;

    public NativeInventoryBridgeController(
        NativeInventorySyncService sync,
        ILogger<NativeInventoryBridgeController> logger)
    {
        _sync = sync;
        _logger = logger;
    }

    [HttpPost("capture")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public ActionResult<DesktopCaptureReceipt> Receive(
        [FromBody] NativeInventoryBridgeRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();
        if (!Request.Headers.TryGetValue("X-Warframe-Native-Bridge", out var bridge)
            || bridge.ToString() != "1")
            return BadRequest(new { error = "Falta la identificación del puente Native." });

        try
        {
            return Ok(_sync.Stage(userId, request.InventoryJson, "overwolf-native"));
        }
        catch (DesktopInventoryException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (System.Text.Json.JsonException)
        {
            return BadRequest(new { error = "El inventario no contiene JSON válido." });
        }
        catch (Exception exception)
        {
            // Never log the request body or raw GEP payload.
            _logger.LogError(
                exception,
                "Falló la recepción Native para el usuario {UserId}.",
                userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "La captura no pudo procesarse y no fue aplicada." });
        }
    }
}

public sealed record NativeInventoryBridgeRequest(string InventoryJson);
