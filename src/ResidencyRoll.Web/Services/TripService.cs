using Microsoft.EntityFrameworkCore;
using ResidencyRoll.Web.Data;
using ResidencyRoll.Web.Models;

namespace ResidencyRoll.Web.Services;

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

    /// <summary>
    /// Calculates total days spent per country across all recorded trips
    /// </summary>
    public async Task<Dictionary<string, int>> GetTotalDaysPerCountryAsync()
    {
        var trips = await _context.Trips.ToListAsync();
        var totals = new Dictionary<string, int>();

        foreach (var trip in trips)
        {
            // Midnights present: arrival counts, departure does not.
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

    public async Task DeleteTripAsync(int id)
    {
        var trip = await _context.Trips.FindAsync(id);
        if (trip != null)
        {
            _context.Trips.Remove(trip);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Calculates days spent per country within the last 365 days from today
    /// </summary>
    public async Task<Dictionary<string, int>> GetDaysPerCountryInLast365DaysAsync()
    {
        var today = DateTime.Today;
        var windowStart = today.AddDays(-365);

        var trips = await _context.Trips.ToListAsync();
        var daysPerCountry = new Dictionary<string, int>();

        foreach (var trip in trips)
        {
            // Calculate the overlap between the trip and the 365-day window using end-exclusive counting.
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

    /// <summary>
    /// Gets the total days away from home in the last 365 days
    /// </summary>
    public async Task<int> GetTotalDaysAwayInLast365DaysAsync()
    {
        var daysPerCountry = await GetDaysPerCountryInLast365DaysAsync();
        return daysPerCountry.Values.Sum();
    }

    /// <summary>
    /// Gets the days at home in the last 365 days
    /// </summary>
    public async Task<int> GetDaysAtHomeInLast365DaysAsync()
    {
        var totalDaysAway = await GetTotalDaysAwayInLast365DaysAsync();
        return 365 - totalDaysAway;
    }

    /// <summary>
    /// Gets trips ordered chronologically for timeline display
    /// </summary>
    public async Task<List<Trip>> GetTripsForTimelineAsync()
    {
        return await _context.Trips.OrderBy(t => t.StartDate).ToListAsync();
    }

    /// <summary>
    /// Forecasts days per country in a 365-day window ending at the forecast trip's end date.
    /// Recalculates the rolling 365-day window based on the end of the hypothetical trip.
    /// </summary>
    public async Task<(Dictionary<string, int> Current, Dictionary<string, int> Forecast)> ForecastDaysWithTripAsync(string countryName, DateTime tripStart, DateTime tripEnd)
    {
        var today = DateTime.Today;
        var currentWindowStart = today.AddDays(-365);

        // Forecast window: 365 days ending at the forecast trip's end date
        var forecastWindowEnd = tripEnd;
        var forecastWindowStart = tripEnd.AddDays(-365);

        var trips = await _context.Trips.ToListAsync();
        var currentDaysPerCountry = new Dictionary<string, int>();
        var forecastDaysPerCountry = new Dictionary<string, int>();

        // Calculate current (last 365 days from today)
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

        // Calculate forecast (365 days ending at forecast trip end date, including all overlapping trips + hypothetical trip)
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

        // Add the hypothetical trip to forecast
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
}
