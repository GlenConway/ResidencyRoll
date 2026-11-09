using System.ComponentModel;
using System.Security.Claims;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ResidencyRoll.Api.Services;

namespace ResidencyRoll.Api.Mcp;

[McpServerToolType]
public sealed class ProfileTools
{
    [McpServerTool(Name = "get_home_country", Title = "Get home country", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(McpHomeCountry))]
    [Description("The home country saved for the signed-in user. Null when it has not been set. Days left, days over, and days away use this value.")]
    public static async Task<CallToolResult> GetHomeCountry(UserProfileService profiles, ClaimsPrincipal user)
    {
        McpScopes.Require(user, McpScopes.Read);
        var country = await profiles.GetHomeCountryAsync(McpScopes.UserId(user));
        var text = country is null
            ? "No home country is saved. Ask the user, then call set_home_country."
            : $"Home country is {country}.";
        return McpResults.Ok(text, new McpHomeCountry { HomeCountry = country });
    }

    [McpServerTool(Name = "set_home_country", Title = "Set home country", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(McpHomeCountry))]
    [Description("Saves the home country for the signed-in user. Ask the user to confirm before changing it. Pass an empty string to clear it. The name should match the country names in residency results, such as Canada or United Kingdom.")]
    public static async Task<CallToolResult> SetHomeCountry(
        UserProfileService profiles,
        ClaimsPrincipal user,
        [Description("Home country name. Empty clears the saved value.")] string homeCountry)
    {
        McpScopes.Require(user, McpScopes.Write);
        try
        {
            var saved = await profiles.SetHomeCountryAsync(McpScopes.UserId(user), homeCountry);
            var text = saved is null ? "Cleared the saved home country." : $"Saved home country {saved}.";
            return McpResults.Ok(text, new McpHomeCountry { HomeCountry = saved });
        }
        catch (ArgumentException ex)
        {
            throw new McpException(ex.Message);
        }
    }
}

public sealed class McpHomeCountry
{
    [Description("Saved home country, or null when none is saved.")]
    public string? HomeCountry { get; set; }
}

internal static class SavedHomeCountry
{
    public static async Task<string?> Resolve(UserProfileService profiles, string userId, string? argument)
    {
        if (!string.IsNullOrWhiteSpace(argument))
        {
            return argument.Trim();
        }

        return await profiles.GetHomeCountryAsync(userId);
    }
}
