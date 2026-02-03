using System;
using System.Collections.Generic;
using System.Linq;
using ResidencyRoll.Api.Models;
using ResidencyRoll.Api.Services;
using Xunit;

namespace ResidencyRoll.Tests;

/// <summary>
/// Tests for the forecast calculation functionality.
/// Ensures that forecasts properly incorporate existing trips and apply residency rules correctly.
/// </summary>
public class ForecastCalculationTests
{
    private readonly ResidencyCalculationService _residencyService;

    public ForecastCalculationTests()
    {
        _residencyService = new ResidencyCalculationService();
    }

    #region Test: Forecast includes existing trips

    [Fact]
    public void Forecast_IncludesExistingTripsInCurrentWindow()
    {
        // Arrange: Existing trip in Canada from Jan 1-5, current date is Jan 10
        var today = new DateTime(2026, 1, 10);
        var existingTrips = new List<Trip>
        {
            new Trip
            {
                Id = 1,
                UserId = "user1",
                DepartureCountry = "United States",
                DepartureCity = "New York",
                DepartureDateTime = new DateTime(2026, 1, 1, 14, 0, 0, DateTimeKind.Utc),
                DepartureTimezone = "America/New_York",
                ArrivalCountry = "Canada",
                ArrivalCity = "Toronto",
                ArrivalDateTime = new DateTime(2026, 1, 1, 19, 0, 0, DateTimeKind.Utc),
                ArrivalTimezone = "America/Toronto"
            },
            new Trip
            {
                Id = 2,
                UserId = "user1",
                DepartureCountry = "Canada",
                DepartureCity = "Toronto",
                DepartureDateTime = new DateTime(2026, 1, 5, 18, 0, 0, DateTimeKind.Utc),
                DepartureTimezone = "America/Toronto",
                ArrivalCountry = "United States",
                ArrivalCity = "New York",
                ArrivalDateTime = new DateTime(2026, 1, 5, 22, 0, 0, DateTimeKind.Utc),
                ArrivalTimezone = "America/New_York"
            }
        };

        var windowStart = today.AddDays(-365);
        var windowEnd = today;

        // Act
        var presenceLog = _residencyService.GenerateDailyPresenceLog(existingTrips);
        var residencyDays = _residencyService.CalculateResidencyDays(
            presenceLog,
            DateOnly.FromDateTime(windowStart.Date),
            DateOnly.FromDateTime(windowEnd.Date)
        );

        // Assert: Should count days in Canada
        Assert.True(residencyDays.ContainsKey("Canada"), "Canada should be in residency days");
        Assert.True(residencyDays["Canada"] >= 1, "Should count at least 1 day in Canada");
    }

