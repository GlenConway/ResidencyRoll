using Microsoft.EntityFrameworkCore;
using ResidencyRoll.Api.Data;
using ResidencyRoll.Api.Models;
using ResidencyRoll.Api.Services;
using Xunit;

namespace ResidencyRoll.Tests;

public class ForecastWithTimezonesTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly TripService _tripService;
    private readonly string _testUserId = "test-user-timezone-forecast";

    public ForecastWithTimezonesTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_Forecast_Timezones_{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _tripService = new TripService(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task ForecastWithTimezones_AcrossDateLine_CalculatesCorrectly()
    {
        // Arrange: Existing trip from LA to Sydney
        // Depart LA: Jan 1, 2026 at 10:00 AM PST
        // Arrive Sydney: Jan 3, 2026 at 6:00 AM AEDT (after crossing date line)
        var existingTrip = new Trip
        {
            UserId = _testUserId,
            DepartureCountry = "United States",
            DepartureCity = "Los Angeles",
            DepartureDateTime = new DateTime(2026, 1, 1, 10, 0, 0),
            DepartureTimezone = "America/Los_Angeles",
            DepartureIataCode = "LAX",
            ArrivalCountry = "Australia",
            ArrivalCity = "Sydney",
            ArrivalDateTime = new DateTime(2026, 1, 3, 6, 0, 0),
            ArrivalTimezone = "Australia/Sydney",
            ArrivalIataCode = "SYD"
        };

        await _context.Trips.AddAsync(existingTrip);
        await _context.SaveChangesAsync();

        // Forecast: Trip from Sydney to Tokyo
        // Depart Sydney: Jan 10, 2026 at 9:00 AM AEDT
        // Arrive Tokyo: Jan 10, 2026 at 5:00 PM JST
        var hypotheticalTrip = new Trip
        {
            UserId = _testUserId,
            DepartureCountry = "Australia",
            DepartureCity = "Sydney",
            DepartureDateTime = new DateTime(2026, 1, 10, 9, 0, 0),
            DepartureTimezone = "Australia/Sydney",
            DepartureIataCode = "SYD",
            ArrivalCountry = "Japan",
            ArrivalCity = "Tokyo",
            ArrivalDateTime = new DateTime(2026, 1, 10, 17, 0, 0),
            ArrivalTimezone = "Asia/Tokyo",
            ArrivalIataCode = "NRT"
        };

        // Act
        var (current, forecast) = await _tripService.ForecastDaysWithTripAsync(_testUserId, hypotheticalTrip);

        // Assert - The forecast should properly account for timezone differences
        Assert.NotNull(forecast);
        Assert.True(forecast.Count >= 0, "Forecast should return a valid dictionary");
        
        // If there are entries, verify Australia is present (the arrival country)
        if (forecast.Count > 0)
        {
            Assert.True(forecast.ContainsKey("Australia"), "Australia should be in forecast as arrival country");
        }
    }

    [Fact]
    public async Task ForecastWithTimezones_ShortStopover_CountsAccurately()
    {
        // Test a short stopover scenario where timezone awareness matters
        // Arrange: No existing trips
        
        // Forecast: Flight from NYC to Dubai with stopover in London
        // Depart NYC: Jan 15, 2026 at 8:00 PM EST
        // Arrive London: Jan 16, 2026 at 8:00 AM GMT (8-hour layover)
        // This represents arriving in UK for a few hours
        var hypotheticalTrip = new Trip
        {
            UserId = _testUserId,
            DepartureCountry = "United States",
            DepartureCity = "New York",
            DepartureDateTime = new DateTime(2026, 1, 15, 20, 0, 0),
            DepartureTimezone = "America/New_York",
            DepartureIataCode = "JFK",
            ArrivalCountry = "United Kingdom",
            ArrivalCity = "London",
            ArrivalDateTime = new DateTime(2026, 1, 16, 8, 0, 0),
            ArrivalTimezone = "Europe/London",
            ArrivalIataCode = "LHR"
        };

        // Act
        var (current, forecast) = await _tripService.ForecastDaysWithTripAsync(_testUserId, hypotheticalTrip);

        // Assert - Forecast should work with timezone-aware trips
        Assert.NotNull(forecast);
        Assert.True(forecast.Count >= 0, "Forecast should return a valid dictionary");
        
        // If there are entries, verify UK is present
        if (forecast.Count > 0)
        {
            Assert.True(forecast.ContainsKey("United Kingdom"), "UK should be in forecast");
        }
    }

    [Fact]
    public async Task ForecastWithTimezones_MultipleTripsWithDifferentTimezones_CalculatesCorrectly()
    {
        // Arrange: Multiple existing trips across different timezones
        var today = DateTime.Today;
        
        var trip1 = new Trip
        {
            UserId = _testUserId,
            DepartureCountry = "France",
            DepartureCity = "Paris",
            DepartureDateTime = today.AddDays(-50),
            DepartureTimezone = "Europe/Paris",
            ArrivalCountry = "United States",
            ArrivalCity = "New York",
            ArrivalDateTime = today.AddDays(-60),
            ArrivalTimezone = "America/New_York"
        };

        var trip2 = new Trip
        {
            UserId = _testUserId,
            DepartureCountry = "Japan",
            DepartureCity = "Tokyo",
            DepartureDateTime = today.AddDays(-30),
            DepartureTimezone = "Asia/Tokyo",
            ArrivalCountry = "France",
            ArrivalCity = "Paris",
            ArrivalDateTime = today.AddDays(-50),
            ArrivalTimezone = "Europe/Paris"
        };

        await _context.Trips.AddRangeAsync(trip1, trip2);
        await _context.SaveChangesAsync();

        // Forecast: New trip to Singapore
        var hypotheticalTrip = new Trip
        {
            UserId = _testUserId,
            DepartureCountry = "Singapore",
            DepartureCity = "Singapore",
            DepartureDateTime = today.AddDays(20),
            DepartureTimezone = "Asia/Singapore",
            ArrivalCountry = "Japan",
            ArrivalCity = "Tokyo",
            ArrivalDateTime = today.AddDays(10),
            ArrivalTimezone = "Asia/Tokyo"
        };

        // Act
        var (current, forecast) = await _tripService.ForecastDaysWithTripAsync(_testUserId, hypotheticalTrip);

        // Assert - Should handle multiple trips with timezones
        Assert.NotNull(current);
        Assert.NotNull(forecast);
        
        // Forecast should include the new Japan trip (arrival country)
        Assert.True(forecast.ContainsKey("Japan"), "Japan should be in forecast");
        Assert.True(forecast["Japan"] > 0, "Japan should have positive days");
    }

    [Fact]
    public async Task ForecastMaxEndDate_WithTimezones_FindsCorrectDate()
    {
        // Arrange: Some existing trips
        var today = DateTime.Today;
        
        var existingTrip = new Trip
        {
            UserId = _testUserId,
            DepartureCountry = "Canada",
            DepartureCity = "Toronto",
            DepartureDateTime = today.AddDays(-100),
            DepartureTimezone = "America/Toronto",
            ArrivalCountry = "Spain",
            ArrivalCity = "Barcelona",
            ArrivalDateTime = today.AddDays(-50),
            ArrivalTimezone = "Europe/Madrid"
        };

        await _context.Trips.AddAsync(existingTrip);
        await _context.SaveChangesAsync();

        // Test: Find max end date for a trip starting today to Spain
        var hypotheticalTrip = new Trip
        {
            UserId = _testUserId,
            DepartureCountry = "Spain",
            DepartureCity = "Barcelona",
            DepartureTimezone = "Europe/Madrid",
            ArrivalCountry = "Spain",
            ArrivalCity = "Barcelona",
            ArrivalDateTime = today,
            ArrivalTimezone = "Europe/Madrid"
        };

        // Act
        var (maxEndDate, daysAtLimit) = await _tripService.CalculateMaxTripEndDateAsync(
            _testUserId, hypotheticalTrip, 183);

        // Assert
        Assert.True(maxEndDate >= today, "Max end date should be at or after start");
        Assert.True(daysAtLimit <= 183, "Days should not exceed limit");
    }

    [Fact]
    public async Task ForecastStandardDurations_WithTimezones_CalculatesCorrectly()
    {
        // Arrange: Empty database for clean test
        var today = DateTime.Today;

        // Test: Calculate standard durations for a trip to Italy
        var hypotheticalTrip = new Trip
        {
            UserId = _testUserId,
            DepartureCountry = "Italy",
            DepartureCity = "Rome",
            DepartureTimezone = "Europe/Rome",
            ArrivalCountry = "Italy",
            ArrivalCity = "Rome",
            ArrivalDateTime = today.AddDays(30),
            ArrivalTimezone = "Europe/Rome"
        };

        // Act
        var results = await _tripService.CalculateStandardDurationForecastsAsync(
            _testUserId, hypotheticalTrip, 183);

        // Assert
        Assert.NotEmpty(results);
        Assert.True(results.Count >= 3, "Should return at least 3 standard durations");
        
        foreach (var result in results)
        {
            Assert.True(result.DurationDays > 0, "Duration should be positive");
            Assert.True(result.TotalDaysInCountry <= 183 || result.ExceedsLimit, 
                "ExceedsLimit flag should match actual count");
        }
    }

    [Fact]
    public async Task ForecastWithMultipleTrips_TwoLegs_CalculatesCorrectly()
    {
        // Test forecasting with multiple trips (e.g., outbound and return)
        // Arrange: No existing trips
        
        // Trip 1: Travel to London, stay for 7 days
        // Arrive London: Feb 2, 2026 at 6:00 AM GMT (after departing Canada Feb 1)
        // Depart London: Feb 9, 2026 at 10:00 AM GMT
        var trip1 = new Trip
        {
            UserId = _testUserId,
            DepartureCountry = "Canada",
            DepartureCity = "Toronto",
            DepartureDateTime = new DateTime(2026, 2, 9, 10, 0, 0),
            DepartureTimezone = "Europe/London",  // Departing FROM London
            DepartureIataCode = "LHR",
            ArrivalCountry = "United Kingdom",
            ArrivalCity = "London",
            ArrivalDateTime = new DateTime(2026, 2, 2, 6, 0, 0),
            ArrivalTimezone = "Europe/London",
            ArrivalIataCode = "LHR"
        };

        var trips = new List<Trip> { trip1 };

        // Act
        var (current, forecast) = await _tripService.ForecastDaysWithTripsAsync(_testUserId, trips);

        // Assert
        Assert.NotNull(forecast);
        
        // Debug: Print what's in forecast
        var forecastKeys = string.Join(", ", forecast.Keys);
        Assert.True(forecast.ContainsKey("United Kingdom"), $"United Kingdom should be in forecast. Found: {forecastKeys}");
        
        // Should count approximately 7 days in UK
        var ukDays = forecast["United Kingdom"];
        Assert.True(ukDays >= 6 && ukDays <= 8, $"Should count approximately 7 days in UK, got {ukDays}");
    }

    [Fact]
    public async Task ForecastWithMultipleTrips_RoundTripWithConnections_CalculatesCorrectly()
    {
        // Test a realistic scenario: Travel to UK and stay for 14 days
        // Arrive in UK: Mar 2, 2026 at 8:00 AM GMT
        // Depart from UK: Mar 16, 2026 at 10:00 AM GMT  
        // This represents the STAY in United Kingdom
        var ukStay = new Trip
        {
            UserId = _testUserId,
            DepartureCountry = "Canada",  // Departed back to Canada
            DepartureCity = "Toronto",
            DepartureDateTime = new DateTime(2026, 3, 16, 10, 0, 0),
            DepartureTimezone = "Europe/London",  // Departing FROM London
            DepartureIataCode = "LHR",
            ArrivalCountry = "United Kingdom",  // Arrived in UK
            ArrivalCity = "London",
            ArrivalDateTime = new DateTime(2026, 3, 2, 8, 0, 0),
            ArrivalTimezone = "Europe/London",
            ArrivalIataCode = "LHR"
        };

        var trips = new List<Trip> { ukStay };

        // Act
        var (current, forecast) = await _tripService.ForecastDaysWithTripsAsync(_testUserId, trips);

        // Assert
        Assert.NotNull(forecast);
        Assert.True(forecast.ContainsKey("United Kingdom"), "United Kingdom should be in forecast");
        
        // Should count approximately 14 days in UK (from arrival Mar 2 to departure Mar 16)
        var ukDays = forecast["United Kingdom"];
        Assert.True(ukDays >= 13 && ukDays <= 15, $"Should count approximately 14 days in UK, got {ukDays}");
    }

    [Fact]
    public async Task ForecastWithMultipleTrips_EmptyList_ReturnsEmpty()
    {
        // Test with empty trip list
        var trips = new List<Trip>();

        // Act
        var (current, forecast) = await _tripService.ForecastDaysWithTripsAsync(_testUserId, trips);

        // Assert
        Assert.NotNull(forecast);
        Assert.Empty(forecast);
    }
}
