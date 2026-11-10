using Microsoft.EntityFrameworkCore;
using ResidencyRoll.Api.Data;
using ResidencyRoll.Api.Models;

namespace ResidencyRoll.Api.Services;

public class TripService
{
    private readonly ApplicationDbContext _context;
    private readonly ResidencyCalculationService _residencyService;
    private readonly TravellerAccess _access;

    public TripService(ApplicationDbContext context, ResidencyCalculationService residencyService, TravellerAccess? access = null)
    {
        _context = context;
        _residencyService = residencyService;
        _access = access ?? new TravellerAccess(context);
    }

    public Task<Traveller> ResolveSelfAsync(string userId) => _access.ResolveSelfAsync(userId);

    public async Task<List<Trip>> GetAllTripsAsync(string userId, Traveller? traveller = null)
    {
        var scope = await Scope(userId, traveller);
        return await _access.TripsFor(scope).OrderBy(trip => trip.DepartureDateTime).ToListAsync();
    }

    public async Task<Trip?> GetTripByIdAsync(int id, string userId, Traveller? traveller = null)
    {
        var scope = await Scope(userId, traveller);
        return await _access.TripsFor(scope).FirstOrDefaultAsync(trip => trip.Id == id);
    }

    public async Task<Trip> CreateTripAsync(Trip trip, string? actorUserId = null)
    {
        if (!string.IsNullOrEmpty(actorUserId))
        {
            trip.CreatedByUserId ??= actorUserId;
        }

        _context.Trips.Add(trip);
        await _context.SaveChangesAsync();
        return trip;
    }

    public async Task<List<Trip>> CreateForTravellersAsync(Trip template, IReadOnlyList<Traveller> travellers, string actorUserId)
    {
        if (travellers.Count == 0)
        {
            throw new ArgumentException("At least one traveller is required.", nameof(travellers));
        }

        var created = travellers.Select(traveller => TripCopies.Onto(template, traveller, actorUserId)).ToList();
        return await SaveCreatedAsync(created);
    }

    public async Task<List<Trip>> CreateLegsForTravellersAsync(IReadOnlyList<Trip> templates, IReadOnlyList<Traveller> travellers, string actorUserId)
    {
        if (travellers.Count == 0 || templates.Count == 0)
        {
            throw new ArgumentException("At least one traveller and one leg are required.");
        }

        var created = new List<Trip>();
        foreach (var template in templates)
        {
            foreach (var traveller in travellers)
            {
                created.Add(TripCopies.Onto(template, traveller, actorUserId));
            }
        }

        return await SaveCreatedAsync(created);
    }

    private async Task<List<Trip>> SaveCreatedAsync(List<Trip> created)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.Trips.AddRange(created);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return created;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
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

    public async Task<Trip> UpdateTripAsync(Trip trip, string userId, Traveller? traveller = null)
    {
        var existing = await GetTripByIdAsync(trip.Id, userId, traveller);
        if (existing == null)
        {
            throw new UnauthorizedAccessException("Trip not found or you don't have permission to update it.");
        }

        if (!ReferenceEquals(existing, trip))
        {
            _context.Entry(existing).CurrentValues.SetValues(trip);
        }

        existing.UpdatedByUserId = userId;
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteTripAsync(int id, string userId, Traveller? traveller = null)
    {
        var trip = await GetTripByIdAsync(id, userId, traveller);
        if (trip != null)
        {
            _context.Trips.Remove(trip);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Dictionary<string, int>> GetTotalDaysPerCountryAsync(string userId, Traveller? traveller = null)
    {
        var trips = await GetAllTripsAsync(userId, traveller);
        var presenceLog = _residencyService.GenerateDailyPresenceLog(trips);
        return _residencyService.CalculateResidencyDays(presenceLog);
    }

    public async Task<Dictionary<string, int>> GetDaysPerCountryInLast365DaysAsync(string userId, Traveller? traveller = null)
    {
        var today = DateTime.Today;
        var windowStart = RollingWindow.StartOn(today);
        var trips = await GetAllTripsAsync(userId, traveller);
        return CalculateDaysPerCountryByDateRange(trips, windowStart, today);
    }

    public async Task<(Dictionary<string, int> Current, Dictionary<string, int> Forecast)> ForecastDaysWithTripsAsync(
        string userId,
        List<Trip> hypotheticalTrips,
        DateOnly? through = null,
        Traveller? traveller = null)
    {
        var trips = await GetAllTripsAsync(userId, traveller);
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

    public async Task<int> GetTotalDaysAwayInLast365DaysAsync(string userId, Traveller? traveller = null)
    {
        var daysPerCountry = await GetDaysPerCountryInLast365DaysAsync(userId, traveller);
        return daysPerCountry.Values.Sum();
    }

    public async Task<int> GetDaysAtHomeInLast365DaysAsync(string userId, Traveller? traveller = null)
    {
        var totalDaysAway = await GetTotalDaysAwayInLast365DaysAsync(userId, traveller);
        return 365 - totalDaysAway;
    }

    public Task<List<Trip>> GetTripsForTimelineAsync(string userId, Traveller? traveller = null) =>
        GetAllTripsAsync(userId, traveller);

    /// <summary>
    /// Deletes every id, or none of them. A missing id rolls the batch back.
    /// </summary>
    public async Task DeleteTripsAsync(IReadOnlyCollection<int> ids, string userId, Traveller? traveller = null)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0)
        {
            throw new ArgumentException("At least one trip id is required.", nameof(ids));
        }

        var scope = await Scope(userId, traveller);
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var found = await _access.TripsFor(scope).Where(trip => distinct.Contains(trip.Id)).ToListAsync();
            var missing = distinct.Except(found.Select(trip => trip.Id)).OrderBy(id => id).ToList();
            if (missing.Count > 0)
            {
                throw new InvalidOperationException($"Trips not found: {string.Join(", ", missing)}.");
            }

            _context.Trips.RemoveRange(found);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Deletes the listed trips and inserts the new ones, or leaves the traveller unchanged.
    /// </summary>
    public async Task<List<Trip>> ReplaceTripsAsync(
        IReadOnlyCollection<int> deleteIds,
        IReadOnlyList<Trip> created,
        string userId,
        Traveller? traveller = null)
    {
        var distinct = deleteIds.Distinct().ToList();
        var scope = await Scope(userId, traveller);
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var found = distinct.Count == 0
                ? []
                : await _access.TripsFor(scope).Where(trip => distinct.Contains(trip.Id)).ToListAsync();
            var missing = distinct.Except(found.Select(trip => trip.Id)).OrderBy(id => id).ToList();
            if (missing.Count > 0)
            {
                throw new InvalidOperationException($"Trips not found: {string.Join(", ", missing)}.");
            }

            if (found.Count > 0)
            {
                _context.Trips.RemoveRange(found);
            }

            if (created.Count > 0)
            {
                _context.Trips.AddRange(created);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return created.ToList();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<Traveller> Scope(string userId, Traveller? traveller) =>
        traveller ?? await _access.ResolveSelfAsync(userId);

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
