using System.ComponentModel;
using System.Security.Claims;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ResidencyRoll.Api.Mappings;
using ResidencyRoll.Api.Services;

namespace ResidencyRoll.Api.Mcp;

[McpServerToolType]
public sealed class TripWriteTools
{
    [McpServerTool(Name = "create_trip", Title = "Create a trip", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(McpTrip))]
    [Description("Saves one flight leg for the signed-in user. Uses the same checks as the trips API. Ask the user to confirm before calling. IATA codes resolve city, country, and IANA timezone; pass those explicitly for airports that are not in the table.")]
    public static async Task<CallToolResult> CreateTrip(
        TripService trips,
        AirportCatalog airports,
        ClaimsPrincipal user,
        McpTripLeg leg)
    {
        McpScopes.Require(user, McpScopes.Write);
        var created = await trips.CreateTripAsync(TripLegResolver.Resolve(leg, McpScopes.UserId(user), airports));
        var dto = McpTrip.From(created.ToDto());
        return McpResults.Ok($"Saved trip {dto.Id}: {dto.DepartureIataCode ?? dto.DepartureCity} to {dto.ArrivalIataCode ?? dto.ArrivalCity}.", dto);
    }

    [McpServerTool(Name = "create_trips", Title = "Create trips", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(McpTrip[]))]
    [Description("Saves every leg or none of them, in one transaction. A bad leg leaves no partial import. Ask the user to confirm before calling. Use this after parse_itinerary once the user accepts the draft.")]
    public static async Task<CallToolResult> CreateTrips(
        TripService trips,
        AirportCatalog airports,
        ClaimsPrincipal user,
        [Description("Flight legs to save together.")] List<McpTripLeg> legs)
    {
        McpScopes.Require(user, McpScopes.Write);
        if (legs is null || legs.Count == 0)
        {
            throw new McpException("legs must contain at least one flight leg.");
        }

        var userId = McpScopes.UserId(user);
        var resolved = legs.Select(leg => TripLegResolver.Resolve(leg, userId, airports)).ToList();
        var created = await trips.CreateTripsAsync(resolved);
        var dtos = created.Select(trip => McpTrip.From(trip.ToDto())).ToList();
        return McpResults.Ok($"Saved {dtos.Count} trip{(dtos.Count == 1 ? "" : "s")}.", dtos);
    }

    [McpServerTool(Name = "update_trip", Title = "Update a trip", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(McpTrip))]
    [Description("Replaces one flight leg. Send the full leg, not a partial patch. A leg owned by someone else is reported as not found. Calling again with the same leg does not create a second copy.")]
    public static async Task<CallToolResult> UpdateTrip(
        TripService trips,
        AirportCatalog airports,
        ClaimsPrincipal user,
        [Description("Trip id.")] int id,
        [Description("Replacement flight leg.")] McpTripLeg changes)
    {
        McpScopes.Require(user, McpScopes.Write);
        var userId = McpScopes.UserId(user);
        var existing = await trips.GetTripByIdAsync(id, userId);
        if (existing is null)
        {
            throw new McpException("Trip not found.");
        }

        var resolved = TripLegResolver.Resolve(changes, userId, airports);
        existing.DepartureCountry = resolved.DepartureCountry;
        existing.DepartureCity = resolved.DepartureCity;
        existing.DepartureDateTime = resolved.DepartureDateTime;
        existing.DepartureTimezone = resolved.DepartureTimezone;
        existing.DepartureIataCode = resolved.DepartureIataCode;
        existing.ArrivalCountry = resolved.ArrivalCountry;
        existing.ArrivalCity = resolved.ArrivalCity;
        existing.ArrivalDateTime = resolved.ArrivalDateTime;
        existing.ArrivalTimezone = resolved.ArrivalTimezone;
        existing.ArrivalIataCode = resolved.ArrivalIataCode;
        var updated = await trips.UpdateTripAsync(existing, userId);
        return McpResults.Ok($"Updated trip {id}.", McpTrip.From(updated.ToDto()));
    }

    [McpServerTool(Name = "delete_trip", Title = "Delete a trip", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(DeletedTripResult))]
    [Description("Deletes one flight leg. A leg owned by someone else is reported as not found. Deleting an already deleted leg also reports not found.")]
    public static async Task<CallToolResult> DeleteTrip(
        TripService trips,
        ClaimsPrincipal user,
        [Description("Trip id.")] int id)
    {
        McpScopes.Require(user, McpScopes.Write);
        var userId = McpScopes.UserId(user);
        var existing = await trips.GetTripByIdAsync(id, userId);
        if (existing is null)
        {
            throw new McpException("Trip not found.");
        }

        await trips.DeleteTripAsync(id, userId);
        return McpResults.Ok($"Deleted trip {id}.", new DeletedTripResult { Id = id, Deleted = true });
    }
}

public sealed class DeletedTripResult
{
    public int Id { get; set; }
    public bool Deleted { get; set; }
}
