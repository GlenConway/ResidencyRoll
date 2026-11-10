using System.ComponentModel;
using ResidencyRoll.Api.Models;

namespace ResidencyRoll.Api.Services;

/// <summary>
/// Warnings for legs that look wrong. A warning does not block the save.
/// </summary>
public static class TripAuditor
{
    public const int LongHaulHours = 20;

    public static List<TripWarning> InspectAll(IReadOnlyList<Trip> trips)
    {
        var ordered = trips.OrderBy(DepartureUtc).ThenBy(trip => trip.Id).ToList();
        var warnings = new List<TripWarning>();
        foreach (var trip in ordered)
        {
            warnings.AddRange(InspectLeg(trip));
        }

        for (var i = 0; i < ordered.Count; i++)
        {
            for (var j = i + 1; j < ordered.Count; j++)
            {
                if (Overlaps(ordered[i], ordered[j]))
                {
                    warnings.Add(OverlapWarning(ordered[i], ordered[j]));
                }
            }

            if (i + 1 < ordered.Count)
            {
                var gap = CountryGap(ordered[i], ordered[i + 1]);
                if (gap is not null)
                {
                    warnings.Add(gap);
                }
            }
        }

        return warnings;
    }

    public static List<TripWarning> InspectLeg(Trip trip, IReadOnlyList<Trip>? others = null)
    {
        var warnings = new List<TripWarning>();
        warnings.AddRange(InspectLeg(trip));
        if (others is null)
        {
            return warnings;
        }

        foreach (var other in others)
        {
            if (other.Id == trip.Id && other.Id != 0)
            {
                continue;
            }

            if (ReferenceEquals(other, trip))
            {
                continue;
            }

            if (Overlaps(trip, other))
            {
                warnings.Add(OverlapWarning(trip, other));
            }
        }

        return warnings;
    }

    private static IEnumerable<TripWarning> InspectLeg(Trip trip)
    {
        if (!TryUtc(trip.DepartureDateTime, trip.DepartureTimezone, out var departureUtc)
            || !TryUtc(trip.ArrivalDateTime, trip.ArrivalTimezone, out var arrivalUtc))
        {
            yield break;
        }

        var minutes = (int)Math.Round((arrivalUtc - departureUtc).TotalMinutes);
        var route = $"{Code(trip, departure: true)} to {Code(trip, departure: false)}";
        if (minutes < 0)
        {
            yield return new TripWarning
            {
                TripId = IdOrNull(trip),
                Code = "arrivalBeforeDeparture",
                Message = $"{route} arrives before it departs in UTC ({trip.DepartureDateTime:yyyy-MM-dd HH:mm} {trip.DepartureTimezone} to {trip.ArrivalDateTime:yyyy-MM-dd HH:mm} {trip.ArrivalTimezone})."
            };
            yield break;
        }

        var hours = minutes / 60.0;
        if (hours > LongHaulHours)
        {
            yield return new TripWarning
            {
                TripId = IdOrNull(trip),
                Code = "longHaul",
                Message = $"{route} is {hours:0.#} hours. A non-stop leg over {LongHaulHours} hours is unusual; check the dates and times."
            };
            yield break;
        }

        if (TryExpectedHours(trip, out var expected) && (hours < expected * 0.6 || hours > expected * 1.8))
        {
            yield return new TripWarning
            {
                TripId = IdOrNull(trip),
                Code = "unusualDuration",
                Message = $"{route} is {hours:0.#} hours. The timezone difference suggests about {expected:0.#} hours."
            };
        }
    }