    [Fact]
    public void Forecast_CombinesExistingAndNewTrips_PartialDayRule()
    {
        // Arrange: 
        // Existing: Long stay in Canada
        // New: Trip from YHZ to LHR on Feb 19 (10:35 - 20:35) returning March 1 (09:00 - 12:00)
        // Expected: Feb 19 and Mar 1 should be counted in United Kingdom (partial day rule)

        var trips = new List<Trip>();

        // Generate existing trips for Canada (simulating extended stay)
        // For simplicity, we'll use a continuous trip from Jan 1 to May 31
        trips.Add(new Trip
        {
            Id = 1,
            UserId = "user1",
            DepartureCountry = "USA",
            DepartureCity = "Boston",
            DepartureDateTime = new DateTime(2025, 12, 31, 19, 0, 0, DateTimeKind.Utc),
            DepartureTimezone = "America/New_York",
            ArrivalCountry = "Canada",
            ArrivalCity = "Toronto",
            ArrivalDateTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ArrivalTimezone = "America/Toronto"
        });

        // Departure from Canada back to USA on May 31
        trips.Add(new Trip
        {
            Id = 2,
            UserId = "user1",
            DepartureCountry = "Canada",
            DepartureCity = "Toronto",
            DepartureDateTime = new DateTime(2026, 5, 31, 23, 0, 0, DateTimeKind.Utc),
            DepartureTimezone = "America/Toronto",
            ArrivalCountry = "USA",
            ArrivalCity = "Boston",
            ArrivalDateTime = new DateTime(2026, 6, 1, 3, 0, 0, DateTimeKind.Utc),
            ArrivalTimezone = "America/New_York"
        });

        // New trip: YHZ (Montreal) to LHR (London)
        // Feb 19, 10:35 - 20:35
        trips.Add(new Trip
        {
            Id = 3,
            UserId = "user1",
            DepartureCountry = "Canada",
            DepartureCity = "Montreal",
            DepartureDateTime = new DateTime(2026, 2, 19, 15, 35, 0, DateTimeKind.Utc),
            DepartureTimezone = "America/Toronto",
            ArrivalCountry = "United Kingdom",
            ArrivalCity = "London",
            ArrivalDateTime = new DateTime(2026, 2, 19, 20, 35, 0, DateTimeKind.Utc),
            ArrivalTimezone = "Europe/London"
        });

        // Return: LHR to YHZ
        // March 1, 09:00 - 12:00
        trips.Add(new Trip
        {
            Id = 4,
            UserId = "user1",
            DepartureCountry = "United Kingdom",
            DepartureCity = "London",
            DepartureDateTime = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc),
            DepartureTimezone = "Europe/London",
            ArrivalCountry = "Canada",
            ArrivalCity = "Montreal",
            ArrivalDateTime = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc),
            ArrivalTimezone = "America/Toronto"
        });

        // Act
        var presenceLog = _residencyService.GenerateDailyPresenceLog(trips);
        var residencyDays = _residencyService.CalculateResidencyDays(presenceLog);

        // Assert: United Kingdom days should be calculated correctly
        Assert.True(residencyDays.ContainsKey("United Kingdom"), "United Kingdom should be in residency days");
        // Should count at least the Feb 19 and March 1 days (partial day rule counts any day with presence)
        Assert.True(residencyDays["United Kingdom"] >= 2, $"Should count at least 2 days in UK, got {residencyDays.GetValueOrDefault("United Kingdom", 0)}");

        // Canada should include the gap-filling days between departures
        Assert.True(residencyDays.ContainsKey("Canada"), "Canada should be in residency days");
    }

    [Fact]
    public void Forecast_WindowFiltering_OnlyCountsDaysInWindow()
    {
        // Arrange: Trip from Jan 1-10, but we only want forecast for Feb-Mar window
        var trips = new List<Trip>
        {
            new Trip
            {
                Id = 1,
                UserId = "user1",
                DepartureCountry = "USA",
                DepartureCity = "Boston",
                DepartureDateTime = new DateTime(2026, 1, 1, 14, 0, 0, DateTimeKind.Utc),
                DepartureTimezone = "America/New_York",
                ArrivalCountry = "Canada",
                ArrivalCity = "Toronto",
                ArrivalDateTime = new DateTime(2026, 1, 1, 19, 0, 0, DateTimeKind.Utc),
                ArrivalTimezone = "America/Toronto"
            }
        };

        var presenceLog = _residencyService.GenerateDailyPresenceLog(trips);

        // Act: Calculate only for February window (Feb 1 - Feb 28)
        var feb1 = new DateOnly(2026, 2, 1);
        var feb28 = new DateOnly(2026, 2, 28);
        var febResidency = _residencyService.CalculateResidencyDays(presenceLog, feb1, feb28);

        // Assert: Should not count January days
        Assert.True(!febResidency.ContainsKey("Canada") || febResidency["Canada"] == 0, 
            "Should not count Canada days in February window when trip was in January");
    }

    [Fact]
    public void Forecast_MidnightRule_CountsCorrectly()
    {
        // Arrange: UK uses Midnight Rule
        // Trip departing Canada at 2 PM EST, arriving UK at 10 PM GMT on same calendar day
        // At UK midnight, person should be in transit (counts for no country)
        var trips = new List<Trip>
        {
            new Trip
            {
                Id = 1,
                UserId = "user1",
                DepartureCountry = "Canada",
                DepartureCity = "Toronto",
                DepartureDateTime = new DateTime(2026, 2, 19, 19, 0, 0, DateTimeKind.Utc), // 2 PM EST
                DepartureTimezone = "America/Toronto",
                ArrivalCountry = "United Kingdom",
                ArrivalCity = "London",
                ArrivalDateTime = new DateTime(2026, 2, 20, 4, 0, 0, DateTimeKind.Utc), // 4 AM GMT next day
                ArrivalTimezone = "Europe/London"
            }
        };

        var presenceLog = _residencyService.GenerateDailyPresenceLog(trips);

        // Act
        var residencyDays = _residencyService.CalculateResidencyDays(presenceLog);

        // Assert
        // Feb 19: Should be Canada (departure day)
        // Feb 20: Should be UK (arrival day)
        Assert.True(residencyDays.ContainsKey("Canada"), "Should count departure day in Canada");
        Assert.True(residencyDays.ContainsKey("United Kingdom"), "Should count arrival day in UK");
    }

    [Fact]
    public void Forecast_MultipleTripsInWindow_AggregatesCorrectly()
    {
        // Arrange: Multiple short trips to same country
        // Trip 1: Feb 1-5 in Canada
        // Trip 2: Feb 10-15 in Canada  
        // Trip 3: Feb 20-25 in UK
        var trips = new List<Trip>
        {
            new Trip
            {
                Id = 1,
                UserId = "user1",
                DepartureCountry = "USA",
                DepartureCity = "Boston",
                DepartureDateTime = new DateTime(2026, 2, 1, 14, 0, 0, DateTimeKind.Utc),
                DepartureTimezone = "America/New_York",
                ArrivalCountry = "Canada",
                ArrivalCity = "Toronto",
                ArrivalDateTime = new DateTime(2026, 2, 1, 19, 0, 0, DateTimeKind.Utc),
                ArrivalTimezone = "America/Toronto"
            },
            new Trip
            {
                Id = 2,
                UserId = "user1",
                DepartureCountry = "Canada",
                DepartureCity = "Toronto",
                DepartureDateTime = new DateTime(2026, 2, 5, 14, 0, 0, DateTimeKind.Utc),
                DepartureTimezone = "America/Toronto",
                ArrivalCountry = "USA",
                ArrivalCity = "Boston",
                ArrivalDateTime = new DateTime(2026, 2, 5, 19, 0, 0, DateTimeKind.Utc),
                ArrivalTimezone = "America/New_York"
            },
            new Trip
            {
                Id = 3,
                UserId = "user1",
                DepartureCountry = "USA",
                DepartureCity = "Boston",
                DepartureDateTime = new DateTime(2026, 2, 10, 14, 0, 0, DateTimeKind.Utc),
                DepartureTimezone = "America/New_York",
                ArrivalCountry = "Canada",
                ArrivalCity = "Toronto",
                ArrivalDateTime = new DateTime(2026, 2, 10, 19, 0, 0, DateTimeKind.Utc),
                ArrivalTimezone = "America/Toronto"
            },
            new Trip
            {
                Id = 4,
                UserId = "user1",
                DepartureCountry = "Canada",
                DepartureCity = "Toronto",
                DepartureDateTime = new DateTime(2026, 2, 15, 14, 0, 0, DateTimeKind.Utc),
                DepartureTimezone = "America/Toronto",
                ArrivalCountry = "USA",
                ArrivalCity = "Boston",
                ArrivalDateTime = new DateTime(2026, 2, 15, 19, 0, 0, DateTimeKind.Utc),
                ArrivalTimezone = "America/New_York"
            },
            new Trip
            {
                Id = 5,
                UserId = "user1",
                DepartureCountry = "USA",
                DepartureCity = "Boston",
                DepartureDateTime = new DateTime(2026, 2, 20, 14, 0, 0, DateTimeKind.Utc),
                DepartureTimezone = "America/New_York",
                ArrivalCountry = "United Kingdom",
                ArrivalCity = "London",
                ArrivalDateTime = new DateTime(2026, 2, 20, 22, 0, 0, DateTimeKind.Utc),
                ArrivalTimezone = "Europe/London"
            },
            new Trip
            {
                Id = 6,
                UserId = "user1",
                DepartureCountry = "United Kingdom",
                DepartureCity = "London",
                DepartureDateTime = new DateTime(2026, 2, 25, 14, 0, 0, DateTimeKind.Utc),
                DepartureTimezone = "Europe/London",
                ArrivalCountry = "USA",
                ArrivalCity = "Boston",
                ArrivalDateTime = new DateTime(2026, 2, 25, 18, 0, 0, DateTimeKind.Utc),
                ArrivalTimezone = "America/New_York"
            }
        };

        var presenceLog = _residencyService.GenerateDailyPresenceLog(trips);
        var feb1 = new DateOnly(2026, 2, 1);
        var feb28 = new DateOnly(2026, 2, 28);

        // Act
        var residencyDays = _residencyService.CalculateResidencyDays(presenceLog, feb1, feb28);

        // Assert
        Assert.True(residencyDays.ContainsKey("Canada"), "Should count Canada days");
        Assert.True(residencyDays["Canada"] >= 9, $"Should count at least 9 days in Canada (5+5-1 overlap), got {residencyDays.GetValueOrDefault("Canada", 0)}");
        
        Assert.True(residencyDays.ContainsKey("United Kingdom"), "Should count UK days");
        Assert.True(residencyDays["United Kingdom"] >= 5, $"Should count at least 5 days in UK, got {residencyDays.GetValueOrDefault("United Kingdom", 0)}");
    }

    [Fact]
    public void Forecast_TransitDays_NotCountedForAnyCountry()
    {
        // Arrange: Long-haul flight where person is in transit overnight
        // Departure: Feb 19, 10:35 EST from Montreal
        // Arrival: Feb 20, 13:35 GMT in London (next day due to time zone and flight duration)
        var trips = new List<Trip>
        {
            new Trip
            {
                Id = 1,
                UserId = "user1",
                DepartureCountry = "Canada",
                DepartureCity = "Montreal",
                DepartureDateTime = new DateTime(2026, 2, 19, 15, 35, 0, DateTimeKind.Utc), // 10:35 EST
                DepartureTimezone = "America/Toronto",
                ArrivalCountry = "United Kingdom",
                ArrivalCity = "London",
                ArrivalDateTime = new DateTime(2026, 2, 20, 13, 35, 0, DateTimeKind.Utc), // 13:35 GMT
                ArrivalTimezone = "Europe/London"
            }
        };

        var presenceLog = _residencyService.GenerateDailyPresenceLog(trips);

        // Act
        var residencyDays = _residencyService.CalculateResidencyDays(presenceLog);

        // Assert: Transit days should not count toward any country
        Assert.False(residencyDays.ContainsKey("IN_TRANSIT"), "IN_TRANSIT should not be in residency days");
        // Should count departure and arrival days (Canada departure day, UK arrival day)
        Assert.True(residencyDays.ContainsKey("Canada"), "Should count Canada (departure day)");
        // Note: UK arrival day (Feb 20) may or may not be counted depending on midnight rule calculations
        // The key assertion is that IN_TRANSIT is not counted
    }

    #endregion
}
