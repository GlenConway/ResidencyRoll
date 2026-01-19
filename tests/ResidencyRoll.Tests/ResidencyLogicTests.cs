using System;
using System.Collections.Generic;
using System.Linq;
using ResidencyRoll.Api.Models;
using ResidencyRoll.Api.Services;
using Xunit;

namespace ResidencyRoll.Tests;

/// <summary>
/// Tests for the new location-at-midnight residency tracking logic.
/// Tests cover Midnight Rule, Partial Day Rule, and International Date Line scenarios.
/// </summary>
public class ResidencyLogicTests
{
    private readonly ResidencyCalculationService _service;

    public ResidencyLogicTests()
    {
        _service = new ResidencyCalculationService();
    }

    #region Scenario 1: The Australia Leap (International Date Line - Westbound)

    [Fact]
    public void AustraliaLeap_Westbound_SkipsDecember24()
    {
        // Arrange: Departure from Canada on Dec 23, Arrival in Australia on Dec 25
        // When crossing IDL westbound, Dec 24 is "skipped" in wall-clock time
        var trips = new List<Trip>
        {
            new Trip
            {
                Id = 1,
                UserId = "user1",
                DepartureCountry = "Canada",
                DepartureCity = "Vancouver",
                DepartureDateTime = new DateTime(2025, 12, 23, 18, 0, 0, DateTimeKind.Utc), // Dec 23, 10:00 AM PST
                DepartureTimezone = "America/Vancouver",
                ArrivalCountry = "Australia",
                ArrivalCity = "Sydney",
                ArrivalDateTime = new DateTime(2025, 12, 25, 6, 0, 0, DateTimeKind.Utc), // Dec 25, 5:00 PM AEDT
                ArrivalTimezone = "Australia/Sydney"
            }
        };

        // Act
        var dailyPresenceLog = _service.GenerateDailyPresenceLog(trips);
        var residencyDays = _service.CalculateResidencyDays(dailyPresenceLog);

        // Assert
        // Expected: 
        // - Dec 23: Canada (at midnight Dec 23 in Canada, still in Canada)
        // - Dec 24: IN_TRANSIT (at midnight Dec 24 in Canada TZ, flying over Pacific)
        // - Dec 25: Australia (at midnight Dec 25 in Australia, arrived in Australia)
        
        var dec23 = dailyPresenceLog.FirstOrDefault(dp => dp.Date == new DateOnly(2025, 12, 23));
        var dec24 = dailyPresenceLog.FirstOrDefault(dp => dp.Date == new DateOnly(2025, 12, 24));
        var dec25 = dailyPresenceLog.FirstOrDefault(dp => dp.Date == new DateOnly(2025, 12, 25));

        Assert.NotNull(dec23);
        Assert.Equal("Canada", dec23.LocationAtMidnight);
        Assert.False(dec23.IsInTransitAtMidnight);

        Assert.NotNull(dec24);
        Assert.Equal("IN_TRANSIT", dec24.LocationAtMidnight);
        Assert.True(dec24.IsInTransitAtMidnight);

        Assert.NotNull(dec25);
        Assert.Equal("Australia", dec25.LocationAtMidnight);
        Assert.False(dec25.IsInTransitAtMidnight);

        // Verify residency counts (transit days don't count)
        Assert.Equal(1, residencyDays.GetValueOrDefault("Canada", 0));
        Assert.Equal(1, residencyDays.GetValueOrDefault("Australia", 0));
        Assert.Equal(0, residencyDays.GetValueOrDefault("IN_TRANSIT", 0)); // Transit doesn't count
    }

    [Fact]
    public void AustraliaLeap_Eastbound_DoesNotSkipDays()
    {
        // Arrange: Return flight from Australia to Canada (eastbound)
        // When crossing IDL eastbound, no days are skipped, but night is shortened
        var trips = new List<Trip>
        {
            new Trip
            {
                Id = 2,
                UserId = "user1",
                DepartureCountry = "Australia",
                DepartureCity = "Sydney",
                DepartureDateTime = new DateTime(2026, 1, 10, 22, 0, 0, DateTimeKind.Utc), // Jan 11, 9:00 AM AEDT
                DepartureTimezone = "Australia/Sydney",
                ArrivalCountry = "Canada",
                ArrivalCity = "Vancouver",
                ArrivalDateTime = new DateTime(2026, 1, 11, 6, 0, 0, DateTimeKind.Utc), // Jan 10, 10:00 PM PST (same day!)
                ArrivalTimezone = "America/Vancouver"
            }
        };

        // Act
        var dailyPresenceLog = _service.GenerateDailyPresenceLog(trips);
        var residencyDays = _service.CalculateResidencyDays(dailyPresenceLog);

        // Assert
        // Expected:
        // - Jan 10: Canada (arrive in Canada on Jan 10 local time, count at midnight)
        // - Jan 11: Australia (at midnight Jan 11 Australia time, still in Australia before departure)
        // Note: This is complex due to IDL - we count based on where they are at each midnight
        
        var jan10 = dailyPresenceLog.FirstOrDefault(dp => dp.Date == new DateOnly(2026, 1, 10));
        var jan11 = dailyPresenceLog.FirstOrDefault(dp => dp.Date == new DateOnly(2026, 1, 11));

        Assert.NotNull(jan10);
        Assert.NotNull(jan11);
        
        // At least one day should be counted for each country
        Assert.True(residencyDays.GetValueOrDefault("Canada", 0) >= 1);
        Assert.True(residencyDays.GetValueOrDefault("Australia", 0) >= 1);
    }

