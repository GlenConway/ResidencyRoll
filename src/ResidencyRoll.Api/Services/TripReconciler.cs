using ResidencyRoll.Api.Models;

namespace ResidencyRoll.Api.Services;

/// <summary>
/// Compares a proposed set of legs with the legs already saved.
/// A booking reference matches first. Otherwise the same airports and local times match,
/// and the same airports on the same departure date with different times are a difference.
/// </summary>
public static class TripReconciler
{
    public sealed class Report
    {
        public int Matched { get; init; }
        public List<Difference> Different { get; init; } = [];
        public List<Trip> Missing { get; init; } = [];
        public List<Trip> Extra { get; init; } = [];
    }

    public sealed class Difference
    {
        public Trip Incoming { get; init; } = new();
        public Trip Saved { get; init; } = new();
        public List<string> Fields { get; init; } = [];
    }

    public static Report Compare(IReadOnlyList<Trip> incoming, IReadOnlyList<Trip> saved)
    {
        var unused = saved.ToList();
        var matched = 0;
        var different = new List<Difference>();
        var missing = new List<Trip>();

        var remaining = new List<Trip>();
        foreach (var leg in incoming)
        {
            var byReference = FindBooking(leg, unused);
            if (byReference is not null)
            {
                unused.Remove(byReference);
                var fields = ChangedFields(leg, byReference);
                if (fields.Count == 0)
                {
                    matched++;
                }
                else
                {
                    different.Add(new Difference { Incoming = leg, Saved = byReference, Fields = fields });
                }

                continue;
            }

            remaining.Add(leg);
        }

        var still = new List<Trip>();
        foreach (var leg in remaining)
        {
            var exact = unused.FirstOrDefault(savedLeg => SameInstant(leg, savedLeg));
            if (exact is null)
            {
                still.Add(leg);
                continue;
            }

            unused.Remove(exact);
            matched++;
        }

        var pending = still
            .GroupBy(RouteDate)
            .ToDictionary(group => group.Key, group => group.OrderBy(leg => leg.DepartureDateTime).ToList());
        var savedGroups = unused
            .GroupBy(RouteDate)
            .ToDictionary(group => group.Key, group => group.OrderBy(leg => leg.DepartureDateTime).ToList());

        foreach (var (key, legs) in pending)
        {
            if (!savedGroups.TryGetValue(key, out var savedLegs))
            {
                missing.AddRange(legs);
                continue;
            }

            var pairs = Math.Min(legs.Count, savedLegs.Count);
            for (var index = 0; index < pairs; index++)
            {
                var fields = ChangedFields(legs[index], savedLegs[index]);
                if (fields.Count == 0)
                {
                    matched++;
                }
                else
                {
                    different.Add(new Difference { Incoming = legs[index], Saved = savedLegs[index], Fields = fields });
                }

                unused.Remove(savedLegs[index]);
            }

            if (legs.Count > savedLegs.Count)
            {
                missing.AddRange(legs.Skip(savedLegs.Count));
            }
        }

        return new Report
        {
            Matched = matched,
            Different = different,
            Missing = missing,
            Extra = unused
        };
    }

    private static Trip? FindBooking(Trip leg, IReadOnlyList<Trip> saved)
    {
        if (string.IsNullOrWhiteSpace(leg.BookingRef))
        {
            return null;
        }

        return saved.FirstOrDefault(savedLeg =>
            !string.IsNullOrWhiteSpace(savedLeg.BookingRef)
            && savedLeg.BookingRef.Equals(leg.BookingRef, StringComparison.OrdinalIgnoreCase));
    }

    private static bool SameInstant(Trip left, Trip right) =>
        SameCode(left.DepartureIataCode, right.DepartureIataCode)
        && SameCode(left.ArrivalIataCode, right.ArrivalIataCode)
        && left.DepartureDateTime == right.DepartureDateTime
        && left.ArrivalDateTime == right.ArrivalDateTime;

    private static string RouteDate(Trip trip) =>
        $"{Norm(trip.DepartureIataCode)}|{Norm(trip.ArrivalIataCode)}|{trip.DepartureDateTime:yyyy-MM-dd}";

    private static List<string> ChangedFields(Trip incoming, Trip saved)
    {
        var fields = new List<string>();
        if (!SameCode(incoming.DepartureIataCode, saved.DepartureIataCode))
        {
            fields.Add("departureIataCode");
        }

        if (!SameCode(incoming.ArrivalIataCode, saved.ArrivalIataCode))
        {
            fields.Add("arrivalIataCode");
        }

        if (incoming.DepartureDateTime != saved.DepartureDateTime)
        {
            fields.Add("departureDateTime");
        }

        if (incoming.ArrivalDateTime != saved.ArrivalDateTime)
        {
            fields.Add("arrivalDateTime");
        }

        if (!SameText(incoming.Source, saved.Source))
        {
            fields.Add("source");
        }

        if (!SameText(incoming.BookingRef, saved.BookingRef))
        {
            fields.Add("bookingRef");
        }

        if (!SameText(incoming.Notes, saved.Notes))
        {
            fields.Add("notes");
        }

        return fields;
    }

    private static bool SameCode(string? left, string? right) =>
        string.Equals(Norm(left), Norm(right), StringComparison.Ordinal);

    private static bool SameText(string? left, string? right) =>
        string.Equals(left?.Trim() ?? "", right?.Trim() ?? "", StringComparison.OrdinalIgnoreCase);

    private static string Norm(string? code) => string.IsNullOrWhiteSpace(code) ? "" : code.Trim().ToUpperInvariant();
}
