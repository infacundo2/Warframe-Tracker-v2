using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using WarframeInventory.Services;

namespace WarframeInventory.Security;

public sealed class AgentTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "AgentBearer";
    private readonly AgentTokenService _tokens;

    public AgentTokenAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder, AgentTokenService tokens)
        : base(options, logger, encoder) => _tokens = tokens;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();
        var device = await _tokens.AuthenticateAsync(authorization[7..].Trim(), Context.RequestAborted);
        if (device is null)
            return AuthenticateResult.Fail("Token de dispositivo inválido o revocado.");
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, device.UserId),
            new Claim("agent_device_id", device.Id.ToString()),
            new Claim(ClaimTypes.Name, device.Name)
        };
        return AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)), SchemeName));
    }
}
