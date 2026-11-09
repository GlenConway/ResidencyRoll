using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ResidencyRoll.Api.Authentication;

/// <summary>
/// Used only when JWT validation is turned off. Every request is the local user so
/// <c>[Authorize]</c> still scopes trips without an identity provider.
/// </summary>
public sealed class LocalUserAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Local";
    public const string UserId = "local";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, UserId),
                new Claim(ClaimTypes.Name, "Local User"),
                new Claim(ClaimTypes.Email, "local@localhost"),
            ],
            SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
