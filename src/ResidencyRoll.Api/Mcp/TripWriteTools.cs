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
    [Description("Saves one flight leg. warnings lists a duration far from the usual time, arrival before departure in UTC, a non-stop leg over 20 hours, or an overlap with another leg. The leg is saved even when warnings is not empty, unless onConflict is reject or skip. impact lists the country day-count changes in the rolling 365 days and in each calendar year the change touches. Ask the user to confirm before calling. IATA codes resolve city, country, and IANA timezone. Pass source, bookingRef, and notes to record where the leg came from.")]
    public static async Task<CallToolResult> CreateTrip(
        TripService trips,
        TravellerService travellerService,
        ResidencyCalculationService residency,
        AirportCatalog airports,
        ClaimsPrincipal user,
        McpTripLeg leg,
        [Description("Travellers to save this leg for, by id or name. Optional. Omit it to use traveller, or the signed-in person. The write is atomic across travellers.")] List<string>? travellers = null,
        [Description("save keeps an overlapping leg and returns a warning. reject and skip do not save it. Optional. Defaults to save.")] string? onConflict = null,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Write);
        var mode = McpTripWrites.Conflict(onConflict);
        var people = await McpTravellers.ResolveMany(travellerService, user, travellers, traveller);
        var actor = McpScopes.UserId(user);
        var resolved = TripLegResolver.Resolve(leg, people[0].OwnerUserId, airports);
        if (mode != McpTripWrites.Save)
        {
            foreach (var person in people)
            {
                var saved = await trips.GetAllTripsAsync(person.OwnerUserId, person);
                var applied = McpTripWrites.Apply([resolved], saved, mode);
                if (applied.Accepted.Count == 0)
                {
                    throw new McpException(string.Join("\n", applied.Warnings.Select(warning => warning.Message)));
                }
            }
        }
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

    [McpServerTool(Name = "create_trips", Title = "Create trips", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(McpWriteBatch))]
    [Description("Saves every valid leg or none of them, in one transaction. Every leg is checked before anything is saved, and all of the problems come back in one response. The result lists the new ids, a short summary, and any warnings. Pass detail full to include the legs. onConflict save keeps overlapping legs and warns. reject saves nothing when any new leg overlaps a saved one or another new leg. skip leaves the overlapping legs out and saves the rest. Ask the user to confirm before calling.")]
    public static async Task<CallToolResult> CreateTrips(
        TripService trips,
        TravellerService travellerService,
        AirportCatalog airports,
        ClaimsPrincipal user,
        [Description("Flight legs to save together.")] List<McpTripLeg> legs,
        [Description("Travellers to save these legs for, by id or name. Optional. Omit it to use traveller, or the signed-in person. The write is atomic across legs and travellers.")] List<string>? travellers = null,
        [Description("summary or full. Optional. Defaults to summary, which omits the full legs.")] string? detail = null,
        [Description("save, reject, or skip. Optional. Defaults to save.")] string? onConflict = null,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Write);
        var depth = McpTripWrites.Detail(detail);
        var mode = McpTripWrites.Conflict(onConflict);
        var people = await McpTravellers.ResolveMany(travellerService, user, travellers, traveller);
        var actor = McpScopes.UserId(user);
        var resolved = TripLegResolver.ResolveAll(legs ?? [], people[0].OwnerUserId, airports);
        var accepted = new List<Trip>();
        var warnings = new List<TripWarning>();
        if (people.Count == 1)
        {
            var person = people[0];
            var existing = await trips.GetAllTripsAsync(person.OwnerUserId, person);
            var applied = McpTripWrites.Apply(resolved, existing, mode);
            foreach (var trip in applied.Accepted)
            {
                trip.TravellerId = person.Id;
                trip.UserId = person.OwnerUserId;
                trip.CreatedByUserId = actor;
                accepted.Add(trip);
            }

            warnings.AddRange(applied.Warnings);
        }
        else
        {
            foreach (var person in people)
            {
                var existing = await trips.GetAllTripsAsync(person.OwnerUserId, person);
                var copies = resolved.Select(trip => TripCopies.Onto(trip, person, actor)).ToList();
                var applied = McpTripWrites.Apply(copies, existing, mode);
                accepted.AddRange(applied.Accepted);
                warnings.AddRange(applied.Warnings);
            }
        }

        var created = accepted.Count == 0 ? [] : await trips.CreateTripsAsync(accepted);
        var skipped = (resolved.Count * people.Count) - created.Count;
        var lead = $"Saved {created.Count} trip{(created.Count == 1 ? "" : "s")}.";
        if (skipped > 0)
        {
            lead += $" Skipped {skipped} overlapping leg{(skipped == 1 ? "" : "s")}.";
        }

        var batch = McpTripWrites.Batch(created, warnings, depth, lead);
        if (people.Count == 1)
        {
            return McpResults.For(people[0], batch.Summary, batch);
        }

        var names = string.Join(", ", people.Select(person => person.Name));
        return McpResults.Ok($"{names}: {batch.Summary}", batch);
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
            Source = written.Source,
            BookingRef = written.BookingRef,
            Notes = written.Notes,
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
