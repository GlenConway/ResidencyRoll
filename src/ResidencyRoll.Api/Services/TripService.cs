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

    public async Task<List<Trip>> GetAllTripsAsync(string userId)
    {
        return await _context.Trips
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.StartDate)
            .ToListAsync();
    }

    public async Task<Trip?> GetTripByIdAsync(int id, string userId)
    {
        return await _context.Trips
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
    }

    public async Task<Trip> CreateTripAsync(Trip trip)
    {
        _context.Trips.Add(trip);
        await _context.SaveChangesAsync();
        return trip;
    }

    public async Task<Trip> UpdateTripAsync(Trip trip, string userId)
    {
        var existing = await GetTripByIdAsync(trip.Id, userId);
        if (existing == null)
        {
            throw new UnauthorizedAccessException("Trip not found or you don't have permission to update it.");
        }
        _context.Trips.Update(trip);
        await _context.SaveChangesAsync();
        return trip;
    }

    public async Task DeleteTripAsync(int id, string userId)
    {
        var trip = await GetTripByIdAsync(id, userId);
        if (trip != null)
        {
            _context.Trips.Remove(trip);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Dictionary<string, int>> GetTotalDaysPerCountryAsync(string userId)
    {
        var trips = await _context.Trips
            .Where(t => t.UserId == userId)
            .ToListAsync();
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

    public async Task<Dictionary<string, int>> GetDaysPerCountryInLast365DaysAsync(string userId)
    {
        var today = DateTime.Today;
        var windowStart = today.AddDays(-365);

        var trips = await _context.Trips
            .Where(t => t.UserId == userId)
            .ToListAsync();
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

    public async Task<int> GetTotalDaysAwayInLast365DaysAsync(string userId)
    {
        var daysPerCountry = await GetDaysPerCountryInLast365DaysAsync(userId);
        return daysPerCountry.Values.Sum();
    }

    public async Task<int> GetDaysAtHomeInLast365DaysAsync(string userId)
    {
        var totalDaysAway = await GetTotalDaysAwayInLast365DaysAsync(userId);
        return 365 - totalDaysAway;
    }

    public async Task<List<Trip>> GetTripsForTimelineAsync(string userId)
    {
        return await _context.Trips
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.StartDate)
            .ToListAsync();
    }

    public async Task<(Dictionary<string, int> Current, Dictionary<string, int> Forecast)> ForecastDaysWithTripAsync(string userId, string countryName, DateTime tripStart, DateTime tripEnd)
    {
        var today = DateTime.Today;
        var currentWindowStart = today.AddDays(-365);

        var forecastWindowEnd = tripEnd;
        var forecastWindowStart = tripEnd.AddDays(-365);

        var trips = await _context.Trips
            .Where(t => t.UserId == userId)
            .ToListAsync();
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

    public async Task<(DateTime MaxEndDate, int DaysAtLimit)> CalculateMaxTripEndDateAsync(string userId, string countryName, DateTime tripStart, int dayLimit = 183)
    {
        var trips = await _context.Trips
            .Where(t => t.UserId == userId)
            .ToListAsync();

        DateTime currentEnd = tripStart;
        int daysUsed = 0;

        DateTime minDate = tripStart;
        DateTime maxDate = tripStart.AddDays(365);

        while (minDate < maxDate)
        {
            DateTime midDate = minDate.AddDays((maxDate - minDate).Days / 2);
            var (_, forecastDays) = await ForecastDaysWithTripAsync(userId, countryName, tripStart, midDate);

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

    public async Task<List<(int DurationDays, DateTime EndDate, int TotalDaysInCountry, bool ExceedsLimit)>> CalculateStandardDurationForecastsAsync(string userId, string countryName, DateTime tripStart, int dayLimit = 183, int[]? durations = null)
    {
        var results = new List<(int, DateTime, int, bool)>();
        int[] requestedDurations = durations is { Length: > 0 } ? durations : new[] { 7, 14, 21 };

        foreach (var duration in requestedDurations)
        {
            var endDate = tripStart.AddDays(duration);
            var (_, forecastDays) = await ForecastDaysWithTripAsync(userId, countryName, tripStart, endDate);

            int totalDays = forecastDays.ContainsKey(countryName) ? forecastDays[countryName] : 0;
            bool exceedsLimit = totalDays > dayLimit;

            results.Add((duration, endDate, totalDays, exceedsLimit));
        }

        return results;
    }
}
