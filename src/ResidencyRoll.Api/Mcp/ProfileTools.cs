using System.ComponentModel;
using System.Security.Claims;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ResidencyRoll.Api.Models;
using ResidencyRoll.Api.Services;

namespace ResidencyRoll.Api.Mcp;

[McpServerToolType]
public sealed class ProfileTools
{
    [McpServerTool(Name = "get_home_country", Title = "Get home country", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(McpHomeCountry))]
    [Description("The home country saved for a traveller. Null when it has not been set. Days left, days over, and days away use this value. Omit traveller for the signed-in person.")]
    public static async Task<CallToolResult> GetHomeCountry(
        TravellerService travellers,
        ClaimsPrincipal user,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Read);
        var person = await McpTravellers.Resolve(travellers, user, traveller);
        var country = string.IsNullOrWhiteSpace(person.HomeCountry) ? null : person.HomeCountry.Trim();
        var text = country is null
            ? "No home country is saved. Ask the user, then call set_home_country."
            : $"Home country is {country}.";
        return McpResults.For(person, text, new McpHomeCountry { HomeCountry = country });
    }

    [McpServerTool(Name = "set_home_country", Title = "Set home country", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(McpHomeCountry))]
    [Description("Saves the home country for a traveller. Ask the user to confirm before changing it. Pass an empty string to clear it. The name should match the country names in residency results, such as Canada or United Kingdom. Omit traveller for the signed-in person.")]
    public static async Task<CallToolResult> SetHomeCountry(
        UserProfileService profiles,
        TravellerService travellers,
        ClaimsPrincipal user,
        [Description("Home country name. Empty clears the saved value.")] string homeCountry,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Write);
        var person = await McpTravellers.Resolve(travellers, user, traveller);
        try
        {
            var saved = await profiles.SetHomeCountryAsync(McpScopes.UserId(user), homeCountry, person);
            var text = saved is null ? "Cleared the saved home country." : $"Saved home country {saved}.";
            return McpResults.For(person, text, new McpHomeCountry { HomeCountry = saved });
        }
        catch (ArgumentException ex)
        {
            throw new McpException(ex.Message);
        }
    }
}

public sealed class McpHomeCountry
{
    public McpTravellerRef Traveller { get; set; } = new();

    [Description("Saved home country, or null when none is saved.")]
    public string? HomeCountry { get; set; }
}

internal static class SavedHomeCountry
{
    public static string? Resolve(Traveller traveller, string? argument)
    {
        if (!string.IsNullOrWhiteSpace(argument))
        {
            return argument.Trim();
        }

        return string.IsNullOrWhiteSpace(traveller.HomeCountry) ? null : traveller.HomeCountry.Trim();
    }
}
