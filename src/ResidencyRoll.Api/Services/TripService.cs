using Microsoft.EntityFrameworkCore;
using ResidencyRoll.Api.Data;
using ResidencyRoll.Api.Models;

namespace ResidencyRoll.Api.Services;

public class TripService
{
    private readonly ApplicationDbContext _context;
    private readonly ResidencyCalculationService _residencyService;

    public TripService(ApplicationDbContext context, ResidencyCalculationService residencyService)
    {
        _context = context;
        _residencyService = residencyService;
    }

    public async Task<List<Trip>> GetAllTripsAsync(string userId)
    {
        return await _context.Trips
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.DepartureDateTime)
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

        var presenceLog = _residencyService.GenerateDailyPresenceLog(trips);
        return _residencyService.CalculateResidencyDays(presenceLog);
    }

    public async Task<Dictionary<string, int>> GetDaysPerCountryInLast365DaysAsync(string userId)
    {
        var today = DateTime.Today;
        var windowStart = today.AddDays(-365);

        var trips = await _context.Trips
            .Where(t => t.UserId == userId)
            .ToListAsync();

        return CalculateDaysPerCountryByDateRange(trips, windowStart, today);
    }

    public async Task<(Dictionary<string, int> Current, Dictionary<string, int> Forecast)> ForecastDaysWithTripsAsync(string userId, List<Trip> hypotheticalTrips)
    {
        if (hypotheticalTrips == null || hypotheticalTrips.Count == 0)
        {
            return (new Dictionary<string, int>(), new Dictionary<string, int>());
        }
        
        var today = DateTime.Today;
        var currentWindowStart = today.AddDays(-365);

        // Use the latest date (either arrival or departure) as the end of the forecast window
        var latestArrival = hypotheticalTrips.Max(t => t.ArrivalDateTime);
        var latestDeparture = hypotheticalTrips.Max(t => t.DepartureDateTime);
        var forecastWindowEnd = latestArrival > latestDeparture ? latestArrival : latestDeparture;
        var forecastWindowStart = forecastWindowEnd.AddDays(-365);

        var trips = await _context.Trips
            .Where(t => t.UserId == userId)
            .ToListAsync();

        // Calculate current window (last 365 days from today)
        var currentDaysPerCountry = CalculateDaysPerCountryByDateRange(trips, currentWindowStart, today);

        // For forecast, include all hypothetical trips
        var tripsWithHypothetical = new List<Trip>(trips);
        tripsWithHypothetical.AddRange(hypotheticalTrips);

        var forecastDaysPerCountry = CalculateDaysPerCountryByDateRange(tripsWithHypothetical, forecastWindowStart, forecastWindowEnd);

        return (currentDaysPerCountry, forecastDaysPerCountry);
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
            .OrderBy(t => t.DepartureDateTime)
            .ToListAsync();
    }

    private Dictionary<string, int> CalculateDaysPerCountryByDateRange(
        IEnumerable<Trip> trips,
        DateTime windowStart,
        DateTime windowEnd)
    {
        // All trip leg calculations use ResidencyCalculationService with full timezone information
        var tripsList = trips.ToList();
        var presenceLog = _residencyService.GenerateDailyPresenceLog(tripsList);
        var windowStartDate = DateOnly.FromDateTime(windowStart.Date);
        var windowEndDate = DateOnly.FromDateTime(windowEnd.Date);
        return _residencyService.CalculateResidencyDays(presenceLog, windowStartDate, windowEndDate);
    }
}