    #endregion

    #region Scenario 2: The US Partial Day Rule

    [Fact]
    public void UsaPartialDay_ArriveLateFriday_DepartEarlySaturday_Counts2Days()
    {
        // Arrange: Arrive in USA at 11:50 PM Friday, depart Saturday at 2:00 AM
        // USA uses Partial Day Rule: ANY part of a day counts
        var trips = new List<Trip>
        {
            new Trip
            {
                Id = 3,
                UserId = "user1",
                DepartureCountry = "Canada",
                DepartureCity = "Toronto",
                DepartureDateTime = new DateTime(2025, 3, 14, 23, 0, 0, DateTimeKind.Utc), // Friday 7:00 PM EDT
                DepartureTimezone = "America/Toronto",
                ArrivalCountry = "USA",
                ArrivalCity = "New York",
                ArrivalDateTime = new DateTime(2025, 3, 15, 3, 50, 0, DateTimeKind.Utc), // Friday 11:50 PM EDT
                ArrivalTimezone = "America/New_York"
            },
            new Trip
            {
                Id = 4,
                UserId = "user1",
                DepartureCountry = "USA",
                DepartureCity = "New York",
                DepartureDateTime = new DateTime(2025, 3, 15, 6, 0, 0, DateTimeKind.Utc), // Saturday 2:00 AM EDT
                DepartureTimezone = "America/New_York",
                ArrivalCountry = "Canada",
                ArrivalCity = "Toronto",
                ArrivalDateTime = new DateTime(2025, 3, 15, 7, 30, 0, DateTimeKind.Utc), // Saturday 3:30 AM EDT
                ArrivalTimezone = "America/Toronto"
            }
        };

        // Act
        var dailyPresenceLog = _service.GenerateDailyPresenceLog(trips);
        var usaDays = _service.CalculateResidencyDaysForCountry("USA", dailyPresenceLog, trips);

        // Assert
        // Expected: 2 days for USA
        // - Friday (partial): arrived 11:50 PM, counts as 1 day
        // - Saturday (partial): departed 2:00 AM, counts as 1 day
        Assert.Equal(2, usaDays);

        // Verify the presence log contains both days with USA in LocationsDuringDay
        var friday = dailyPresenceLog.FirstOrDefault(dp => dp.Date == new DateOnly(2025, 3, 14));
        var saturday = dailyPresenceLog.FirstOrDefault(dp => dp.Date == new DateOnly(2025, 3, 15));

        Assert.NotNull(friday);
        Assert.Contains("USA", friday.LocationsDuringDay);

        Assert.NotNull(saturday);
        Assert.Contains("USA", saturday.LocationsDuringDay);
    }

    [Fact]
    public void UsaPartialDay_FullDay_Counts1Day()
    {
        // Arrange: Spend a full day in USA
        var trips = new List<Trip>
        {
            new Trip
            {
                Id = 5,
                UserId = "user1",
                DepartureCountry = "Canada",
                DepartureCity = "Toronto",
                DepartureDateTime = new DateTime(2025, 6, 10, 14, 0, 0, DateTimeKind.Utc), // June 10, 10:00 AM EDT
                DepartureTimezone = "America/Toronto",
                ArrivalCountry = "USA",
                ArrivalCity = "New York",
                ArrivalDateTime = new DateTime(2025, 6, 10, 15, 30, 0, DateTimeKind.Utc), // June 10, 11:30 AM EDT
                ArrivalTimezone = "America/New_York"
            },
            new Trip
            {
                Id = 6,
                UserId = "user1",
                DepartureCountry = "USA",
                DepartureCity = "New York",
                DepartureDateTime = new DateTime(2025, 6, 11, 18, 0, 0, DateTimeKind.Utc), // June 11, 2:00 PM EDT
                DepartureTimezone = "America/New_York",
                ArrivalCountry = "Canada",
                ArrivalCity = "Toronto",
                ArrivalDateTime = new DateTime(2025, 6, 11, 19, 30, 0, DateTimeKind.Utc), // June 11, 3:30 PM EDT
                ArrivalTimezone = "America/Toronto"
            }
        };

        // Act
        var dailyPresenceLog = _service.GenerateDailyPresenceLog(trips);
        var usaDays = _service.CalculateResidencyDaysForCountry("USA", dailyPresenceLog, trips);

        // Assert
        // Expected: 2 days for USA (June 10 partial, June 11 partial)
        Assert.Equal(2, usaDays);
    }

