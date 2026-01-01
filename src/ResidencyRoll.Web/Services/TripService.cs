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
}
