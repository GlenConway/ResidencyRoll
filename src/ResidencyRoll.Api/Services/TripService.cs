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

    /// <summary>
    /// Saves every trip or none of them. A failure rolls the batch back.
    /// </summary>
    public async Task<List<Trip>> CreateTripsAsync(IReadOnlyList<Trip> trips)
    {
        if (trips.Count == 0)
        {
            return [];
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.Trips.AddRange(trips);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return trips.ToList();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
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
        var windowStart = RollingWindow.StartOn(today);

        var trips = await _context.Trips
            .Where(t => t.UserId == userId)
            .ToListAsync();

        return CalculateDaysPerCountryByDateRange(trips, windowStart, today);
    }

    public async Task<(Dictionary<string, int> Current, Dictionary<string, int> Forecast)> ForecastDaysWithTripsAsync(
        string userId,
        List<Trip> hypotheticalTrips,
        DateOnly? through = null)
    {
        var trips = await _context.Trips
            .Where(t => t.UserId == userId)
            .ToListAsync();
        return ForecastDays(trips, hypotheticalTrips, through);
    }

    public (Dictionary<string, int> Current, Dictionary<string, int> Forecast) ForecastDays(
        IReadOnlyList<Trip> existingTrips,
        IReadOnlyList<Trip> hypotheticalTrips,
        DateOnly? through = null)
    {
        if (hypotheticalTrips == null || hypotheticalTrips.Count == 0)
        {
            return (new Dictionary<string, int>(), new Dictionary<string, int>());
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var window = ScenarioWindow(hypotheticalTrips, through);

        var currentDaysPerCountry = CountByCountryWindow(existingTrips, today);

        var tripsWithHypothetical = new List<Trip>(existingTrips);
        tripsWithHypothetical.AddRange(hypotheticalTrips);

        var forecastDaysPerCountry = CountByCountryWindow(tripsWithHypothetical, window.End);

        return (currentDaysPerCountry, forecastDaysPerCountry);
    }

    public ForecastWindow ScenarioWindow(IReadOnlyList<Trip> hypotheticalTrips, DateOnly? through)
    {
        var window = GetForecastWindow(hypotheticalTrips);
        if (through is { } end && end > window.End)
        {
            return new ForecastWindow(RollingWindow.StartOn(end), end);
        }

        return window;
    }

    private Dictionary<string, int> CountByCountryWindow(IEnumerable<Trip> trips, DateOnly referenceEnd)
    {
        var presence = _residencyService.GenerateDailyPresenceLog(trips.ToList());
        var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var country in CountriesIn(presence))
        {
            var period = CountingWindow.For(_residencyService.GetCountryRule(country), referenceEnd);
            var counted = _residencyService.CalculateResidencyDays(presence, period.Start, period.End);
            if (counted.TryGetValue(country, out var days) && days > 0)
            {
                totals[country] = days;
            }
        }

        return totals;
    }

    private static IEnumerable<string> CountriesIn(IEnumerable<DailyPresence> presence)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var day in presence)
        {
            if (!string.IsNullOrWhiteSpace(day.LocationAtMidnight) && day.LocationAtMidnight != "IN_TRANSIT")
            {
                names.Add(day.LocationAtMidnight);
            }

            foreach (var country in day.LocationsDuringDay)
            {
                if (!string.IsNullOrWhiteSpace(country) && country != "IN_TRANSIT")
                {
                    names.Add(country);
                }
            }
        }

        return names;
    }

    /// <summary>
    /// Rolling window whose end is the latest local date on the hypothetical legs.
    /// The start is 364 days earlier, so both ends count and the span is 365 days.
    /// </summary>
    public ForecastWindow GetForecastWindow(IReadOnlyList<Trip> hypotheticalTrips)
    {
        if (hypotheticalTrips == null || hypotheticalTrips.Count == 0)
        {
            throw new ArgumentException("At least one hypothetical trip is required.", nameof(hypotheticalTrips));
        }

        var latestArrival = hypotheticalTrips.Max(trip => trip.ArrivalDateTime);
        var latestDeparture = hypotheticalTrips.Max(trip => trip.DepartureDateTime);
        var forecastWindowEnd = latestArrival > latestDeparture ? latestArrival : latestDeparture;
        var forecastWindowStart = RollingWindow.StartOn(forecastWindowEnd);
        return new ForecastWindow(
            DateOnly.FromDateTime(forecastWindowStart.Date),
            DateOnly.FromDateTime(forecastWindowEnd.Date));
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

public readonly record struct ForecastWindow(DateOnly Start, DateOnly End);
