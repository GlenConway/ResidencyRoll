using Microsoft.EntityFrameworkCore;
using ResidencyRoll.Api.Data;
using ResidencyRoll.Api.Models;

namespace ResidencyRoll.Api.Services;

public class TripService
{
    private readonly ApplicationDbContext _context;

    public TripService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Trip>> GetAllTripsAsync()
    {
        return await _context.Trips.OrderBy(t => t.StartDate).ToListAsync();
    }

    public async Task<Trip?> GetTripByIdAsync(int id)
    {
        return await _context.Trips.FindAsync(id);
    }

    public async Task<Trip> CreateTripAsync(Trip trip)
    {
        _context.Trips.Add(trip);
        await _context.SaveChangesAsync();
        return trip;
    }

    public async Task<Trip> UpdateTripAsync(Trip trip)
    {
        _context.Trips.Update(trip);
        await _context.SaveChangesAsync();
        return trip;
    }

    public async Task DeleteTripAsync(int id)
    {
        var trip = await _context.Trips.FindAsync(id);
        if (trip != null)
        {
            _context.Trips.Remove(trip);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Dictionary<string, int>> GetTotalDaysPerCountryAsync()
    {
        var trips = await _context.Trips.ToListAsync();
        var totals = new Dictionary<string, int>();

        foreach (var trip in trips)
        {
            var days = Math.Max(0, (trip.EndDate - trip.StartDate).Days);

            if (totals.ContainsKey(trip.CountryName))
            {
                totals[trip.CountryName] += days;
            }
            else
            {
                totals[trip.CountryName] = days;
            }
        }

        return totals;
    }

    public async Task<Dictionary<string, int>> GetDaysPerCountryInLast365DaysAsync()
    {
        var today = DateTime.Today;
        var windowStart = today.AddDays(-365);

        var trips = await _context.Trips.ToListAsync();
        var daysPerCountry = new Dictionary<string, int>();

        foreach (var trip in trips)
        {
            var overlapStart = trip.StartDate > windowStart ? trip.StartDate : windowStart;
            var overlapEndExclusive = trip.EndDate < today ? trip.EndDate : today;

            if (overlapStart < overlapEndExclusive)
            {
                var days = (overlapEndExclusive - overlapStart).Days;

                if (daysPerCountry.ContainsKey(trip.CountryName))
                {
                    daysPerCountry[trip.CountryName] += days;
                }
                else
                {
                    daysPerCountry[trip.CountryName] = days;
                }
            }
        }

        return daysPerCountry;
    }

    public async Task<int> GetTotalDaysAwayInLast365DaysAsync()
    {
        var daysPerCountry = await GetDaysPerCountryInLast365DaysAsync();
        return daysPerCountry.Values.Sum();
    }

    public async Task<int> GetDaysAtHomeInLast365DaysAsync()
    {
        var totalDaysAway = await GetTotalDaysAwayInLast365DaysAsync();
        return 365 - totalDaysAway;
    }

    public async Task<List<Trip>> GetTripsForTimelineAsync()
    {
        return await _context.Trips.OrderBy(t => t.StartDate).ToListAsync();
    }

    public async Task<(Dictionary<string, int> Current, Dictionary<string, int> Forecast)> ForecastDaysWithTripAsync(string countryName, DateTime tripStart, DateTime tripEnd)
    {
        var today = DateTime.Today;
        var currentWindowStart = today.AddDays(-365);

        var forecastWindowEnd = tripEnd;
        var forecastWindowStart = tripEnd.AddDays(-365);

        var trips = await _context.Trips.ToListAsync();
        var currentDaysPerCountry = new Dictionary<string, int>();
        var forecastDaysPerCountry = new Dictionary<string, int>();

        foreach (var trip in trips)
        {
            var overlapStart = trip.StartDate > currentWindowStart ? trip.StartDate : currentWindowStart;
            var overlapEndExclusive = trip.EndDate < today ? trip.EndDate : today;

            if (overlapStart < overlapEndExclusive)
            {
                var days = (overlapEndExclusive - overlapStart).Days;
                if (currentDaysPerCountry.ContainsKey(trip.CountryName))
                {
                    currentDaysPerCountry[trip.CountryName] += days;
                }
                else
                {
                    currentDaysPerCountry[trip.CountryName] = days;
                }
            }
        }

        foreach (var trip in trips)
        {
            var overlapStart = trip.StartDate > forecastWindowStart ? trip.StartDate : forecastWindowStart;
            var overlapEndExclusive = trip.EndDate < forecastWindowEnd ? trip.EndDate : forecastWindowEnd;

            if (overlapStart < overlapEndExclusive)
            {
                var days = (overlapEndExclusive - overlapStart).Days;
                if (forecastDaysPerCountry.ContainsKey(trip.CountryName))
                {
                    forecastDaysPerCountry[trip.CountryName] += days;
                }
                else
                {
                    forecastDaysPerCountry[trip.CountryName] = days;
                }
            }
        }

        var hypotheticalStart = tripStart > forecastWindowStart ? tripStart : forecastWindowStart;
        var hypotheticalEndExclusive = tripEnd < forecastWindowEnd ? tripEnd : forecastWindowEnd;

        if (hypotheticalStart < hypotheticalEndExclusive)
        {
            var days = (hypotheticalEndExclusive - hypotheticalStart).Days;
            if (forecastDaysPerCountry.ContainsKey(countryName))
            {
                forecastDaysPerCountry[countryName] += days;
            }
            else
            {
                forecastDaysPerCountry[countryName] = days;
            }
        }

        return (currentDaysPerCountry, forecastDaysPerCountry);
    }

    public async Task<(DateTime MaxEndDate, int DaysAtLimit)> CalculateMaxTripEndDateAsync(string countryName, DateTime tripStart, int dayLimit = 183)
    {
        var trips = await _context.Trips.ToListAsync();

        DateTime currentEnd = tripStart;
        int daysUsed = 0;

        DateTime minDate = tripStart;
        DateTime maxDate = tripStart.AddDays(365);

        while (minDate < maxDate)
        {
            DateTime midDate = minDate.AddDays((maxDate - minDate).Days / 2);
            var (_, forecastDays) = await ForecastDaysWithTripAsync(countryName, tripStart, midDate);

            int totalDays = forecastDays.ContainsKey(countryName) ? forecastDays[countryName] : 0;

            if (totalDays <= dayLimit)
            {
                currentEnd = midDate;
                daysUsed = totalDays;
                minDate = midDate.AddDays(1);
            }
            else
            {
                maxDate = midDate;
            }
        }

        return (currentEnd, daysUsed);
    }

    public async Task<List<(int DurationDays, DateTime EndDate, int TotalDaysInCountry, bool ExceedsLimit)>> CalculateStandardDurationForecastsAsync(string countryName, DateTime tripStart, int dayLimit = 183, int[]? durations = null)
    {
        var results = new List<(int, DateTime, int, bool)>();
        int[] requestedDurations = durations is { Length: > 0 } ? durations : new[] { 7, 14, 21 };

        foreach (var duration in requestedDurations)
        {
            var endDate = tripStart.AddDays(duration);
            var (_, forecastDays) = await ForecastDaysWithTripAsync(countryName, tripStart, endDate);

            int totalDays = forecastDays.ContainsKey(countryName) ? forecastDays[countryName] : 0;
            bool exceedsLimit = totalDays > dayLimit;

            results.Add((duration, endDate, totalDays, exceedsLimit));
        }

        return results;
    }
}
