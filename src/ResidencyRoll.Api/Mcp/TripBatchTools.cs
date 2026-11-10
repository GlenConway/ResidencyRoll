using System.ComponentModel;
using System.Security.Claims;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ResidencyRoll.Api.Models;
using ResidencyRoll.Api.Services;

namespace ResidencyRoll.Api.Mcp;

[McpServerToolType]
public sealed class TripBatchTools
{
    [McpServerTool(Name = "delete_trips", Title = "Delete trips", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(McpDeleteTripsResult))]
    [Description("Deletes every listed leg, or none of them, in one transaction. A missing id deletes nothing and names every missing id. impact is the combined day-count change.")]
    public static async Task<CallToolResult> DeleteTrips(
        TripService trips,
        TravellerService travellerService,
        ResidencyCalculationService residency,
        ClaimsPrincipal user,
        [Description("Trip ids to delete together.")] List<int> ids,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Write);
        if (ids is null || ids.Count == 0)
        {
            throw new McpException("ids must contain at least one trip id.");
        }

        var person = await McpTravellers.Resolve(travellerService, user, traveller);
        var actor = McpScopes.UserId(user);
        var before = (await trips.GetAllTripsAsync(person.OwnerUserId, person)).Select(TripImpact.Snapshot).ToList();
        try
        {
            await trips.DeleteTripsAsync(ids, actor, person);
        }
        catch (InvalidOperationException ex)
        {
            throw new McpException(ex.Message);
        }