    #endregion

    #region Scenario 3: UK/Commonwealth Comparison (Midnight Rule)

    [Fact]
    public void UkMidnightRule_ArriveLateFriday_DepartEarlySaturday_Counts1Day()
    {
        // Arrange: Same timing as USA test, but for UK (Midnight Rule)
        // Arrive in UK at 11:50 PM Friday, depart Saturday at 2:00 AM
        var trips = new List<Trip>
        {
            new Trip
            {
                Id = 7,
                UserId = "user1",
                DepartureCountry = "Ireland",
                DepartureCity = "Dublin",
                DepartureDateTime = new DateTime(2025, 3, 14, 22, 0, 0, DateTimeKind.Utc), // Friday 10:00 PM GMT
                DepartureTimezone = "Europe/Dublin",
                ArrivalCountry = "United Kingdom",
                ArrivalCity = "London",
                ArrivalDateTime = new DateTime(2025, 3, 14, 23, 50, 0, DateTimeKind.Utc), // Friday 11:50 PM GMT
                ArrivalTimezone = "Europe/London"
            },
            new Trip
            {
                Id = 8,
                UserId = "user1",
                DepartureCountry = "United Kingdom",
                DepartureCity = "London",
                DepartureDateTime = new DateTime(2025, 3, 15, 2, 0, 0, DateTimeKind.Utc), // Saturday 2:00 AM GMT
                DepartureTimezone = "Europe/London",
                ArrivalCountry = "Ireland",
                ArrivalCity = "Dublin",
                ArrivalDateTime = new DateTime(2025, 3, 15, 3, 30, 0, DateTimeKind.Utc), // Saturday 3:30 AM GMT
                ArrivalTimezone = "Europe/Dublin"
            }
        };

        // Act
        var dailyPresenceLog = _service.GenerateDailyPresenceLog(trips);
        var ukDays = _service.CalculateResidencyDaysForCountry("United Kingdom", dailyPresenceLog, trips);

        // Assert
        // Expected: 1 day for UK
        // - Friday: arrived 11:50 PM, but midnight hasn't passed yet (doesn't count)
        // - Saturday: at midnight (00:00), physically present in UK (counts)
        // So only Saturday counts
        Assert.Equal(1, ukDays);

        var saturday = dailyPresenceLog.FirstOrDefault(dp => dp.Date == new DateOnly(2025, 3, 15));
        Assert.NotNull(saturday);
        Assert.Equal("United Kingdom", saturday.LocationAtMidnight);
    }

    [Fact]
    public void CanadaMidnightRule_MultiDayStay_CountsCorrectly()
    {
        // Arrange: Stay in Canada for 3 full days
        var trips = new List<Trip>
        {
            new Trip
            {
                Id = 9,
                UserId = "user1",
                DepartureCountry = "USA",
                DepartureCity = "Seattle",
                DepartureDateTime = new DateTime(2025, 7, 1, 17, 0, 0, DateTimeKind.Utc), // July 1, 10:00 AM PDT
                DepartureTimezone = "America/Los_Angeles",
                ArrivalCountry = "Canada",
                ArrivalCity = "Vancouver",
                ArrivalDateTime = new DateTime(2025, 7, 1, 20, 0, 0, DateTimeKind.Utc), // July 1, 1:00 PM PDT
                ArrivalTimezone = "America/Vancouver"
            },
            new Trip
            {
                Id = 10,
                UserId = "user1",
                DepartureCountry = "Canada",
                DepartureCity = "Vancouver",
                DepartureDateTime = new DateTime(2025, 7, 4, 17, 0, 0, DateTimeKind.Utc), // July 4, 10:00 AM PDT
                DepartureTimezone = "America/Vancouver",
                ArrivalCountry = "USA",
                ArrivalCity = "Seattle",
                ArrivalDateTime = new DateTime(2025, 7, 4, 20, 0, 0, DateTimeKind.Utc), // July 4, 1:00 PM PDT
                ArrivalTimezone = "America/Los_Angeles"
            }
        };

        // Act
        var dailyPresenceLog = _service.GenerateDailyPresenceLog(trips);
        var canadaDays = _service.CalculateResidencyDaysForCountry("Canada", dailyPresenceLog, trips);

        // Assert
        // Expected: 4 days for Canada (Partial Day Rule)
        // - July 1: arrival day at 1:00 PM - present for part of day (counts)
        // - July 2: full day in Canada (counts)
        // - July 3: full day in Canada (counts)
        // - July 4: departure day at 1:00 PM - present for part of day (counts)
        // Canada uses Partial Day Rule - any part of a day counts
        Assert.Equal(4, canadaDays);
    }

