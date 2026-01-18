using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ResidencyRoll.Api.Data;
using ResidencyRoll.Api.Models;
using ResidencyRoll.Api.Services;
using Xunit;

namespace ResidencyRoll.Tests;

public class OverlappingTripsTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly TripService _tripService;
    private readonly string _testUserId = "test-user-123";

    public OverlappingTripsTests()
    {
        // Create an in-memory database for testing
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        var residencyService = new ResidencyCalculationService();
        _tripService = new TripService(_context, residencyService);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task GetDaysPerCountryInLast365Days_WithOverlappingTrips_CountsOnlyActualDaysInEachCountry()
    {
        // Arrange: Australia trip from Dec 23, 2025 to Jan 15, 2026 (24 days in window)
        // with a New Zealand trip nested inside from Jan 6 to Jan 9, 2026 (3 days)
        // Australia should count: Dec 23-Jan 6 (14 days) + Jan 9-Jan 15 (6 days) = 20 days
        // New Zealand should count: Jan 6-Jan 9 (3 days)
        // Note: Test window is last 365 days from today (Jan 17, 2026), 
        // so trips end at Jan 17 (exclusive) if they extend beyond
        var australiaTrip = new Trip
        {
            UserId = _testUserId,
            CountryName = "Australia",
            StartDate = new DateTime(2025, 12, 23),
            EndDate = new DateTime(2026, 1, 15)
        };

        var newZealandTrip = new Trip
        {
            UserId = _testUserId,
            CountryName = "New Zealand",
            StartDate = new DateTime(2026, 1, 6),
            EndDate = new DateTime(2026, 1, 9)
        };

        await _context.Trips.AddRangeAsync(australiaTrip, newZealandTrip);
        await _context.SaveChangesAsync();

        // Act
        var result = await _tripService.GetDaysPerCountryInLast365DaysAsync(_testUserId);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(20, result["Australia"]); // 23 total days minus 3 days in NZ
        Assert.Equal(3, result["New Zealand"]);
    }

    [Fact]
    public async Task GetDaysPerCountryInLast365Days_WithCompletelyOverlappingTrips_CountsOnlyLaterTrip()
    {
        // Arrange: Base trip to USA from Jan 1 to Jan 10 (9 days)
        // Overlapping trip to Canada from Jan 3 to Jan 7 (4 days) - completely inside USA trip
        // USA should count: Jan 1-3 (2 days) + Jan 7-10 (3 days) = 5 days
        // Canada should count: Jan 3-7 (4 days)
        var usaTrip = new Trip
        {
            UserId = _testUserId,
            CountryName = "USA",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 10)
        };

        var canadaTrip = new Trip
        {
            UserId = _testUserId,
            CountryName = "Canada",
            StartDate = new DateTime(2026, 1, 3),
            EndDate = new DateTime(2026, 1, 7)
        };

        await _context.Trips.AddRangeAsync(usaTrip, canadaTrip);
        await _context.SaveChangesAsync();

        // Act
        var result = await _tripService.GetDaysPerCountryInLast365DaysAsync(_testUserId);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(5, result["USA"]); // 9 total days minus 4 days in Canada
        Assert.Equal(4, result["Canada"]);
    }

    [Fact]
    public async Task GetDaysPerCountryInLast365Days_WithPartialOverlap_CountsCorrectly()
    {
        // Arrange: Trip to France from Jan 1 to Jan 10 (9 days)
        // Overlapping trip to Spain from Jan 8 to Jan 15 (7 days)
        // France should count: Jan 1-8 (7 days)
        // Spain should count: Jan 8-15 (7 days)
        var franceTrip = new Trip
        {
            UserId = _testUserId,
            CountryName = "France",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 10)
        };

        var spainTrip = new Trip
        {
            UserId = _testUserId,
            CountryName = "Spain",
            StartDate = new DateTime(2026, 1, 8),
            EndDate = new DateTime(2026, 1, 15)
        };

        await _context.Trips.AddRangeAsync(franceTrip, spainTrip);
        await _context.SaveChangesAsync();

        // Act
        var result = await _tripService.GetDaysPerCountryInLast365DaysAsync(_testUserId);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(7, result["France"]); // Jan 1-8
        Assert.Equal(7, result["Spain"]); // Jan 8-15
    }

    [Fact]
    public async Task GetDaysPerCountryInLast365Days_WithMultipleOverlaps_CountsCorrectly()
    {
        // Arrange: Complex scenario with multiple overlapping trips
        // Base trip to Germany: Jan 1 to Jan 16 (15 days, to stay within 365-day window ending Jan 17)
        // Trip to Austria: Jan 5 to Jan 10 (5 days)
        // Trip to Switzerland: Jan 12 to Jan 15 (3 days)
        // Germany should count: Jan 1-5 (4 days) + Jan 10-12 (2 days) + Jan 15-16 (1 day) = 7 days
        var germanyTrip = new Trip
        {
            UserId = _testUserId,
            CountryName = "Germany",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 16)
        };

        var austriaTrip = new Trip
        {
            UserId = _testUserId,
            CountryName = "Austria",
            StartDate = new DateTime(2026, 1, 5),
            EndDate = new DateTime(2026, 1, 10)
        };

        var switzerlandTrip = new Trip
        {
            UserId = _testUserId,
            CountryName = "Switzerland",
            StartDate = new DateTime(2026, 1, 12),
            EndDate = new DateTime(2026, 1, 15)
        };

        await _context.Trips.AddRangeAsync(germanyTrip, austriaTrip, switzerlandTrip);
        await _context.SaveChangesAsync();

        // Act
        var result = await _tripService.GetDaysPerCountryInLast365DaysAsync(_testUserId);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(7, result["Germany"]); // 15 days minus 5 (Austria) minus 3 (Switzerland)
        Assert.Equal(5, result["Austria"]);
        Assert.Equal(3, result["Switzerland"]);
    }

    [Fact]
    public async Task GetDaysPerCountryInLast365Days_WithNoOverlaps_CountsAllDays()
    {
        // Arrange: Non-overlapping trips
        // Note: Window ends at today (Jan 17), so we need to ensure trips are in the past
        var italyTrip = new Trip
        {
            UserId = _testUserId,
            CountryName = "Italy",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 10)
        };

        var greeceTrip = new Trip
        {
            UserId = _testUserId,
            CountryName = "Greece",
            StartDate = new DateTime(2026, 1, 11),
            EndDate = new DateTime(2026, 1, 16)
        };

        await _context.Trips.AddRangeAsync(italyTrip, greeceTrip);
        await _context.SaveChangesAsync();

        // Act
        var result = await _tripService.GetDaysPerCountryInLast365DaysAsync(_testUserId);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(9, result["Italy"]);
        Assert.Equal(5, result["Greece"]); // Jan 11-16 (5 days)
    }

    [Fact]
    public async Task ForecastDaysWithTrip_WithOverlappingHypotheticalTrip_CountsCorrectly()
    {
        // Arrange: Existing trip to Japan from Jan 1 to Jan 15 (14 days)
        // Forecast a trip to South Korea from Jan 10 to Jan 20 (10 days)
        // In forecast, Japan should count: Jan 1-10 (9 days)
        // South Korea should count: Jan 10-20 (10 days)
        var japanTrip = new Trip
        {
            UserId = _testUserId,
            ArrivalCountry = "Japan",
            ArrivalCity = "Tokyo",
            ArrivalDateTime = new DateTime(2026, 1, 1),
            ArrivalTimezone = "Asia/Tokyo",
            DepartureCountry = "Japan",
            DepartureCity = "Tokyo",
            DepartureDateTime = new DateTime(2026, 1, 15),
            DepartureTimezone = "Asia/Tokyo"
        };

        await _context.Trips.AddAsync(japanTrip);
        await _context.SaveChangesAsync();

        // Create hypothetical trip to South Korea
        var hypotheticalTrip = new Trip
        {
            UserId = _testUserId,
            ArrivalCountry = "South Korea",
            ArrivalCity = "Seoul",
            ArrivalDateTime = new DateTime(2026, 1, 10),
            ArrivalTimezone = "Asia/Seoul",
            DepartureCountry = "South Korea",
            DepartureCity = "Seoul",
            DepartureDateTime = new DateTime(2026, 1, 20),
            DepartureTimezone = "Asia/Seoul"
        };

        // Act
        var (current, forecast) = await _tripService.ForecastDaysWithTripAsync(_testUserId, hypotheticalTrip);

        // Assert - Current should just have Japan with full days
        Assert.Single(current);
        Assert.Equal(14, current["Japan"]);

        // Assert - Forecast should have Japan with reduced days and South Korea
        Assert.Equal(2, forecast.Count);
        Assert.Equal(9, forecast["Japan"]); // Jan 1-10
        Assert.Equal(10, forecast["South Korea"]); // Jan 10-20
    }

    [Fact]
    public async Task ForecastDaysWithTrip_WithHypotheticalTripInsideExisting_CountsCorrectly()
    {
        // Arrange: Long trip to Thailand starting 29 days ago and ending 14 days in the future
        // Forecast a trip to Vietnam from 3 days in the future to 8 days in the future (5 days)
        var today = DateTime.Today;
        var thailandStart = today.AddDays(-29);
        var thailandEnd = today.AddDays(14);
        var vietnamStart = today.AddDays(3);
        var vietnamEnd = today.AddDays(8);
        
        var thailandTrip = new Trip
        {
            UserId = _testUserId,
            ArrivalCountry = "Thailand",
            ArrivalCity = "Bangkok",
            ArrivalDateTime = thailandStart,
            ArrivalTimezone = "Asia/Bangkok",
            DepartureCountry = "Thailand",
            DepartureCity = "Bangkok",
            DepartureDateTime = thailandEnd,
            DepartureTimezone = "Asia/Bangkok"
        };

        await _context.Trips.AddAsync(thailandTrip);
        await _context.SaveChangesAsync();

        // Create hypothetical trip to Vietnam
        var hypotheticalTrip = new Trip
        {
            UserId = _testUserId,
            ArrivalCountry = "Vietnam",
            ArrivalCity = "Hanoi",
            ArrivalDateTime = vietnamStart,
            ArrivalTimezone = "Asia/Ho_Chi_Minh",
            DepartureCountry = "Vietnam",
            DepartureCity = "Hanoi",
            DepartureDateTime = vietnamEnd,
            DepartureTimezone = "Asia/Ho_Chi_Minh"
        };

        // Act - forecast window is 365 days ending at vietnamEnd
        var (current, forecast) = await _tripService.ForecastDaysWithTripAsync(_testUserId, hypotheticalTrip);

        // Assert - Current window (last 365 from today)
        // Thailand from 29 days ago to today = 29 days
        Assert.Single(current);
        Assert.Equal(29, current["Thailand"]);

        // Assert - Forecast should have reduced Thailand and new Vietnam
        // Forecast window: vietnamEnd - 365 days to vietnamEnd = (today+8) - 365 to (today+8) = today-357 to today+8
        // Thailand: starts at today-29, ends at today+14
        // Thailand in forecast window: from today-29 to today+8 = 37 days total
        // Vietnam: starts at today+3, ends at today+8 = 5 days
        // Vietnam overlaps Thailand from today+3 to today+8, so Thailand loses those 5 days
        // Thailand counts: 37 - 5 = 32 days (from today-29 to today+3)
        Assert.Equal(2, forecast.Count);
        Assert.Equal(32, forecast["Thailand"]);
        Assert.Equal(5, forecast["Vietnam"]);
    }

    [Fact]
    public async Task GetDaysPerCountryInLast365Days_WithSameCountryOverlappingItself_CountsCorrectly()
    {
        // Arrange: Two trips to the same country that overlap
        // This could happen if someone records a base trip and then adds specific side trips
        // Trip 1: Mexico Jan 1 to Jan 16 (15 days, within window)
        // Trip 2: Mexico Jan 10 to Jan 15 (5 days) - same country, nested
        // Total for Mexico should be 15 days (not 20)
        var mexicoTrip1 = new Trip
        {
            UserId = _testUserId,
            CountryName = "Mexico",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 16)
        };

        var mexicoTrip2 = new Trip
        {
            UserId = _testUserId,
            CountryName = "Mexico",
            StartDate = new DateTime(2026, 1, 10),
            EndDate = new DateTime(2026, 1, 15)
        };

        await _context.Trips.AddRangeAsync(mexicoTrip1, mexicoTrip2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _tripService.GetDaysPerCountryInLast365DaysAsync(_testUserId);

        // Assert
        // First trip contributes Jan 1-10 (9 days) + Jan 15-16 (1 day) = 10 days
        // Second trip contributes Jan 10-15 (5 days)
        // Total: 10 + 5 = 15 days
        Assert.Single(result);
        Assert.Equal(15, result["Mexico"]);
    }

    [Fact]
    public async Task GetDaysPerCountryInLast365Days_WithAdjacentTrips_CountsCorrectly()
    {
        // Arrange: Two trips that are adjacent (one ends when the next starts)
        // Portugal: Jan 1 to Jan 10 (9 days, exclusive end)
        // Morocco: Jan 10 to Jan 16 (6 days, within window ending Jan 17)
        // No overlap - should count all days
        var portugalTrip = new Trip
        {
            UserId = _testUserId,
            CountryName = "Portugal",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 10)
        };

        var moroccoTrip = new Trip
        {
            UserId = _testUserId,
            CountryName = "Morocco",
            StartDate = new DateTime(2026, 1, 10),
            EndDate = new DateTime(2026, 1, 16)
        };

        await _context.Trips.AddRangeAsync(portugalTrip, moroccoTrip);
        await _context.SaveChangesAsync();

        // Act
        var result = await _tripService.GetDaysPerCountryInLast365DaysAsync(_testUserId);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(9, result["Portugal"]);
        Assert.Equal(6, result["Morocco"]);
    }
}