    private static TripWarning? CountryGap(Trip current, Trip next)
    {
        if (string.IsNullOrWhiteSpace(current.ArrivalCountry) || string.IsNullOrWhiteSpace(next.DepartureCountry))
        {
            return null;
        }

        if (current.ArrivalCountry.Equals(next.DepartureCountry, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (Overlaps(current, next))
        {
            return null;
        }

        return new TripWarning
        {
            TripId = IdOrNull(current),
            OtherTripId = IdOrNull(next),
            Code = "countryGap",
            Message = $"Trip {current.Id} arrives in {current.ArrivalCountry}, and the next leg (trip {next.Id}) departs from {next.DepartureCountry}."
        };
    }

    private static bool Overlaps(Trip left, Trip right)
    {
        if (!TryInterval(left, out var leftStart, out var leftEnd) || !TryInterval(right, out var rightStart, out var rightEnd))
        {
            return false;
        }

        return leftStart < rightEnd && rightStart < leftEnd;
    }

    private static TripWarning OverlapWarning(Trip trip, Trip other) => new()
    {
        TripId = IdOrNull(trip),
        OtherTripId = IdOrNull(other),
        Code = "overlap",
        Message = $"{Describe(trip)} overlaps {Describe(other)}."
    };

    private static bool TryInterval(Trip trip, out DateTime start, out DateTime end)
    {
        start = default;
        end = default;
        if (!TryUtc(trip.DepartureDateTime, trip.DepartureTimezone, out var departure)
            || !TryUtc(trip.ArrivalDateTime, trip.ArrivalTimezone, out var arrival)
            || arrival < departure)
        {
            return false;
        }

        start = departure;
        end = arrival;
        return true;
    }

    private static bool TryExpectedHours(Trip trip, out double expected)
    {
        expected = 0;
        if (!TryOffsetHours(trip.DepartureDateTime, trip.DepartureTimezone, out var departureOffset)
            || !TryOffsetHours(trip.ArrivalDateTime, trip.ArrivalTimezone, out var arrivalOffset))
        {
            return false;
        }

        var difference = Math.Abs(arrivalOffset - departureOffset);
        if (difference < 3)
        {
            return false;
        }

        expected = difference + 3;
        return true;
    }

    private static bool TryOffsetHours(DateTime local, string? timezone, out double hours)
    {
        hours = 0;
        if (!TryUtc(local, timezone, out var utc))
        {
            return false;
        }

        var unspecified = local.Kind == DateTimeKind.Unspecified ? local : DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        hours = (unspecified - utc).TotalHours;
        return true;
    }

    private static DateTime DepartureUtc(Trip trip) =>
        TryUtc(trip.DepartureDateTime, trip.DepartureTimezone, out var utc) ? utc : DateTime.MaxValue;

    public static bool TryUtc(DateTime local, string? timezoneId, out DateTime utc)
    {
        var unspecified = local.Kind == DateTimeKind.Unspecified ? local : DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var zoneId = string.IsNullOrWhiteSpace(timezoneId) ? "UTC" : timezoneId;
        if (zoneId == "UTC")
        {
            utc = DateTime.SpecifyKind(unspecified, DateTimeKind.Utc);
            return true;
        }

        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
            utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, zone);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            utc = default;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            utc = default;
            return false;
        }
        catch (ArgumentException)
        {
            utc = default;
            return false;
        }
    }

    private static string Code(Trip trip, bool departure)
    {
        var iata = departure ? trip.DepartureIataCode : trip.ArrivalIataCode;
        if (!string.IsNullOrWhiteSpace(iata))
        {
            return iata;
        }

        var city = departure ? trip.DepartureCity : trip.ArrivalCity;
        return string.IsNullOrWhiteSpace(city) ? (departure ? trip.DepartureCountry : trip.ArrivalCountry) : city;
    }

    private static string Describe(Trip trip) => trip.Id == 0
        ? $"{Code(trip, true)} to {Code(trip, false)} departing {trip.DepartureDateTime:yyyy-MM-dd HH:mm}"
        : $"trip {trip.Id} ({Code(trip, true)} to {Code(trip, false)}, departing {trip.DepartureDateTime:yyyy-MM-dd HH:mm})";

    private static int? IdOrNull(Trip trip) => trip.Id == 0 ? null : trip.Id;
}

public static class TripImpact
{
    public static Trip Snapshot(Trip trip) => new()
    {
        Id = trip.Id,
        UserId = trip.UserId,
        DepartureCountry = trip.DepartureCountry,
        DepartureCity = trip.DepartureCity,
        DepartureDateTime = trip.DepartureDateTime,
        DepartureTimezone = trip.DepartureTimezone,
        DepartureIataCode = trip.DepartureIataCode,
        ArrivalCountry = trip.ArrivalCountry,
        ArrivalCity = trip.ArrivalCity,
        ArrivalDateTime = trip.ArrivalDateTime,
        ArrivalTimezone = trip.ArrivalTimezone,
        ArrivalIataCode = trip.ArrivalIataCode
    };

