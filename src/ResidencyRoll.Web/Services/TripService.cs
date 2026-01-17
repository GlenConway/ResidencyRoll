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

        var forecastWindowEnd = tripEnd;
        var forecastWindowStart = tripEnd.AddDays(-365);

        var trips = await _context.Trips.ToListAsync();

        // Calculate current window (last 365 days from today)
        var currentDaysPerCountry = CalculateDaysPerCountryWithOverlapHandling(trips, currentWindowStart, today);

        // For forecast, include the hypothetical trip
        var tripsWithHypothetical = new List<Trip>(trips)
        {
            new Trip
            {
                CountryName = countryName,
                StartDate = tripStart,
                EndDate = tripEnd
            }
        };

        var forecastDaysPerCountry = CalculateDaysPerCountryWithOverlapHandling(tripsWithHypothetical, forecastWindowStart, forecastWindowEnd);

        return (currentDaysPerCountry, forecastDaysPerCountry);
    }

    /// <summary>
    /// Calculates the maximum end date for a trip starting on tripStart in a given country
    /// such that the total days in that country within the 365-day window (ending at that max end date)
    /// does not exceed the limit (183 days). Returns the date and the actual day count.
    /// </summary>
    public async Task<(DateTime MaxEndDate, int DaysAtLimit)> CalculateMaxTripEndDateAsync(
        string countryName, DateTime tripStart, int dayLimit = 183)
    {
        var trips = await _context.Trips.ToListAsync();
        
        // Start with the trip start date; we'll increment the end date to find the maximum
        DateTime currentEnd = tripStart;
        int daysUsed = 0;

        // Binary search approach: find the latest end date that keeps us at or under the limit
        DateTime minDate = tripStart;
        DateTime maxDate = tripStart.AddDays(365); // reasonable upper bound

        while (minDate < maxDate)
        {
            double midDays = (maxDate - minDate).Days / 2.0;
            DateTime midDate = minDate.AddDays(Math.Floor(midDays));
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

    /// <summary>
    /// Calculates forecast results for standard trip durations (7, 14, 21 days) 
    /// starting from the given start date.
    /// </summary>
    public async Task<List<(int DurationDays, DateTime EndDate, int TotalDaysInCountry, bool ExceedsLimit)>> 
        CalculateStandardDurationForecastsAsync(string countryName, DateTime tripStart, int dayLimit = 183)
    {
        var results = new List<(int, DateTime, int, bool)>();
        int[] durations = { 7, 14, 21 };

        foreach (var duration in durations)
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
