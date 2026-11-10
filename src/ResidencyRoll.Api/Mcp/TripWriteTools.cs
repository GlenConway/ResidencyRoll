using System.ComponentModel;
using System.Security.Claims;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ResidencyRoll.Api.Mappings;
using ResidencyRoll.Api.Models;
using ResidencyRoll.Api.Services;

namespace ResidencyRoll.Api.Mcp;

[McpServerToolType]
public sealed class TripWriteTools
{
    [McpServerTool(Name = "create_trip", Title = "Create a trip", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(McpTripWriteResult))]
    [Description("Saves one flight leg for the signed-in user. Uses the same checks as the trips API. warnings lists problems such as a duration far from the usual time, arrival before departure in UTC, a non-stop leg over 20 hours, or an overlap with another leg. The leg is saved even when warnings is not empty. impact lists the country day-count changes in the rolling 365 days and in each calendar year the change touches. Ask the user to confirm before calling. IATA codes resolve city, country, and IANA timezone; pass those explicitly for airports that are not in the table.")]
    public static async Task<CallToolResult> CreateTrip(
        TripService trips,
        TravellerService travellerService,
        ResidencyCalculationService residency,
        AirportCatalog airports,
        ClaimsPrincipal user,
        McpTripLeg leg,
        [Description("Travellers to save this leg for, by id or name. Optional. Omit it to use traveller, or the signed-in person. The write is atomic across travellers.")] List<string>? travellers = null,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Write);
        var people = await McpTravellers.ResolveMany(travellerService, user, travellers, traveller);
        var actor = McpScopes.UserId(user);
        var resolved = TripLegResolver.Resolve(leg, people[0].OwnerUserId, airports);
        if (people.Count == 1)
        {
            var person = people[0];
            var before = (await trips.GetAllTripsAsync(person.OwnerUserId, person)).Select(TripImpact.Snapshot).ToList();
            resolved.TravellerId = person.Id;
            resolved.UserId = person.OwnerUserId;
            resolved.CreatedByUserId = actor;
            var created = await trips.CreateTripAsync(resolved, actor);
            var after = await trips.GetAllTripsAsync(person.OwnerUserId, person);
            var result = WriteResult(created, after, TripImpact.Compare(before, after, residency, DateOnly.FromDateTime(DateTime.Today)));
            return McpResults.For(person, WriteText($"Saved trip {created.Id}: {created.DepartureIataCode ?? created.DepartureCity} to {created.ArrivalIataCode ?? created.ArrivalCity}.", result), result);
        }

        var createdMany = await trips.CreateForTravellersAsync(resolved, people, actor);
        var results = new List<McpTripWriteResult>();
        foreach (var created in createdMany)
        {
            var person = people.First(item => item.Id == created.TravellerId);
            var after = await trips.GetAllTripsAsync(person.OwnerUserId, person);
            var result = WriteResult(created, after, []);
            result.Traveller = new McpTravellerRef { Id = person.Id, Name = person.Name };
            results.Add(result);
        }

