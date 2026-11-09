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
        var daysAtBest = DaysInCountry(existing, request, request.TripStart);

        if (daysAtBest > limit)
        {
            return new MaxTripEndDateResponseDto
            {
                MaxEndDate = request.TripStart,
                DaysAtLimit = daysAtBest
            };
        }

        for (var extraDays = 1; extraDays <= limit + 366; extraDays++)
        {
            var candidate = request.TripStart.AddDays(extraDays);
            var days = DaysInCountry(existing, request, candidate);
            if (days > limit)
            {
                break;
            }

            bestEnd = candidate;
            daysAtBest = days;
        }

        return new MaxTripEndDateResponseDto
        {
            MaxEndDate = bestEnd,
            DaysAtLimit = daysAtBest
        };
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

    private int DaysInCountry(IReadOnlyList<Trip> existing, MaxTripEndDateRequestDto request, DateTime end)
    {
        var hypothetical = Stay(request, end);
        var (_, forecast) = _trips.ForecastDays(existing, hypothetical);
        return forecast
            .Where(pair => string.Equals(pair.Key, request.ArrivalCountry, StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Value)
            .FirstOrDefault();
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