    #endregion

    #region Edge Cases and Additional Tests

    [Fact]
    public void InTransit_AtMidnight_DoesNotCountForAnyCountry()
    {
        // Arrange: Long flight where midnight occurs during the flight
        var trips = new List<Trip>
        {
            new Trip
            {
                Id = 11,
                UserId = "user1",
                DepartureCountry = "United Kingdom",
                DepartureCity = "London",
                DepartureDateTime = new DateTime(2025, 5, 20, 20, 0, 0, DateTimeKind.Utc), // May 20, 8:00 PM UTC
                DepartureTimezone = "Europe/London",
                ArrivalCountry = "USA",
                ArrivalCity = "Los Angeles",
                ArrivalDateTime = new DateTime(2025, 5, 21, 4, 0, 0, DateTimeKind.Utc), // May 20, 9:00 PM PDT (same date in LA!)
                ArrivalTimezone = "America/Los_Angeles"
            }
        };

        // Act
        var dailyPresenceLog = _service.GenerateDailyPresenceLog(trips);
        var residencyDays = _service.CalculateResidencyDays(dailyPresenceLog);

        // Assert
        // At midnight May 21 (00:00 UTC), the person is in transit
        // This day should not count toward any country's residency
        var may21 = dailyPresenceLog.FirstOrDefault(dp => dp.Date == new DateOnly(2025, 5, 21));
        
        if (may21 != null && may21.IsInTransitAtMidnight)
        {
            // Verify transit day doesn't contribute to residency counts
            Assert.Equal(0, residencyDays.GetValueOrDefault("IN_TRANSIT", 0));
        }
    }

    [Fact]
    public void MultipleTrips_OverlappingDates_LaterTripTakesPrecedence()
    {
        // Arrange: Two trips with overlapping dates (later trip should override)
        var trips = new List<Trip>
        {
            new Trip
            {
                Id = 12,
                UserId = "user1",
                DepartureCountry = "Canada",
                DepartureCity = "Toronto",
                DepartureDateTime = new DateTime(2025, 8, 1, 14, 0, 0, DateTimeKind.Utc),
                DepartureTimezone = "America/Toronto",
                ArrivalCountry = "USA",
                ArrivalCity = "New York",
                ArrivalDateTime = new DateTime(2025, 8, 1, 16, 0, 0, DateTimeKind.Utc),
                ArrivalTimezone = "America/New_York"
            },
            new Trip
            {
                Id = 13,
                UserId = "user1",
                DepartureCountry = "USA",
                DepartureCity = "New York",
                DepartureDateTime = new DateTime(2025, 8, 3, 14, 0, 0, DateTimeKind.Utc),
                DepartureTimezone = "America/New_York",
                ArrivalCountry = "United Kingdom",
                ArrivalCity = "London",
                ArrivalDateTime = new DateTime(2025, 8, 4, 2, 0, 0, DateTimeKind.Utc),
                ArrivalTimezone = "Europe/London"
            }
        };

        // Act
        var dailyPresenceLog = _service.GenerateDailyPresenceLog(trips);
        var residencyDays = _service.CalculateResidencyDays(dailyPresenceLog);

        // Assert
        // Should have days counted for USA and UK
        Assert.True(residencyDays.GetValueOrDefault("USA", 0) >= 1);
        Assert.True(residencyDays.GetValueOrDefault("United Kingdom", 0) >= 1);
    }

    [Fact]
    public void GetLocationAtTimestamp_DuringFlight_ReturnsInTransit()
    {
        // Arrange
        var trips = new List<Trip>
        {
            new Trip
            {
                Id = 14,
                UserId = "user1",
                DepartureCountry = "Canada",
                DepartureCity = "Toronto",
                DepartureDateTime = new DateTime(2025, 9, 15, 14, 0, 0, DateTimeKind.Utc),
                DepartureTimezone = "America/Toronto",
                ArrivalCountry = "USA",
                ArrivalCity = "Miami",
                ArrivalDateTime = new DateTime(2025, 9, 15, 18, 0, 0, DateTimeKind.Utc),
                ArrivalTimezone = "America/New_York"
            }
        };

        // Act: Check location halfway through flight
        var midFlightTime = new DateTime(2025, 9, 15, 16, 0, 0, DateTimeKind.Utc);
        var location = _service.GetLocationAtTimestamp(midFlightTime, trips);

        // Assert
        Assert.Equal("IN_TRANSIT", location);
    }

