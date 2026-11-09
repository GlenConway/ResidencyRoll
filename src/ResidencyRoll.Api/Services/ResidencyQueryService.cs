using ResidencyRoll.Api.Models;
using ResidencyRoll.Shared.Trips;

namespace ResidencyRoll.Api.Services;

/// <summary>
/// Residency reads shared by the REST controller and the MCP tools.
/// </summary>
public class ResidencyQueryService
{
    private static readonly int[] DefaultDurations = [7, 14, 30, 60, 90, 180];

    private readonly TripService _trips;
    private readonly ResidencyCalculationService _residency;

    public ResidencyQueryService(TripService trips, ResidencyCalculationService residency)
    {
        _trips = trips;
        _residency = residency;
    }

    public async Task<ForecastResponseDto> ForecastAsync(string userId, IReadOnlyList<Trip> hypotheticalTrips)
    {
        Dictionary<string, int> current;
        Dictionary<string, int> forecast;

        if (hypotheticalTrips.Count > 0)
        {
            (current, forecast) = await _trips.ForecastDaysWithTripsAsync(userId, hypotheticalTrips.ToList());
        }
        else
        {
            current = new Dictionary<string, int>();
            forecast = new Dictionary<string, int>();
        }

        return new ForecastResponseDto
        {
            Current = current.Select(kvp => new CountryDaysDto { CountryName = kvp.Key, Days = kvp.Value }).ToList(),
            Forecast = forecast.Select(kvp => new CountryDaysDto { CountryName = kvp.Key, Days = kvp.Value }).ToList()
        };
    }

    public async Task<ForecastBreakdown> ForecastBreakdownAsync(string userId, IReadOnlyList<Trip> hypotheticalTrips)
    {
        var basic = await ForecastAsync(userId, hypotheticalTrips);
        var current = basic.Current.ToDictionary(row => row.CountryName, row => row.Days);
        var forecast = basic.Forecast.ToDictionary(row => row.CountryName, row => row.Days);
        if (hypotheticalTrips.Count == 0)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            return new ForecastBreakdown(RollingWindow.StartOn(today), today, current, forecast, new Dictionary<string, PeakCount>(StringComparer.OrdinalIgnoreCase));
        }

        var window = _trips.GetForecastWindow(hypotheticalTrips);
        var existing = await _trips.GetAllTripsAsync(userId);
        var combined = new List<Trip>(existing);
        combined.AddRange(hypotheticalTrips);
        var presence = _residency.GenerateDailyPresenceLog(combined);
        var earliest = hypotheticalTrips.Min(EarliestLocalDate);
        var todayDate = DateOnly.FromDateTime(DateTime.Today);
        var periodStart = earliest < todayDate ? earliest : todayDate;
        if (periodStart > window.End)
        {
            periodStart = window.End;
        }

