using System.Security.Claims;
using System.Text.Json;
using ResidencyRoll.Api.Authentication;

namespace ResidencyRoll.Api.Mcp;

public static class McpScopes
{
    public const string Read = "read:trips";
    public const string Write = "write:trips";

    public static void Require(ClaimsPrincipal user, string scope)
    {
        if (string.Equals(user.Identity?.AuthenticationType, LocalUserAuthenticationHandler.SchemeName, StringComparison.Ordinal))
        {
            return;
        }

        if (Granted(user).Contains(scope))
        {
            return;
        }

        throw new ModelContextProtocol.McpException($"This tool requires the {scope} scope.");
    }

    public static string UserId(ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        if (string.IsNullOrEmpty(id))
        {
            throw new ModelContextProtocol.McpException("Unable to determine user identity.");
        }

        return id;
    }

    private static HashSet<string> Granted(ClaimsPrincipal user)
    {
        var scopes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var claim in user.FindAll("scope").Concat(user.FindAll("scp")).Concat(user.FindAll("permissions")))
        {
            if (claim.Value.StartsWith('['))
            {
                try
                {
                    foreach (var item in JsonSerializer.Deserialize<string[]>(claim.Value) ?? [])
                    {
                        scopes.Add(item);
                    }

                    continue;
                }
                catch (JsonException)
                {
                    // Fall through and split the raw value.
                }
            }

            foreach (var part in claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                scopes.Add(part);
            }
        }

        return scopes;
    }
}
