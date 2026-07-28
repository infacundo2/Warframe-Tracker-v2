using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using WarframeInventory.Services;

namespace WarframeInventory.Controllers;

[ApiController]
[Route("api/desktop-bridge")]
public sealed class DesktopInventoryBridgeController : ControllerBase
{
    private readonly DesktopInventorySyncService _sync;
    private readonly string _bridgeKey;

    public DesktopInventoryBridgeController(
        DesktopInventorySyncService sync,
        IConfiguration configuration)
    {
        _sync = sync;
        _bridgeKey = configuration["DesktopBridge:Key"]
                     ?? Environment.GetEnvironmentVariable("WARFRAME_DESKTOP_BRIDGE_KEY")
                     ?? "";
    }

    [HttpPost("inventory")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public ActionResult<DesktopCaptureReceipt> Receive(
        [FromBody] DesktopInventoryBridgeRequest request)
    {
        if (!HttpContext.Connection.RemoteIpAddress?.IsLoopback() ?? true)
            return Forbid();
        if (string.IsNullOrWhiteSpace(_bridgeKey))
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "El puente de escritorio no está habilitado." });
        if (!Request.Headers.TryGetValue("X-Warframe-Bridge-Key", out var supplied)
            || !SecureEquals(_bridgeKey, supplied.ToString()))
            return Unauthorized(new { error = "Clave de puente inválida." });

        try
        {
            return Ok(_sync.Stage(request.InventoryJson, request.Source));
        }
        catch (DesktopInventoryException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (System.Text.Json.JsonException)
        {
            return BadRequest(new { error = "El inventario no contiene JSON válido." });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        if (!HttpContext.Connection.RemoteIpAddress?.IsLoopback() ?? true)
            return Forbid();
        return Ok(new
        {
            ready = !string.IsNullOrWhiteSpace(_bridgeKey),
            capture = _sync.GetStatus()
        });
    }

    private static bool SecureEquals(string expected, string supplied)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        return expectedBytes.Length == suppliedBytes.Length
               && CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }
}

public sealed record DesktopInventoryBridgeRequest(
    string InventoryJson,
    string Source = "overwolf-gep");

internal static class IpAddressExtensions
{
    public static bool IsLoopback(this System.Net.IPAddress address)
        => System.Net.IPAddress.IsLoopback(address)
           || address.Equals(System.Net.IPAddress.IPv6Loopback);
}