        var peaks = _residency.PeakRollingCounts(presence, periodStart, window.End);
        return new ForecastBreakdown(window.Start, window.End, current, forecast, peaks);
    }

    public async Task<List<DailyPresenceDto>> GetDailyPresenceAsync(string userId, DateOnly? startDate, DateOnly? endDate)
    {
        var trips = await _trips.GetAllTripsAsync(userId);
        var dailyPresence = _residency.GenerateDailyPresenceLog(trips);

        var filteredPresence = dailyPresence.AsEnumerable();
        if (startDate.HasValue)
        {
            filteredPresence = filteredPresence.Where(dp => dp.Date >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            filteredPresence = filteredPresence.Where(dp => dp.Date <= endDate.Value);
        }

        return filteredPresence
            .OrderBy(dp => dp.Date)
            .Select(dp => new DailyPresenceDto
            {
                Date = dp.Date,
                LocationAtMidnight = dp.LocationAtMidnight,
                LocationsDuringDay = dp.LocationsDuringDay.ToList(),
                IsInTransitAtMidnight = dp.IsInTransitAtMidnight
            })
            .ToList();
    }

    public async Task<List<ResidencySummaryDto>> GetResidencySummaryAsync(string userId, DateOnly? startDate, DateOnly? endDate)
    {
        var trips = await _trips.GetAllTripsAsync(userId);
        var dailyPresence = _residency.GenerateDailyPresenceLog(trips);
        var residencyDays = _residency.CalculateResidencyDays(dailyPresence, startDate, endDate);

        var summary = new List<ResidencySummaryDto>();
        foreach (var kvp in residencyDays.OrderByDescending(pair => pair.Value))
        {
            var rule = _residency.GetCountryRule(kvp.Key);
            var thresholdDays = rule.ResidencyThresholdDays ?? 183;
            var daysUntilThreshold = thresholdDays - kvp.Value;

            summary.Add(new ResidencySummaryDto
            {
                CountryName = kvp.Key,
                TotalDays = kvp.Value,
                RuleType = rule.RuleType == ResidencyRuleType.MidnightRule ? "Midnight" : "PartialDay",
                ThresholdDays = thresholdDays,
                IsApproachingThreshold = daysUntilThreshold <= 30 && daysUntilThreshold > 0,
                DaysUntilThreshold = Math.Max(0, daysUntilThreshold)
            });
        }

        return summary;
    }

    public async Task<MaxTripEndDateResponseDto> CalculateMaxEndDateAsync(string userId, MaxTripEndDateRequestDto request)
    {
        var limit = request.DayLimit <= 0 ? 183 : request.DayLimit;
        var existing = await _trips.GetAllTripsAsync(userId);
        var bestEnd = request.TripStart;
        var best = MeasureStay(existing, request, request.TripStart);

        if (best.PeakDays > limit)
        {
            return MaxEndResponse(request, request.TripStart, best);
        }

        for (var extraDays = 1; extraDays <= limit + 366; extraDays++)
        {
            var candidate = request.TripStart.AddDays(extraDays);
            var measured = MeasureStay(existing, request, candidate);
            if (measured.PeakDays > limit)
            {
                break;
            }

            bestEnd = candidate;
            best = measured;
        }

        return MaxEndResponse(request, bestEnd, best);
    }

    private MaxTripEndDateResponseDto MaxEndResponse(MaxTripEndDateRequestDto request, DateTime end, StayMeasure measure)
    {
        var rule = _residency.GetCountryRule(request.ArrivalCountry);
        return new MaxTripEndDateResponseDto
        {
            MaxEndDate = end,
            DaysAtLimit = measure.PeakDays,
            WindowStart = measure.WindowStart,
            WindowEnd = measure.WindowEnd,
            RuleType = rule.RuleType == ResidencyRuleType.MidnightRule ? "Midnight" : "PartialDay"
        };
    }

    public StayMeasure MeasureStay(IReadOnlyList<Trip> existing, MaxTripEndDateRequestDto request, DateTime end)
    {
        var hypothetical = Stay(request, end);
        var combined = new List<Trip>(existing);
        combined.AddRange(hypothetical);
        var presence = _residency.GenerateDailyPresenceLog(combined);
        var window = _trips.GetForecastWindow(hypothetical);
        var endDays = CountCountry(_residency.CalculateResidencyDays(presence, window.Start, window.End), request.ArrivalCountry);
        var periodStart = DateOnly.FromDateTime(request.TripStart);
        if (periodStart > window.End)
        {
            periodStart = window.End;
        }

        var peaks = _residency.PeakRollingCounts(presence, periodStart, window.End);
        var peakDays = endDays;
        if (TryGetPeak(peaks, request.ArrivalCountry, out var peak) && peak.Days > peakDays)
        {
            peakDays = peak.Days;
        }

        return new StayMeasure(peakDays, endDays, window.Start, window.End);
    }

    public async Task<LocationOnDate> GetLocationOnAsync(string userId, DateOnly day)
    {
        var trips = await _trips.GetAllTripsAsync(userId);
        return LocationFrom(trips, day);
    }

    public async Task<CurrentLocation> GetCurrentLocationAsync(string userId)
    {
        var trips = await _trips.GetAllTripsAsync(userId);
        var now = DateTime.UtcNow;
        Trip? lastArrival = null;
        var lastArrivalUtc = DateTime.MinValue;
        Trip? inTransit = null;
        foreach (var trip in trips)
        {
            var arrivalUtc = ToUtc(trip.ArrivalDateTime, trip.ArrivalTimezone);
            var departureUtc = ToUtc(trip.DepartureDateTime, trip.DepartureTimezone);
            if (arrivalUtc <= now && arrivalUtc >= lastArrivalUtc)
            {
                lastArrival = trip;
                lastArrivalUtc = arrivalUtc;
            }

            if (departureUtc <= now && arrivalUtc > now)
            {
                inTransit = trip;
            }
        }

        if (lastArrival is null)
        {
            return new CurrentLocation(string.Empty, "noData", null, null, null, null, null, null, null);
        }

        var today = LocationFrom(trips, DateOnly.FromDateTime(DateTime.Today));
        var status = inTransit is not null ? "inTransit" : today.Status;
        var country = inTransit is not null
            ? "IN_TRANSIT"
            : string.IsNullOrEmpty(today.LocationAtMidnight) ? lastArrival.ArrivalCountry : today.LocationAtMidnight;
        return new CurrentLocation(
            country,
            status,
            lastArrival.ArrivalIataCode,
            lastArrival.ArrivalCity,
            lastArrival.ArrivalCountry,
            DateOnly.FromDateTime(lastArrival.ArrivalDateTime),
            inTransit?.ArrivalIataCode,
            inTransit?.ArrivalCity,
            inTransit?.ArrivalCountry);
    }

    public async Task<List<StandardDurationForecastItemDto>> GetStandardDurationForecastsAsync(
        string userId,
        StandardDurationForecastRequestDto request)
    {
        var limit = request.DayLimit <= 0 ? 183 : request.DayLimit;
        var durations = request.Durations is { Length: > 0 } ? request.Durations : DefaultDurations;
        var existing = await _trips.GetAllTripsAsync(userId);
        var stay = new MaxTripEndDateRequestDto
        {
            DepartureCountry = request.DepartureCountry,
            DepartureCity = request.DepartureCity,
            DepartureTimezone = request.DepartureTimezone,
            DepartureIataCode = request.DepartureIataCode,
            ArrivalCountry = request.ArrivalCountry,
            ArrivalCity = request.ArrivalCity,
            ArrivalTimezone = request.ArrivalTimezone,
            ArrivalIataCode = request.ArrivalIataCode,
            TripStart = request.TripStart,
            DayLimit = limit
        };

        var results = new List<StandardDurationForecastItemDto>();
        foreach (var duration in durations)
        {
            var end = request.TripStart.AddDays(duration);
            var days = DaysInCountry(existing, stay, end);
            results.Add(new StandardDurationForecastItemDto
            {
                DurationDays = duration,
                EndDate = end,
                TotalDaysInCountry = days,
                ExceedsLimit = days > limit
            });
        }

        return results;
    }

    private int DaysInCountry(IReadOnlyList<Trip> existing, MaxTripEndDateRequestDto request, DateTime end) =>
        MeasureStay(existing, request, end).EndOfWindowDays;

    private LocationOnDate LocationFrom(IReadOnlyList<Trip> trips, DateOnly day)
    {
        var presence = _residency.GenerateDailyPresenceLog(trips.ToList());
        var match = presence.FirstOrDefault(item => item.Date == day && !string.IsNullOrWhiteSpace(item.LocationAtMidnight));
        if (match is not null)
        {
            return new LocationOnDate(
                day,
                match.LocationAtMidnight,
                match.LocationsDuringDay.ToList(),
                match.IsInTransitAtMidnight,
                IsTravelDay(trips, day) ? "recorded" : "gapFilled");
        }

        Trip? lastArrival = null;
        var lastArrivalUtc = DateTime.MinValue;
        foreach (var trip in trips)
        {
            if (DateOnly.FromDateTime(trip.ArrivalDateTime) > day || string.IsNullOrWhiteSpace(trip.ArrivalCountry))
            {
                continue;
            }

            var arrivalUtc = ToUtc(trip.ArrivalDateTime, trip.ArrivalTimezone);
            if (lastArrival is null || arrivalUtc >= lastArrivalUtc)
            {
                lastArrival = trip;
                lastArrivalUtc = arrivalUtc;
            }
        }

        if (lastArrival is not null)
        {
            return new LocationOnDate(day, lastArrival.ArrivalCountry, [lastArrival.ArrivalCountry], false, "projected");
        }

        return new LocationOnDate(day, string.Empty, [], false, "noData");
    }

    private static bool IsTravelDay(IEnumerable<Trip> trips, DateOnly day)
    {
        foreach (var trip in trips)
        {
            var departure = DateOnly.FromDateTime(trip.DepartureDateTime);
            var arrival = DateOnly.FromDateTime(trip.ArrivalDateTime);
            var start = departure < arrival ? departure : arrival;
            var end = departure < arrival ? arrival : departure;
            if (day >= start && day <= end)
            {
                return true;
            }
        }

        return false;
    }

    private static DateOnly EarliestLocalDate(Trip trip)
    {
        var departure = DateOnly.FromDateTime(trip.DepartureDateTime);
        var arrival = DateOnly.FromDateTime(trip.ArrivalDateTime);
        return departure < arrival ? departure : arrival;
    }

    private static int CountCountry(Dictionary<string, int> days, string country) =>
        days.Where(pair => pair.Key.Equals(country, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Value).FirstOrDefault();

    private static bool TryGetPeak(Dictionary<string, PeakCount> peaks, string country, out PeakCount peak)
    {
        if (peaks.TryGetValue(country, out peak))
        {
            return true;
        }

        foreach (var pair in peaks)
        {
            if (pair.Key.Equals(country, StringComparison.OrdinalIgnoreCase))
            {
                peak = pair.Value;
                return true;
            }
        }

        peak = default;
        return false;
    }

    private static DateTime ToUtc(DateTime local, string? timezoneId)
    {
        if (local.Kind == DateTimeKind.Utc)
        {
            return local;
        }

        var unspecified = local.Kind == DateTimeKind.Unspecified ? local : DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var zoneId = string.IsNullOrWhiteSpace(timezoneId) ? "UTC" : timezoneId;
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
            return TimeZoneInfo.ConvertTimeToUtc(unspecified, zone);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateTime.SpecifyKind(unspecified, DateTimeKind.Utc);
        }
        catch (InvalidTimeZoneException)
        {
            return DateTime.SpecifyKind(unspecified, DateTimeKind.Utc);
        }
    }

    private static List<Trip> Stay(MaxTripEndDateRequestDto request, DateTime end)
    {
        var start = DateTime.SpecifyKind(request.TripStart, DateTimeKind.Unspecified);
        var departureAt = DateTime.SpecifyKind(end, DateTimeKind.Unspecified);
        var outboundDeparture = start.AddHours(-6);
        var returnArrival = departureAt.AddHours(6);
        var departureZone = string.IsNullOrWhiteSpace(request.DepartureTimezone) ? "UTC" : request.DepartureTimezone;
        var arrivalZone = string.IsNullOrWhiteSpace(request.ArrivalTimezone) ? "UTC" : request.ArrivalTimezone;

        return
        [
            new Trip
            {
                DepartureCountry = request.DepartureCountry,
                DepartureCity = request.DepartureCity,
                DepartureDateTime = outboundDeparture,
                DepartureTimezone = departureZone,
                DepartureIataCode = request.DepartureIataCode,
                ArrivalCountry = request.ArrivalCountry,
                ArrivalCity = request.ArrivalCity,
                ArrivalDateTime = start,
                ArrivalTimezone = arrivalZone,
                ArrivalIataCode = request.ArrivalIataCode
            },
            new Trip
            {
                DepartureCountry = request.ArrivalCountry,
                DepartureCity = request.ArrivalCity,
                DepartureDateTime = departureAt,
                DepartureTimezone = arrivalZone,
                DepartureIataCode = request.ArrivalIataCode,
                ArrivalCountry = request.DepartureCountry,
                ArrivalCity = request.DepartureCity,
                ArrivalDateTime = returnArrival,
                ArrivalTimezone = departureZone,
                ArrivalIataCode = request.DepartureIataCode
            }
        ];
    }
}

public sealed record ForecastBreakdown(
    DateOnly WindowStart,
    DateOnly WindowEnd,
    Dictionary<string, int> Current,
    Dictionary<string, int> Forecast,
    Dictionary<string, PeakCount> Peaks);

public readonly record struct StayMeasure(int PeakDays, int EndOfWindowDays, DateOnly WindowStart, DateOnly WindowEnd);

public sealed record LocationOnDate(
    DateOnly Date,
    string LocationAtMidnight,
    IReadOnlyList<string> LocationsDuringDay,
    bool IsInTransitAtMidnight,
    string Status);

public sealed record CurrentLocation(
    string Country,
    string Status,
    string? LastArrivalIataCode,
    string? LastArrivalCity,
    string? LastArrivalCountry,
    DateOnly? LastArrivalDate,
    string? InTransitArrivalIataCode,
    string? InTransitArrivalCity,
    string? InTransitArrivalCountry);