        var after = await trips.GetAllTripsAsync(person.OwnerUserId, person);
        var impact = TripImpact.Compare(before, after, residency, DateOnly.FromDateTime(DateTime.Today));
        var distinct = ids.Distinct().OrderBy(id => id).ToList();
        var result = new McpDeleteTripsResult
        {
            Ids = distinct,
            Summary = $"Deleted {distinct.Count} trip{(distinct.Count == 1 ? "" : "s")}. {TripImpact.Describe(impact)}",
            Impact = impact
        };
        return McpResults.For(person, result.Summary, result);
    }

    [McpServerTool(Name = "replace_trips", Title = "Replace trips", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(McpReplaceResult))]
    [Description("Deletes deleteIds and saves legs in one transaction. Nothing changes when a leg is invalid or an id is missing, and every problem is returned together. onConflict compares the new legs with the trips that remain after the deletes. The result lists the deleted ids, the new ids, a short summary, and warnings. Pass detail full for the saved legs.")]
    public static async Task<CallToolResult> ReplaceTrips(
        TripService trips,
        TravellerService travellerService,
        AirportCatalog airports,
        ClaimsPrincipal user,
        [Description("Trip ids to delete. Optional when only adding legs.")] List<int>? deleteIds,
        [Description("Legs to save after the deletes. Optional when only deleting.")] List<McpTripLeg>? legs,
        [Description("summary or full. Optional. Defaults to summary.")] string? detail = null,
        [Description("save, reject, or skip, applied to the new legs. Optional. Defaults to save.")] string? onConflict = null,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Write);
        var depth = McpTripWrites.Detail(detail);
        var mode = McpTripWrites.Conflict(onConflict);
        var person = await McpTravellers.Resolve(travellerService, user, traveller);
        var actor = McpScopes.UserId(user);
        var ids = deleteIds?.Distinct().ToList() ?? [];
        var resolved = legs is { Count: > 0 }
            ? TripLegResolver.ResolveAll(legs, person.OwnerUserId, airports)
            : [];
        if (ids.Count == 0 && resolved.Count == 0)
        {
            throw new McpException("deleteIds or legs must contain something to change.");
        }

        var existing = await trips.GetAllTripsAsync(person.OwnerUserId, person);
        var remaining = existing.Where(trip => !ids.Contains(trip.Id)).ToList();
        var missing = ids.Where(id => existing.All(trip => trip.Id != id)).OrderBy(id => id).ToList();
        if (missing.Count > 0)
        {
            throw new McpException($"Trips not found: {string.Join(", ", missing)}.");
        }

        var applied = McpTripWrites.Apply(resolved, remaining, mode);
        foreach (var trip in applied.Accepted)
        {
            trip.TravellerId = person.Id;
            trip.UserId = person.OwnerUserId;
            trip.CreatedByUserId = actor;
        }

        List<Trip> created;
        try
        {
            created = await trips.ReplaceTripsAsync(ids, applied.Accepted, actor, person);
        }
        catch (InvalidOperationException ex)
        {
            throw new McpException(ex.Message);
        }

        var skipped = resolved.Count - created.Count;
        var summary = $"Deleted {ids.Count} trip{(ids.Count == 1 ? "" : "s")} and saved {created.Count}.";
        if (skipped > 0)
        {
            summary += $" Skipped {skipped} overlapping leg{(skipped == 1 ? "" : "s")}.";
        }

        if (applied.Warnings.Count > 0)
        {
            summary += $" {applied.Warnings.Count} warning{(applied.Warnings.Count == 1 ? "" : "s")}: " + string.Join(" ", applied.Warnings.Select(warning => warning.Message));
        }

        var result = new McpReplaceResult
        {
            DeletedIds = ids,
            Saved = created.Count,
            Ids = created.Select(trip => trip.Id).ToList(),
            Summary = summary,
            Warnings = applied.Warnings,
            Trips = depth == McpTripWrites.Full ? McpTripWrites.Batch(created, applied.Warnings, depth).Trips : null
        };
        return McpResults.For(person, summary, result);
    }

    [McpServerTool(Name = "reconcile_trips", Title = "Reconcile trips", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(McpReconcileResult))]
    [Description("Compares legs with the saved trips and changes nothing. missing are in the set but not saved. different match a saved leg by booking reference, or by the same airports on the same departure date, and name the fields that differ. extra are saved legs that are not in the set. Every invalid leg is reported together and nothing is compared until they are all valid.")]
    public static async Task<CallToolResult> ReconcileTrips(
        TripService trips,
        TravellerService travellerService,
        AirportCatalog airports,
        ClaimsPrincipal user,
        [Description("Legs to compare with the saved trips.")] List<McpTripLeg> legs,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Read);
        var person = await McpTravellers.Resolve(travellerService, user, traveller);
        var incoming = TripLegResolver.ResolveAll(legs ?? [], person.OwnerUserId, airports);
        var saved = await trips.GetAllTripsAsync(person.OwnerUserId, person);
        var report = TripReconciler.Compare(incoming, saved);
        var result = new McpReconcileResult
        {
            Matched = report.Matched,
            Missing = report.Missing.Select(McpTripWrites.Compact).ToList(),
            Different = report.Different.Select(difference => new McpTripDifference
            {
                Incoming = McpTripWrites.Compact(difference.Incoming),
                Saved = McpTripWrites.Compact(difference.Saved),
                Fields = difference.Fields
            }).ToList(),
            Extra = report.Extra.Select(McpTripWrites.Compact).ToList()
        };
        var text = $"Matched {report.Matched}. Missing {result.Missing.Count}. Different {result.Different.Count}. Extra {result.Extra.Count}.";
        return McpResults.For(person, text, result);
    }

    [McpServerTool(Name = "export_trips", Title = "Export trips", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(McpTripExport))]
    [Description("Returns the traveller's legs as CSV in the same columns as the web export, plus a compact list of id, airports, local times, source, bookingRef, and notes. Use this instead of copying list_trips by hand.")]
    public static async Task<CallToolResult> ExportTrips(
        TripService trips,
        TravellerService travellerService,
        ClaimsPrincipal user,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Read);
        var person = await McpTravellers.Resolve(travellerService, user, traveller);
        var saved = await trips.GetAllTripsAsync(person.OwnerUserId, person);
        var result = new McpTripExport
        {
            Count = saved.Count,
            Csv = TripCsv.Format(saved, person.Name),
            Trips = saved.Select(McpTripWrites.Compact).ToList()
        };
        return McpResults.For(person, $"Exported {saved.Count} trip{(saved.Count == 1 ? "" : "s")}.", result);
    }

    [McpServerTool(Name = "import_trips_csv", Title = "Import trips CSV", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(McpWriteBatch))]
    [Description("Imports CSV in the same format as the web trip export. Every row is checked first. Any error, including an unknown traveller name, saves nothing and lists every problem. A Traveller column chooses the person for that row. Rows without one use traveller, or the signed-in person. onConflict works the same way as create_trips. The result is the new ids, a short summary, and warnings.")]
    public static async Task<CallToolResult> ImportTripsCsv(
        TripService trips,
        TravellerService travellerService,
        ClaimsPrincipal user,
        [Description("CSV text, including the header row when Source, BookingRef, Notes, or Traveller are present.")] string csv,
        [Description("summary or full. Optional. Defaults to summary.")] string? detail = null,
        [Description("save, reject, or skip. Optional. Defaults to save.")] string? onConflict = null,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Write);
        var depth = McpTripWrites.Detail(detail);
        var mode = McpTripWrites.Conflict(onConflict);
        var parsed = TripCsv.Parse(csv);
        if (parsed.Errors.Count > 0)
        {
            throw new McpException(string.Join("\n", parsed.Errors));
        }

        var fallback = await McpTravellers.Resolve(travellerService, user, traveller);
        var actor = McpScopes.UserId(user);
        var errors = new List<string>();
        var staged = new List<(Trip Trip, Traveller Person)>();
        var named = new Dictionary<string, Traveller>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < parsed.Rows.Count; index++)
        {
            var row = parsed.Rows[index];
            var person = fallback;
            if (!string.IsNullOrWhiteSpace(row.TravellerName))
            {
                if (!named.TryGetValue(row.TravellerName, out person!))
                {
                    try
                    {
                        person = await travellerService.ResolveAsync(fallback.OwnerUserId, row.TravellerName);
                        named[row.TravellerName] = person;
                    }
                    catch (TravellerResolutionException ex)
                    {
                        errors.Add($"Row {index + 1}: {ex.Message}");
                        continue;
                    }
                }
            }

            staged.Add((row.Trip, person));
        }

        if (errors.Count > 0)
        {
            throw new McpException(string.Join("\n", errors));
        }

        var accepted = new List<Trip>();
        var warnings = new List<TripWarning>();
        foreach (var group in staged.GroupBy(item => item.Person.Id))
        {
            var person = group.First().Person;
            var existing = await trips.GetAllTripsAsync(person.OwnerUserId, person);
            var copies = group.Select(item => TripCopies.Onto(item.Trip, person, actor)).ToList();
            var applied = McpTripWrites.Apply(copies, existing, mode);
            accepted.AddRange(applied.Accepted);
            warnings.AddRange(applied.Warnings);
        }

        var created = accepted.Count == 0 ? [] : await trips.CreateTripsAsync(accepted);
        var batch = McpTripWrites.Batch(created, warnings, depth, $"Imported {created.Count} trip{(created.Count == 1 ? "" : "s")}.");
        if (staged.Select(item => item.Person.Id).Distinct().Count() == 1)
        {
            return McpResults.For(staged[0].Person, batch.Summary, batch);
        }

        return McpResults.Ok(batch.Summary, batch);
    }
}