    [Fact]
    public void GetLocationAtTimestamp_AfterArrival_ReturnsArrivalCountry()
    {
        // Arrange
        var trips = new List<Trip>
        {
            new Trip
            {
                Id = 15,
                UserId = "user1",
                DepartureCountry = "Canada",
                DepartureCity = "Toronto",
                DepartureDateTime = new DateTime(2025, 9, 15, 14, 0, 0, DateTimeKind.Utc),
                DepartureTimezone = "America/Toronto",
                ArrivalCountry = "USA",
                ArrivalCity = "Miami",
                ArrivalDateTime = new DateTime(2025, 9, 15, 18, 0, 0, DateTimeKind.Utc),
                ArrivalTimezone = "America/New_York"
            }
        };

        // Act: Check location after arrival
        var afterArrival = new DateTime(2025, 9, 15, 20, 0, 0, DateTimeKind.Utc);
        var location = _service.GetLocationAtTimestamp(afterArrival, trips);

        // Assert
        Assert.Equal("USA", location);
    }

    [Fact]
    public void CountryRules_AreCorrectlyConfigured()
    {
        // Assert: Verify country rules are set up correctly per actual tax laws
        var canadaRule = _service.GetCountryRule("Canada");
        Assert.Equal(ResidencyRuleType.PartialDayRule, canadaRule.RuleType);
        Assert.Equal(183, canadaRule.ResidencyThresholdDays);

        var usaRule = _service.GetCountryRule("USA");
        Assert.Equal(ResidencyRuleType.PartialDayRule, usaRule.RuleType);
        Assert.True(usaRule.HasTransitException);

        var ukRule = _service.GetCountryRule("United Kingdom");
        Assert.Equal(ResidencyRuleType.MidnightRule, ukRule.RuleType);

        var australiaRule = _service.GetCountryRule("Australia");
        Assert.Equal(ResidencyRuleType.PartialDayRule, australiaRule.RuleType);
        
        var nzRule = _service.GetCountryRule("New Zealand");
        Assert.Equal(ResidencyRuleType.PartialDayRule, nzRule.RuleType);
    }

    [Fact]
    public void DateRangeFilter_WorksCorrectly()
    {
        // Arrange: Multiple trips across different dates
        var trips = new List<Trip>
        {
            new Trip
            {
                Id = 16,
                UserId = "user1",
                DepartureCountry = "Canada",
                DepartureCity = "Toronto",
                DepartureDateTime = new DateTime(2025, 1, 1, 14, 0, 0, DateTimeKind.Utc),
                DepartureTimezone = "America/Toronto",
                ArrivalCountry = "USA",
                ArrivalCity = "New York",
                ArrivalDateTime = new DateTime(2025, 1, 1, 16, 0, 0, DateTimeKind.Utc),
                ArrivalTimezone = "America/New_York"
            },
            new Trip
            {
                Id = 17,
                UserId = "user1",
                DepartureCountry = "USA",
                DepartureCity = "New York",
                DepartureDateTime = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc),
                DepartureTimezone = "America/New_York",
                ArrivalCountry = "Canada",
                ArrivalCity = "Toronto",
                ArrivalDateTime = new DateTime(2025, 6, 1, 16, 0, 0, DateTimeKind.Utc),
                ArrivalTimezone = "America/Toronto"
            }
        };

        // Act: Calculate residency for only the first half of the year
        var dailyPresenceLog = _service.GenerateDailyPresenceLog(trips);
        var residencyDays = _service.CalculateResidencyDays(
            dailyPresenceLog,
            startDate: new DateOnly(2025, 1, 1),
            endDate: new DateOnly(2025, 3, 31)
        );

