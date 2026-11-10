using System.Security.Claims;
using ModelContextProtocol;
using ResidencyRoll.Api.Models;
using ResidencyRoll.Api.Services;

namespace ResidencyRoll.Api.Mcp;

internal static class McpTravellers
{
    public const string ParameterDescription =
        "Traveller id or name. Optional. Omit it for the signed-in person. Names match case-insensitively. No match or several matches is an error that lists the travellers.";

    public static async Task<Traveller> Resolve(TravellerService travellers, ClaimsPrincipal user, string? traveller)
    {
        try
        {
            return await travellers.ResolveAsync(McpScopes.UserId(user), traveller);
        }
        catch (TravellerResolutionException ex)
        {
            throw new McpException(ex.Message);
        }
    }

    public static async Task<IReadOnlyList<Traveller>> ResolveMany(TravellerService travellers, ClaimsPrincipal user, List<string>? names, string? traveller)
    {
        try
        {
            if (names is { Count: > 0 })
            {
                return await travellers.ResolveManyAsync(McpScopes.UserId(user), names);
            }

            return [await travellers.ResolveAsync(McpScopes.UserId(user), traveller)];
        }
        catch (TravellerResolutionException ex)
        {
            throw new McpException(ex.Message);
        }
    }
}
