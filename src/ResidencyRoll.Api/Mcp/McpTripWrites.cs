using System.ComponentModel;
using ModelContextProtocol;
using ResidencyRoll.Api.Mappings;
using ResidencyRoll.Api.Models;
using ResidencyRoll.Api.Services;

namespace ResidencyRoll.Api.Mcp;

internal static class McpTripWrites
{
    public const string Summary = "summary";
    public const string Full = "full";
    public const string Save = "save";
    public const string Reject = "reject";
    public const string Skip = "skip";

    public static string Detail(string? detail)
    {
        var value = string.IsNullOrWhiteSpace(detail) ? Summary : detail.Trim().ToLowerInvariant();
        if (value is not (Summary or Full))
        {
            throw new McpException("detail must be summary or full.");
        }

        return value;
    }

    public static string Conflict(string? onConflict)
    {
        var value = string.IsNullOrWhiteSpace(onConflict) ? Save : onConflict.Trim().ToLowerInvariant();
        if (value is not (Save or Reject or Skip))
        {
            throw new McpException("onConflict must be save, reject, or skip.");
        }

        return value;
    }

    public static (List<Trip> Accepted, List<TripWarning> Warnings) Apply(
        IReadOnlyList<Trip> incoming,
        IReadOnlyList<Trip> existing,
        string mode)
    {
        if (mode == Reject)
        {
            var overlaps = Overlaps(incoming, existing.Concat(incoming).ToList());
            if (overlaps.Count > 0)
            {
                throw new McpException("Overlapping legs were not saved.\n" + string.Join("\n", overlaps.Select(warning => warning.Message)));
            }

            return (incoming.ToList(), WarningsFor(incoming, existing));
        }

        if (mode == Skip)
        {
            var accepted = new List<Trip>();
            var skipped = new List<TripWarning>();
            foreach (var leg in incoming)
            {
                var overlaps = Overlaps([leg], existing.Concat(accepted).ToList());
                if (overlaps.Count > 0)
                {
                    skipped.AddRange(overlaps.Select(warning => new TripWarning
                    {
                        Code = warning.Code,
                        TripId = warning.TripId,
                        OtherTripId = warning.OtherTripId,
                        Message = "Skipped: " + warning.Message
                    }));
                    continue;
                }

                accepted.Add(leg);
            }

            return (accepted, Distinct(skipped.Concat(WarningsFor(accepted, existing))));
        }

        return (incoming.ToList(), WarningsFor(incoming, existing));
    }

    public static McpWriteBatch Batch(IReadOnlyList<Trip> saved, IReadOnlyList<TripWarning> warnings, string detail, string? lead = null)
    {
        var summary = lead ?? $"Saved {saved.Count} trip{(saved.Count == 1 ? "" : "s")}.";
        if (warnings.Count > 0)
        {
            summary += $" {warnings.Count} warning{(warnings.Count == 1 ? "" : "s")}: " + string.Join(" ", warnings.Select(warning => warning.Message));
        }

        return new McpWriteBatch
        {
            Saved = saved.Count,
            Ids = saved.Select(trip => trip.Id).ToList(),
            Summary = summary,
            Warnings = warnings.ToList(),
            Trips = detail == Full ? saved.Select(FullLeg).ToList() : null
        };
    }

    public static McpCompactTrip Compact(Trip trip) => new()
    {
        Id = trip.Id == 0 ? null : trip.Id,
        DepartureIataCode = trip.DepartureIataCode,
        ArrivalIataCode = trip.ArrivalIataCode,
        DepartureDateTime = McpTime.Format(trip.DepartureDateTime),
        ArrivalDateTime = McpTime.Format(trip.ArrivalDateTime),
        Source = trip.Source,
        BookingRef = trip.BookingRef,
        Notes = trip.Notes
    };

    private static McpTripWriteResult FullLeg(Trip trip)
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
            Warnings = []
        };
    }

    private static List<TripWarning> Overlaps(IReadOnlyList<Trip> incoming, IReadOnlyList<Trip> others)
    {
        var warnings = new List<TripWarning>();
        foreach (var leg in incoming)
        {
            warnings.AddRange(TripAuditor.InspectLeg(leg, others).Where(warning => warning.Code == "overlap"));
        }

        return Distinct(warnings);
    }

    private static List<TripWarning> WarningsFor(IReadOnlyList<Trip> incoming, IReadOnlyList<Trip> existing)
    {
        var context = existing.Concat(incoming).ToList();
        var warnings = new List<TripWarning>();
        foreach (var leg in incoming)
        {
            warnings.AddRange(TripAuditor.InspectLeg(leg, context));
        }

        return Distinct(warnings);
    }

    private static List<TripWarning> Distinct(IEnumerable<TripWarning> warnings)
    {
        var seen = new HashSet<string>();
        var unique = new List<TripWarning>();
        foreach (var warning in warnings)
        {
            var key = $"{warning.Code}|{warning.TripId}|{warning.OtherTripId}|{warning.Message}";
            if (seen.Add(key))
            {
                unique.Add(warning);
            }
        }

        return unique;
    }
}

public sealed class McpWriteBatch
{
    public McpTravellerRef Traveller { get; set; } = new();
    public int Saved { get; set; }
    public List<int> Ids { get; set; } = [];

    [Description("Short description of what was saved, including any warnings.")]
    public string Summary { get; set; } = string.Empty;

    public List<TripWarning> Warnings { get; set; } = [];

    [Description("Full legs. Present only when detail is full.")]
    public List<McpTripWriteResult>? Trips { get; set; }
}

public sealed class McpCompactTrip
{
    public int? Id { get; set; }
    public string? DepartureIataCode { get; set; }
    public string? ArrivalIataCode { get; set; }

    [Description(McpTime.LocalTimeDescription)]
    public string DepartureDateTime { get; set; } = string.Empty;

    [Description(McpTime.LocalTimeDescription)]
    public string ArrivalDateTime { get; set; } = string.Empty;

    public string? Source { get; set; }
    public string? BookingRef { get; set; }
    public string? Notes { get; set; }
}

public sealed class McpReplaceResult
{
    public McpTravellerRef Traveller { get; set; } = new();
    public List<int> DeletedIds { get; set; } = [];
    public int Saved { get; set; }
    public List<int> Ids { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
    public List<TripWarning> Warnings { get; set; } = [];
    public List<McpTripWriteResult>? Trips { get; set; }
}

public sealed class McpDeleteTripsResult
{
    public McpTravellerRef Traveller { get; set; } = new();
    public List<int> Ids { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
    public List<DayCountChange> Impact { get; set; } = [];
}

public sealed class McpReconcileResult
{
    public McpTravellerRef Traveller { get; set; } = new();
    public int Matched { get; set; }
    public List<McpCompactTrip> Missing { get; set; } = [];
    public List<McpTripDifference> Different { get; set; } = [];
    public List<McpCompactTrip> Extra { get; set; } = [];
}

public sealed class McpTripDifference
{
    public McpCompactTrip Incoming { get; set; } = new();
    public McpCompactTrip Saved { get; set; } = new();
    public List<string> Fields { get; set; } = [];
}

public sealed class McpTripExport
{
    public McpTravellerRef Traveller { get; set; } = new();
    public int Count { get; set; }
    public string Csv { get; set; } = string.Empty;
    public List<McpCompactTrip> Trips { get; set; } = [];
}

public sealed class McpServerInfo
{
    public string Name { get; set; } = "ResidencyRoll";
    public string Version { get; set; } = string.Empty;
    public string Commit { get; set; } = string.Empty;
    public int AirportCount { get; set; }
}