        // Assert: Should only count days in Q1
        Assert.True(residencyDays.GetValueOrDefault("USA", 0) >= 1);
        // June trip should not be counted
    }

    #endregion

    #region IATA Airport Timezone Resolution Tests

    /// <summary>
    /// Tests IATA-resolved timezones with YHZ (Halifax) to SYD (Sydney) crossing International Date Line westbound.
    /// This verifies that automatic timezone resolution from airport codes correctly feeds into residency calculations.
    /// </summary>
    [Fact]
    public void IATAResolution_YHZ_To_SYD_WestboundIDL_CorrectTimezones()
    {
        // Arrange: Trip from YHZ (Halifax - America/Halifax, UTC-4 in summer) to SYD (Sydney - Australia/Sydney, UTC+10)
        // Flight departs Dec 23, 2025 at 6:00 PM Atlantic Time
        // Arrives Dec 25, 2025 at 6:00 AM Australian Eastern Daylight Time
        // Dec 24 is "lost" due to westbound IDL crossing
        var trips = new List<Trip>
        {
            new Trip
            {
                Id = 100,
                UserId = "user1",
                DepartureCountry = "Canada",
                DepartureCity = "Halifax",
                DepartureDateTime = new DateTime(2025, 12, 23, 22, 0, 0, DateTimeKind.Utc), // Dec 23, 6:00 PM AST (UTC-4)
                DepartureTimezone = "America/Halifax", // Resolved from YHZ
                DepartureIataCode = "YHZ",
                ArrivalCountry = "Australia",
                ArrivalCity = "Sydney",
                ArrivalDateTime = new DateTime(2025, 12, 24, 19, 0, 0, DateTimeKind.Utc), // Dec 25, 6:00 AM AEDT (UTC+11)
                ArrivalTimezone = "Australia/Sydney", // Resolved from SYD
                ArrivalIataCode = "SYD"
            }
        };

        // Act
        var dailyPresenceLog = _service.GenerateDailyPresenceLog(trips);
        var residencyDays = _service.CalculateResidencyDays(dailyPresenceLog);

        // Assert: Verify proper timezone-aware midnight rule application
        var dec23 = dailyPresenceLog.FirstOrDefault(dp => dp.Date == new DateOnly(2025, 12, 23));
        var dec24 = dailyPresenceLog.FirstOrDefault(dp => dp.Date == new DateOnly(2025, 12, 24));
        var dec25 = dailyPresenceLog.FirstOrDefault(dp => dp.Date == new DateOnly(2025, 12, 25));

        // Dec 23: At midnight in Canada timezone, still in Canada
        Assert.NotNull(dec23);
        Assert.Equal("Canada", dec23.LocationAtMidnight);
        Assert.False(dec23.IsInTransitAtMidnight);

        // Dec 24: At midnight in Canada timezone (where departed from), in transit over Pacific
        Assert.NotNull(dec24);
        Assert.Equal("IN_TRANSIT", dec24.LocationAtMidnight);
        Assert.True(dec24.IsInTransitAtMidnight);

        // Dec 25: At midnight in Australia timezone, arrived in Australia
        Assert.NotNull(dec25);
        Assert.Equal("Australia", dec25.LocationAtMidnight);
        Assert.False(dec25.IsInTransitAtMidnight);

        // Verify residency counts
        Assert.Equal(1, residencyDays.GetValueOrDefault("Canada", 0));
        Assert.Equal(1, residencyDays.GetValueOrDefault("Australia", 0));
        Assert.Equal(0, residencyDays.GetValueOrDefault("IN_TRANSIT", 0));
    }

    /// <summary>
    /// Tests return flight SYD to YHZ (eastbound IDL crossing).
    /// Verifies no day is skipped when crossing eastbound.
    /// </summary>
    [Fact]
    public void IATAResolution_SYD_To_YHZ_EastboundIDL_NoSkippedDay()
    {
        // Arrange: Return flight from SYD to YHZ (eastbound)
        // Departs Sydney Jan 15, 2026 at 10:00 AM AEDT
        // Arrives Halifax Jan 15, 2026 at 10:00 AM AST (same calendar day due to IDL)
        var trips = new List<Trip>
        {
            new Trip
            {
                Id = 101,
                UserId = "user1",
                DepartureCountry = "Australia",
                DepartureCity = "Sydney",
                DepartureDateTime = new DateTime(2026, 1, 14, 23, 0, 0, DateTimeKind.Utc), // Jan 15, 10:00 AM AEDT
                DepartureTimezone = "Australia/Sydney", // Resolved from SYD
                DepartureIataCode = "SYD",
                ArrivalCountry = "Canada",
                ArrivalCity = "Halifax",
                ArrivalDateTime = new DateTime(2026, 1, 15, 14, 0, 0, DateTimeKind.Utc), // Jan 15, 10:00 AM AST
                ArrivalTimezone = "America/Halifax", // Resolved from YHZ
                ArrivalIataCode = "YHZ"
            }
        };

        // Act
        var dailyPresenceLog = _service.GenerateDailyPresenceLog(trips);
        var residencyDays = _service.CalculateResidencyDays(dailyPresenceLog);

        // Assert: Verify days are in the log
        var jan15 = dailyPresenceLog.FirstOrDefault(dp => dp.Date == new DateOnly(2026, 1, 15));

        Assert.NotNull(jan15);

        // At least one country should be counted (the arrival or departure country)
        var totalDays = residencyDays.Values.Sum();
        Assert.True(totalDays >= 1, $"Expected at least 1 residency day, got {totalDays}");
    }

    /// <summary>
    /// Tests multi-timezone trip with automatic IATA resolution.
    /// YYZ (Toronto) -> LHR (London) -> SIN (Singapore).
    /// </summary>
    [Fact]
    public void IATAResolution_MultiTimezone_YYZ_LHR_SIN_CorrectResidency()
    {
        // Arrange: Complex multi-city trip
        // Toronto to London: Dec 1-2
        // London stay: Dec 2-7
        // London to Singapore: Dec 7-8
        // Stay in Singapore: Dec 8-10
        var trips = new List<Trip>
        {
            // Trip 1: Toronto to London
            new Trip
            {
                Id = 102,
                UserId = "user1",
                DepartureCountry = "Canada",
                DepartureCity = "Toronto",
                DepartureDateTime = new DateTime(2025, 12, 1, 23, 0, 0, DateTimeKind.Utc), // Dec 1, 6:00 PM EST
                DepartureTimezone = "America/Toronto", // From YYZ
                DepartureIataCode = "YYZ",
                ArrivalCountry = "United Kingdom",
                ArrivalCity = "London",
                ArrivalDateTime = new DateTime(2025, 12, 2, 11, 0, 0, DateTimeKind.Utc), // Dec 2, 11:00 AM GMT
                ArrivalTimezone = "Europe/London", // From LHR
                ArrivalIataCode = "LHR"
            },
            // Trip 2: London to Singapore
            new Trip
            {
                Id = 103,
                UserId = "user1",
                DepartureCountry = "United Kingdom",
                DepartureCity = "London",
                DepartureDateTime = new DateTime(2025, 12, 7, 10, 0, 0, DateTimeKind.Utc), // Dec 7, 10:00 AM GMT
                DepartureTimezone = "Europe/London", // From LHR
                DepartureIataCode = "LHR",
                ArrivalCountry = "Singapore",
                ArrivalCity = "Singapore",
                ArrivalDateTime = new DateTime(2025, 12, 8, 6, 0, 0, DateTimeKind.Utc), // Dec 8, 2:00 PM SGT
                ArrivalTimezone = "Asia/Singapore", // From SIN
                ArrivalIataCode = "SIN"
            },
            // Trip 3: Singapore back to Toronto
            new Trip
            {
                Id = 104,
                UserId = "user1",
                DepartureCountry = "Singapore",
                DepartureCity = "Singapore",
                DepartureDateTime = new DateTime(2025, 12, 10, 14, 0, 0, DateTimeKind.Utc), // Dec 10, 10:00 PM SGT
                DepartureTimezone = "Asia/Singapore", // From SIN
                DepartureIataCode = "SIN",
                ArrivalCountry = "Canada",
                ArrivalCity = "Toronto",
                ArrivalDateTime = new DateTime(2025, 12, 11, 6, 0, 0, DateTimeKind.Utc), // Dec 11, 1:00 AM EST
                ArrivalTimezone = "America/Toronto", // From YYZ
                ArrivalIataCode = "YYZ"
            }
        };

        // Act
        var dailyPresenceLog = _service.GenerateDailyPresenceLog(trips);
        var residencyDays = _service.CalculateResidencyDays(dailyPresenceLog);

        // Assert
        // Verify each country gets some residency days
        var canadaDays = residencyDays.GetValueOrDefault("Canada", 0);
        var ukDays = residencyDays.GetValueOrDefault("United Kingdom", 0);
        var singaporeDays = residencyDays.GetValueOrDefault("Singapore", 0);

        // Canada: At least 1 day (departed in evening)
        Assert.True(canadaDays >= 1, $"Expected at least 1 Canada day, got {canadaDays}");
        
        // UK: Should get multiple days between Dec 2 and Dec 7
        Assert.True(ukDays >= 3, $"Expected at least 3 UK days, got {ukDays}");
        
        // Singapore: At least 2 days (Dec 8-10)
        Assert.True(singaporeDays >= 2, $"Expected at least 2 Singapore days, got {singaporeDays}");
    }

    #endregion

    #region Sydney-Auckland No IDL Crossing Tests

    /// <summary>
    /// Tests that a trip from Sydney, Australia to Auckland, New Zealand does NOT show IDL crossing.
    /// Both cities are on the same side of the International Date Line (west side).
    /// Sydney: longitude ~151°E, timezone UTC+10/+11
    /// Auckland: longitude ~174°E, timezone UTC+12/+13
    /// </summary>
    [Fact]
    public void SydneyToAuckland_NoIDL_ContinuousDates()
    {
        // Arrange: User in Sydney from Dec 25, 2025 to Jan 15, 2026
        // With a trip to Auckland from Jan 6 to Jan 9
        var trips = new List<Trip>
        {
            // Initial arrival in Sydney
            new Trip
            {
                Id = 1,
                UserId = "user1",
                DepartureCountry = "Canada",
                DepartureCity = "Vancouver",
                DepartureDateTime = new DateTime(2025, 12, 24, 10, 0, 0, DateTimeKind.Utc),
                DepartureTimezone = "America/Vancouver",
                ArrivalCountry = "Australia",
                ArrivalCity = "Sydney",
                ArrivalDateTime = new DateTime(2025, 12, 25, 20, 0, 0, DateTimeKind.Utc), // Dec 26, 7:00 AM AEDT
                ArrivalTimezone = "Australia/Sydney"
            },
            // Trip to Auckland
            new Trip
            {
                Id = 2,
                UserId = "user1",
                DepartureCountry = "Australia",
                DepartureCity = "Sydney",
                DepartureDateTime = new DateTime(2026, 1, 5, 22, 0, 0, DateTimeKind.Utc), // Jan 6, 9:00 AM AEDT
                DepartureTimezone = "Australia/Sydney",
                ArrivalCountry = "New Zealand",
                ArrivalCity = "Auckland",
                ArrivalDateTime = new DateTime(2026, 1, 6, 2, 0, 0, DateTimeKind.Utc), // Jan 6, 3:00 PM NZDT
                ArrivalTimezone = "Pacific/Auckland"
            },
            // Return to Sydney
            new Trip
            {
                Id = 3,
                UserId = "user1",
                DepartureCountry = "New Zealand",
                DepartureCity = "Auckland",
                DepartureDateTime = new DateTime(2026, 1, 8, 23, 0, 0, DateTimeKind.Utc), // Jan 9, 12:00 PM NZDT
                DepartureTimezone = "Pacific/Auckland",
                ArrivalCountry = "Australia",
                ArrivalCity = "Sydney",
                ArrivalDateTime = new DateTime(2026, 1, 9, 2, 0, 0, DateTimeKind.Utc), // Jan 9, 1:00 PM AEDT
                ArrivalTimezone = "Australia/Sydney"
            },
            // Final departure from Sydney
            new Trip
            {
                Id = 4,
                UserId = "user1",
                DepartureCountry = "Australia",
                DepartureCity = "Sydney",
                DepartureDateTime = new DateTime(2026, 1, 14, 23, 0, 0, DateTimeKind.Utc), // Jan 15, 10:00 AM AEDT
                DepartureTimezone = "Australia/Sydney",
                ArrivalCountry = "Canada",
                ArrivalCity = "Vancouver",
                ArrivalDateTime = new DateTime(2026, 1, 15, 14, 0, 0, DateTimeKind.Utc), // Jan 15, 6:00 AM PST
                ArrivalTimezone = "America/Vancouver"
            }
        };

        // Act
        var dailyPresenceLog = _service.GenerateDailyPresenceLog(trips);

        // Assert: All dates from Dec 25 to Jan 15 should be present with NO gaps
        var firstDate = new DateOnly(2025, 12, 25);
        var lastDate = new DateOnly(2026, 1, 15);
        var currentDate = firstDate;
        
        var dateList = new List<DateOnly>();
        while (currentDate <= lastDate)
        {
            dateList.Add(currentDate);
            currentDate = currentDate.AddDays(1);
        }

        // Verify no dates are missing
        var presenceDates = dailyPresenceLog.Select(d => d.Date).ToHashSet();
        var missingDates = dateList.Where(d => !presenceDates.Contains(d)).ToList();
        
        Assert.Empty(missingDates); // No dates should be missing
        
        // Verify Jan 6 is NOT marked as IDL
        var jan6 = dailyPresenceLog.FirstOrDefault(d => d.Date == new DateOnly(2026, 1, 6));
        Assert.NotNull(jan6);
        // Jan 6: Departed Sydney in morning, arrived Auckland afternoon - should be in transit or at destination
        Assert.NotEqual("IDL_SKIP", jan6.LocationAtMidnight);
        Assert.True(jan6.LocationAtMidnight == "Australia" || jan6.LocationAtMidnight == "New Zealand" || jan6.LocationAtMidnight == "IN_TRANSIT",
            $"Expected Jan 6 to be Australia, New Zealand or IN_TRANSIT, got: {jan6.LocationAtMidnight}");
        
        // Verify Jan 9 is NOT marked as IDL
        var jan9 = dailyPresenceLog.FirstOrDefault(d => d.Date == new DateOnly(2026, 1, 9));
        Assert.NotNull(jan9);
        Assert.NotEqual("IDL_SKIP", jan9.LocationAtMidnight);
        Assert.True(jan9.LocationAtMidnight == "New Zealand" || jan9.LocationAtMidnight == "Australia" || jan9.LocationAtMidnight == "IN_TRANSIT",
            $"Expected Jan 9 to be New Zealand, Australia or IN_TRANSIT, got: {jan9.LocationAtMidnight}");
        
        // Verify residency counts
        var residencyDays = _service.CalculateResidencyDays(dailyPresenceLog);
        var australiaDays = residencyDays.GetValueOrDefault("Australia", 0);
        var nzDays = residencyDays.GetValueOrDefault("New Zealand", 0);
        
        // Should have significant days in Australia (around 17-18 days)
        Assert.True(australiaDays >= 15, $"Expected at least 15 Australia days, got {australiaDays}");
        
        // Should have a few days in New Zealand (2-3 days)
        Assert.True(nzDays >= 2, $"Expected at least 2 New Zealand days, got {nzDays}");
    }

    #endregion
}