        var names = string.Join(", ", people.Select(person => person.Name));
        return McpResults.Ok($"{names}: Saved the leg for {people.Count} travellers.", results);
    }

    [McpServerTool(Name = "create_trips", Title = "Create trips", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(McpTripWriteResult[]))]
    [Description("Saves every leg or none of them, in one transaction. A bad leg leaves no partial import. Each saved leg includes warnings and the day-count impact of adding that leg. Ask the user to confirm before calling. Use this after parse_itinerary once the user accepts the draft.")]
    public static async Task<CallToolResult> CreateTrips(
        TripService trips,
        TravellerService travellerService,
        ResidencyCalculationService residency,
        AirportCatalog airports,
        ClaimsPrincipal user,
        [Description("Flight legs to save together.")] List<McpTripLeg> legs,
        [Description("Travellers to save these legs for, by id or name. Optional. Omit it to use traveller, or the signed-in person. The write is atomic across legs and travellers.")] List<string>? travellers = null,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Write);
        if (legs is null || legs.Count == 0)
        {
            throw new McpException("legs must contain at least one flight leg.");
        }

        var people = await McpTravellers.ResolveMany(travellerService, user, travellers, traveller);
        var actor = McpScopes.UserId(user);
        var resolved = legs.Select(leg => TripLegResolver.Resolve(leg, people[0].OwnerUserId, airports)).ToList();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var results = new List<McpTripWriteResult>();
        if (people.Count == 1)
        {
            var person = people[0];
            foreach (var trip in resolved)
            {
                trip.TravellerId = person.Id;
                trip.UserId = person.OwnerUserId;
                trip.CreatedByUserId = actor;
            }

            var rolling = (await trips.GetAllTripsAsync(person.OwnerUserId, person)).Select(TripImpact.Snapshot).ToList();
            var created = await trips.CreateTripsAsync(resolved);
            foreach (var trip in created)
            {
                var next = rolling.Append(TripImpact.Snapshot(trip)).ToList();
                var impact = TripImpact.Compare(rolling, next, residency, today);
                results.Add(WriteResult(trip, next, impact));
                rolling = next;
            }
        }
        else
        {
            var created = await trips.CreateLegsForTravellersAsync(resolved, people, actor);
            foreach (var trip in created)
            {
                var person = people.First(item => item.Id == trip.TravellerId);
                var saved = await trips.GetAllTripsAsync(person.OwnerUserId, person);
                var result = WriteResult(trip, saved, []);
                result.Traveller = new McpTravellerRef { Id = person.Id, Name = person.Name };
                results.Add(result);
            }
        }

        var warningCount = results.Sum(result => result.Warnings.Count);
        var text = $"Saved {results.Count} trip{(results.Count == 1 ? "" : "s")}.";
        if (warningCount > 0)
        {
            text += $" {warningCount} warning{(warningCount == 1 ? "" : "s")}: " + string.Join(" ", results.SelectMany(result => result.Warnings).Select(warning => warning.Message));
        }

        if (people.Count == 1)
        {
            return McpResults.For(people[0], text, results);
        }

        var names = string.Join(", ", people.Select(person => person.Name));
        return McpResults.Ok($"{names}: {text}", results);
    }

    [McpServerTool(Name = "update_trip", Title = "Update a trip", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(McpTripWriteResult))]
    [Description("Changes one flight leg. Send only the fields that should change; omitted fields stay as they are. warnings lists duration, ordering, and overlap problems. impact lists the country day-count changes in the rolling 365 days and the calendar years touched. A leg owned by someone else is reported as not found.")]
    public static async Task<CallToolResult> UpdateTrip(
        TripService trips,
        TravellerService travellerService,
        ResidencyCalculationService residency,
        AirportCatalog airports,
        ClaimsPrincipal user,
        [Description("Trip id.")] int id,
        [Description("Fields to change. Omit a field to leave it unchanged.")] McpTripLeg changes,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Write);
        var person = await McpTravellers.Resolve(travellerService, user, traveller);
        var actor = McpScopes.UserId(user);
        var before = (await trips.GetAllTripsAsync(person.OwnerUserId, person)).Select(TripImpact.Snapshot).ToList();
        var existing = await trips.GetTripByIdAsync(id, actor, person);
        if (existing is null)
        {
            throw new McpException("Trip not found.");
        }

        TripLegResolver.ApplyPatch(existing, changes, airports);
        var updated = await trips.UpdateTripAsync(existing, actor, person);
        var after = await trips.GetAllTripsAsync(person.OwnerUserId, person);
        var result = WriteResult(updated, after, TripImpact.Compare(before, after, residency, DateOnly.FromDateTime(DateTime.Today)));
        return McpResults.For(person, WriteText($"Updated trip {id}.", result), result);
    }

    [McpServerTool(Name = "delete_trip", Title = "Delete a trip", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(DeletedTripResult))]
    [Description("Deletes one flight leg. impact lists the country day-count changes in the rolling 365 days and the calendar years touched. A leg owned by someone else is reported as not found. Deleting an already deleted leg also reports not found.")]
    public static async Task<CallToolResult> DeleteTrip(
        TripService trips,
        TravellerService travellerService,
        ResidencyCalculationService residency,
        ClaimsPrincipal user,
        [Description("Trip id.")] int id,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Write);
        var person = await McpTravellers.Resolve(travellerService, user, traveller);
        var actor = McpScopes.UserId(user);
        var before = (await trips.GetAllTripsAsync(person.OwnerUserId, person)).Select(TripImpact.Snapshot).ToList();
        if (before.All(trip => trip.Id != id))
        {
            throw new McpException("Trip not found.");
        }

        await trips.DeleteTripAsync(id, actor, person);
        var after = await trips.GetAllTripsAsync(person.OwnerUserId, person);
        var impact = TripImpact.Compare(before, after, residency, DateOnly.FromDateTime(DateTime.Today));
        var result = new DeletedTripResult { Id = id, Deleted = true, Impact = impact };
        return McpResults.For(person, $"Deleted trip {id}. {TripImpact.Describe(impact)}", result);
    }

    private static McpTripWriteResult WriteResult(Trip trip, IReadOnlyList<Trip> all, List<DayCountChange> impact)
    {
        var written = McpTrip.From(trip.ToDto());
        return new McpTripWriteResult
        {
            Id = written.Id,
            DepartureCountry = written.DepartureCountry,
            DepartureCity = written.DepartureCity,
            DepartureDateTime = written.DepartureDateTime,
            DepartureTimezone = written.DepartureTimezone,
            DepartureIataCode = written.DepartureIataCode,
            ArrivalCountry = written.ArrivalCountry,
            ArrivalCity = written.ArrivalCity,
            ArrivalDateTime = written.ArrivalDateTime,
            ArrivalTimezone = written.ArrivalTimezone,
            ArrivalIataCode = written.ArrivalIataCode,
            CountryName = written.CountryName,
            DurationDays = written.DurationDays,
            DurationHours = written.DurationHours,
            DurationMinutes = written.DurationMinutes,
            Warnings = TripAuditor.InspectLeg(trip, all),
            Impact = impact
        };
    }

    private static string WriteText(string lead, McpTripWriteResult result)
    {
        var text = lead + " " + TripImpact.Describe(result.Impact);
        if (result.Warnings.Count > 0)
        {
            text += " Warnings: " + string.Join(" ", result.Warnings.Select(warning => warning.Message));
        }

        return text;
    }
}

public sealed class McpTripWriteResult : McpTrip
{
    public List<TripWarning> Warnings { get; set; } = [];
    public List<DayCountChange> Impact { get; set; } = [];
}

public sealed class DeletedTripResult
{
    public McpTravellerRef Traveller { get; set; } = new();

    public int Id { get; set; }
    public bool Deleted { get; set; }
    public List<DayCountChange> Impact { get; set; } = [];
}