    public static List<DayCountChange> Compare(
        IReadOnlyList<Trip> before,
        IReadOnlyList<Trip> after,
        ResidencyCalculationService residency,
        DateOnly today)
    {
        var left = Count(before, residency, today);
        var right = Count(after, residency, today);
        var keys = new HashSet<CountKey>(left.Keys);
        keys.UnionWith(right.Keys);
        var changes = new List<DayCountChange>();
        foreach (var key in keys)
        {
            left.TryGetValue(key, out var beforeDays);
            right.TryGetValue(key, out var afterDays);
            if (beforeDays == afterDays)
            {
                continue;
            }

            changes.Add(new DayCountChange
            {
                Country = key.Country,
                WindowType = key.WindowType,
                Year = key.Year,
                WindowStart = key.Start,
                WindowEnd = key.End,
                Before = beforeDays,
                After = afterDays,
                Delta = afterDays - beforeDays
            });
        }

        return changes
            .OrderBy(change => change.WindowType, StringComparer.Ordinal)
            .ThenBy(change => change.Year)
            .ThenByDescending(change => Math.Abs(change.Delta))
            .ThenBy(change => change.Country, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string Describe(IReadOnlyList<DayCountChange> changes)
    {
        if (changes.Count == 0)
        {
            return "No country day counts changed.";
        }

        return string.Join(" ", changes.Select(change =>
        {
            var signed = change.Delta > 0 ? $"+{change.Delta}" : change.Delta.ToString();
            return change.WindowType == "calendarYear"
                ? $"{change.Country} {signed} in {change.Year}."
                : $"{change.Country} {signed} in the 365 days ending {change.WindowEnd:yyyy-MM-dd}.";
        }));
    }

    private static Dictionary<CountKey, int> Count(IReadOnlyList<Trip> trips, ResidencyCalculationService residency, DateOnly today)
    {
        var presence = residency.GenerateDailyPresenceLog(trips.ToList());
        var totals = new Dictionary<CountKey, int>();
        Add(totals, presence, residency, RollingWindow.StartOn(today), today, "rolling365", null);
        foreach (var year in Years(trips, presence))
        {
            Add(totals, presence, residency, new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), "calendarYear", year);
        }

        return totals;
    }

    private static void Add(
        Dictionary<CountKey, int> totals,
        List<DailyPresence> presence,
        ResidencyCalculationService residency,
        DateOnly start,
        DateOnly end,
        string windowType,
        int? year)
    {
        foreach (var pair in residency.CalculateResidencyDays(presence, start, end))
        {
            if (pair.Value == 0 || string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            totals[new CountKey(pair.Key, windowType, year, start, end)] = pair.Value;
        }
    }

    private static IEnumerable<int> Years(IReadOnlyList<Trip> trips, IEnumerable<DailyPresence> presence)
    {
        var years = new HashSet<int>();
        foreach (var trip in trips)
        {
            years.Add(trip.DepartureDateTime.Year);
            years.Add(trip.ArrivalDateTime.Year);
        }

        foreach (var day in presence)
        {
            years.Add(day.Date.Year);
        }

        return years;
    }

    private readonly record struct CountKey(string Country, string WindowType, int? Year, DateOnly Start, DateOnly End);
}

public sealed class TripWarning
{
    public int? TripId { get; set; }
    public int? OtherTripId { get; set; }

    [Description("arrivalBeforeDeparture, longHaul, unusualDuration, overlap, or countryGap.")]
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

public sealed class DayCountChange
{
    public string Country { get; set; } = string.Empty;

    [Description("rolling365 or calendarYear.")]
    public string WindowType { get; set; } = string.Empty;

    [Description("Calendar year, when windowType is calendarYear.")]
    public int? Year { get; set; }

    public DateOnly WindowStart { get; set; }
    public DateOnly WindowEnd { get; set; }
    public int Before { get; set; }
    public int After { get; set; }
    public int Delta { get; set; }
}
