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

        return CalculateDaysPerCountryWithOverlapHandling(trips, windowStart, today);
    }

    /// <summary>
    /// Calculates days per country within a time window, accounting for overlapping trips.
    /// When trips overlap, days are only counted in the trip where they actually occurred.
    /// </summary>
    private Dictionary<string, int> CalculateDaysPerCountryWithOverlapHandling(
        List<Trip> trips, DateTime windowStart, DateTime windowEnd)
    {
        var daysPerCountry = new Dictionary<string, int>();

        // Sort trips by start date to process them chronologically
        var sortedTrips = trips
            .OrderBy(t => t.StartDate)
            .ToList();

        // Build a list of date ranges for each trip within the window
        var tripRanges = new List<(string Country, DateTime Start, DateTime End)>();

        foreach (var trip in sortedTrips)
        {
            var overlapStart = trip.StartDate > windowStart ? trip.StartDate : windowStart;
            var overlapEnd = trip.EndDate < windowEnd ? trip.EndDate : windowEnd;

            if (overlapStart < overlapEnd)
            {
                tripRanges.Add((trip.CountryName, overlapStart, overlapEnd));
            }
        }

        // For each trip range, calculate actual days excluding overlaps with later trips
        for (int i = 0; i < tripRanges.Count; i++)
        {
            var (country, start, end) = tripRanges[i];
            var currentDate = start;

            // Count each day in the current trip
            while (currentDate < end)
            {
                var nextDate = currentDate.AddDays(1);
                
                // Check if this day is covered by any later trip (which takes precedence)
                bool coveredByLaterTrip = false;
                for (int j = i + 1; j < tripRanges.Count; j++)
                {
                    var (_, laterStart, laterEnd) = tripRanges[j];
                    if (currentDate >= laterStart && currentDate < laterEnd)
                    {
                        coveredByLaterTrip = true;
                        break;
                    }
                }

                // Only count the day if it's not covered by a later trip
                if (!coveredByLaterTrip)
                {
                    if (!daysPerCountry.ContainsKey(country))
                    {
                        daysPerCountry[country] = 0;
                    }
                    daysPerCountry[country]++;
                }

                currentDate = nextDate;
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

    public async Task<(Dictionary<string, int> Current, Dictionary<string, int> Forecast)> ForecastDaysWithTripAsync(string userId, Trip hypotheticalTrip)
    {
        var today = DateTime.Today;
        var currentWindowStart = today.AddDays(-365);

        // Use the departure date/time as the end of the trip for forecast window calculation
        var forecastWindowEnd = hypotheticalTrip.DepartureDateTime;
        var forecastWindowStart = forecastWindowEnd.AddDays(-365);

        var trips = await _context.Trips
            .Where(t => t.UserId == userId)
            .ToListAsync();

        // Calculate current window (last 365 days from today)
        var currentDaysPerCountry = CalculateDaysPerCountryWithOverlapHandling(trips, currentWindowStart, today);

        // For forecast, include the hypothetical trip
        var tripsWithHypothetical = new List<Trip>(trips)
        {
            hypotheticalTrip
        };

        var forecastDaysPerCountry = CalculateDaysPerCountryWithOverlapHandling(tripsWithHypothetical, forecastWindowStart, forecastWindowEnd);

        return (currentDaysPerCountry, forecastDaysPerCountry);
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
        var currentDaysPerCountry = CalculateDaysPerCountryWithOverlapHandling(trips, currentWindowStart, today);

        // For forecast, include all hypothetical trips
        var tripsWithHypothetical = new List<Trip>(trips);
        tripsWithHypothetical.AddRange(hypotheticalTrips);

        var forecastDaysPerCountry = CalculateDaysPerCountryWithOverlapHandling(tripsWithHypothetical, forecastWindowStart, forecastWindowEnd);

        return (currentDaysPerCountry, forecastDaysPerCountry);
    }

    public async Task<(DateTime MaxEndDate, int DaysAtLimit)> CalculateMaxTripEndDateAsync(string userId, Trip hypotheticalTrip, int dayLimit = 183)
    {
        var trips = await _context.Trips
            .Where(t => t.UserId == userId)
            .ToListAsync();

        DateTime tripStart = hypotheticalTrip.ArrivalDateTime;
        string arrivalCountry = hypotheticalTrip.ArrivalCountry;
        DateTime currentEnd = tripStart;
        int daysUsed = 0;

        DateTime minDate = tripStart;
        DateTime maxDate = tripStart.AddDays(365);

        while (minDate < maxDate)
        {
            DateTime midDate = minDate.AddDays((maxDate - minDate).Days / 2);
            
            // Create a hypothetical trip with the midpoint as departure date
            var testTrip = new Trip
            {
                UserId = userId,
                DepartureCountry = hypotheticalTrip.DepartureCountry,
                DepartureCity = hypotheticalTrip.DepartureCity,
                DepartureDateTime = midDate,
                DepartureTimezone = hypotheticalTrip.DepartureTimezone,
                DepartureIataCode = hypotheticalTrip.DepartureIataCode,
                ArrivalCountry = hypotheticalTrip.ArrivalCountry,
                ArrivalCity = hypotheticalTrip.ArrivalCity,
                ArrivalDateTime = hypotheticalTrip.ArrivalDateTime,
                ArrivalTimezone = hypotheticalTrip.ArrivalTimezone,
                ArrivalIataCode = hypotheticalTrip.ArrivalIataCode
            };
            
            var (_, forecastDays) = await ForecastDaysWithTripAsync(userId, testTrip);

            int totalDays = forecastDays.ContainsKey(arrivalCountry) ? forecastDays[arrivalCountry] : 0;

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

    public async Task<List<(int DurationDays, DateTime EndDate, int TotalDaysInCountry, bool ExceedsLimit)>> CalculateStandardDurationForecastsAsync(string userId, Trip hypotheticalTrip, int dayLimit = 183, int[]? durations = null)
    {
        var results = new List<(int, DateTime, int, bool)>();
        int[] requestedDurations = durations is { Length: > 0 } ? durations : new[] { 7, 14, 21 };
        string arrivalCountry = hypotheticalTrip.ArrivalCountry;
        DateTime tripStart = hypotheticalTrip.ArrivalDateTime;

        foreach (var duration in requestedDurations)
        {
            var endDate = tripStart.AddDays(duration);
            
            // Create a hypothetical trip with the calculated end date
            var testTrip = new Trip
            {
                UserId = userId,
                DepartureCountry = hypotheticalTrip.DepartureCountry,
                DepartureCity = hypotheticalTrip.DepartureCity,
                DepartureDateTime = endDate,
                DepartureTimezone = hypotheticalTrip.DepartureTimezone,
                DepartureIataCode = hypotheticalTrip.DepartureIataCode,
                ArrivalCountry = hypotheticalTrip.ArrivalCountry,
                ArrivalCity = hypotheticalTrip.ArrivalCity,
                ArrivalDateTime = hypotheticalTrip.ArrivalDateTime,
                ArrivalTimezone = hypotheticalTrip.ArrivalTimezone,
                ArrivalIataCode = hypotheticalTrip.ArrivalIataCode
            };
            
            var (_, forecastDays) = await ForecastDaysWithTripAsync(userId, testTrip);

            int totalDays = forecastDays.ContainsKey(arrivalCountry) ? forecastDays[arrivalCountry] : 0;
            bool exceedsLimit = totalDays > dayLimit;

            results.Add((duration, endDate, totalDays, exceedsLimit));
        }

        return results;
    }
}
