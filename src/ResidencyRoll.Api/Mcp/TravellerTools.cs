using System.ComponentModel;
using System.Security.Claims;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ResidencyRoll.Api.Services;

namespace ResidencyRoll.Api.Mcp;

[McpServerToolType]
public sealed class TravellerTools
{
    [McpServerTool(Name = "list_travellers", Title = "List travellers", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(McpTravellerInfo[]))]
    [Description("Lists the travellers on the signed-in account. isSelf is true for the traveller linked to the caller. Call this before acting for someone else.")]
    public static async Task<CallToolResult> ListTravellers(TravellerService travellers, ClaimsPrincipal user)
    {
        McpScopes.Require(user, McpScopes.Read);
        var userId = McpScopes.UserId(user);
        var people = await travellers.ListAsync(userId);
        var rows = people.Select(person => McpTravellerInfo.From(person, userId)).ToList();
        var text = rows.Count == 0
            ? "No travellers."
            : string.Join(", ", rows.Select(row => row.IsSelf ? $"{row.Name} (you)" : row.Name)) + ".";
        var self = people.First(person => string.Equals(person.LinkedUserId, userId, StringComparison.Ordinal));
        return McpResults.Ok($"{self.Name}: {text}", rows);
    }

    [McpServerTool(Name = "create_traveller", Title = "Create a traveller", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(McpTravellerInfo))]
    [Description("Adds a traveller who does not sign in, such as a spouse. Ask the user to confirm the name first.")]
    public static async Task<CallToolResult> CreateTraveller(
        TravellerService travellers,
        ClaimsPrincipal user,
        [Description("Display name, unique on this account.")] string name,
        [Description("Home country. Optional.")] string? homeCountry = null)
    {
        McpScopes.Require(user, McpScopes.Write);
        try
        {
            var created = await travellers.CreateAsync(McpScopes.UserId(user), name, homeCountry);
            var info = McpTravellerInfo.From(created, McpScopes.UserId(user));
            return McpResults.For(created, $"Created traveller {created.Name}.", info);
        }
        catch (TravellerValidationException ex)
        {
            throw new McpException(ex.Message);
        }
    }

    [McpServerTool(Name = "update_traveller", Title = "Update a traveller", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(McpTravellerInfo))]
    [Description("Renames a traveller or changes their home country. Ask the user to confirm first.")]
    public static async Task<CallToolResult> UpdateTraveller(
        TravellerService travellers,
        ClaimsPrincipal user,
        [Description("Traveller id or current name.")] string traveller,
        [Description("New display name.")] string name,
        [Description("Home country. Empty clears it.")] string? homeCountry = null)
    {
        McpScopes.Require(user, McpScopes.Write);
        try
        {
            var person = await McpTravellers.Resolve(travellers, user, traveller);
            var updated = await travellers.UpdateAsync(McpScopes.UserId(user), person.Id, name, homeCountry);
            var info = McpTravellerInfo.From(updated, McpScopes.UserId(user));
            return McpResults.For(updated, $"Updated traveller {updated.Name}.", info);
        }
        catch (TravellerValidationException ex)
        {
            throw new McpException(ex.Message);
        }
    }

    [McpServerTool(Name = "delete_traveller", Title = "Delete a traveller", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(McpDeletedTraveller))]
    [Description("Deletes a traveller. Refuses while trips exist unless deleteTrips is true. Never deletes the signed-in person's traveller.")]
    public static async Task<CallToolResult> DeleteTraveller(
        TravellerService travellers,
        ClaimsPrincipal user,
        [Description("Traveller id or name.")] string traveller,
        [Description("Also delete this traveller's trips. Required when trips exist.")] bool deleteTrips = false)
    {
        McpScopes.Require(user, McpScopes.Write);
        var person = await McpTravellers.Resolve(travellers, user, traveller);
        try
        {
            var deleted = await travellers.DeleteAsync(McpScopes.UserId(user), person.Id, deleteTrips);
            if (!deleted)
            {
                throw new McpException("Traveller not found.");
            }

            return McpResults.For(person, $"Deleted traveller {person.Name}.", new McpDeletedTraveller { Deleted = true, TripsDeleted = deleteTrips });
        }
        catch (TravellerConflictException ex)
        {
            throw new McpException(ex.Message);
        }
    }

    [McpServerTool(Name = "copy_trips", Title = "Copy trips to another traveller", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(McpCopyTripsResult))]
    [Description("Copies saved legs from one traveller to another, skipping legs that already exist there. Ask the user to confirm first.")]
    public static async Task<CallToolResult> CopyTrips(
        TravellerService travellers,
        ClaimsPrincipal user,
        [Description("Traveller to copy from, by id or name.")] string source,
        [Description("Traveller to copy to, by id or name.")] string target)
    {
        McpScopes.Require(user, McpScopes.Write);
        var from = await McpTravellers.Resolve(travellers, user, source);
        var to = await McpTravellers.Resolve(travellers, user, target);
        var result = await travellers.CopyTripsAsync(McpScopes.UserId(user), from.Id, to.Id);
        var payload = new McpCopyTripsResult { Copied = result.Copied, Skipped = result.Skipped };
        return McpResults.For(to, $"Copied {result.Copied} from {from.Name} to {to.Name}. Skipped {result.Skipped} that were already there.", payload);
    }
}

public sealed class McpTravellerInfo
{
    public McpTravellerRef Traveller { get; set; } = new();
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? HomeCountry { get; set; }
    public bool IsSelf { get; set; }

    public static McpTravellerInfo From(Models.Traveller traveller, string userId) => new()
    {
        Id = traveller.Id,
        Name = traveller.Name,
        HomeCountry = string.IsNullOrWhiteSpace(traveller.HomeCountry) ? null : traveller.HomeCountry,
        IsSelf = string.Equals(traveller.LinkedUserId, userId, StringComparison.Ordinal),
        Traveller = new McpTravellerRef { Id = traveller.Id, Name = traveller.Name }
    };
}

public sealed class McpDeletedTraveller
{
    public McpTravellerRef Traveller { get; set; } = new();
    public bool Deleted { get; set; }
    public bool TripsDeleted { get; set; }
}

public sealed class McpCopyTripsResult
{
    public McpTravellerRef Traveller { get; set; } = new();
    public int Copied { get; set; }
    public int Skipped { get; set; }
}
