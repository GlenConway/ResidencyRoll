using System;
using System.Collections.Generic;
using System.Linq;
using ResidencyRoll.Api.Models;
using ResidencyRoll.Api.Services;
using Xunit;
using Xunit.Abstractions;

namespace ResidencyRoll.Tests;

/// <summary>
/// Test for exact user scenario to validate midnight rule calculations.
/// </summary>
public class UserScenarioTest
{
    private readonly ITestOutputHelper _output;
    private readonly ResidencyCalculationService _service;

    public UserScenarioTest(ITestOutputHelper output)
    {
        _output = output;
        _service = new ResidencyCalculationService();
    }

    [Fact]
    public void UserScenario_SydneyAucklandTrip_MidnightRuleExplained()
    {
        // User's actual scenario:
        // - Dec 25 - Jan 15: In Sydney, Australia
        // - Jan 6, 11:45am Sydney → Jan 6, 4:50pm Auckland
        // - Jan 9, 2:10pm Auckland → Jan 9, 3:55pm Sydney
        
        _output.WriteLine("Timezone Information:");
        _output.WriteLine("Sydney (AEDT): UTC+11");
        _output.WriteLine("Auckland (NZDT): UTC+13");
        _output.WriteLine("");
        
        _output.WriteLine("Trip Times:");
        _output.WriteLine("Dep Sydney: Jan 6, 11:45am AEDT = Jan 6, 00:45 UTC");
        _output.WriteLine("Arr Auckland: Jan 6, 4:50pm NZDT = Jan 6, 03:50 UTC");
        _output.WriteLine("Dep Auckland: Jan 9, 2:10pm NZDT = Jan 9, 01:10 UTC");
        _output.WriteLine("Arr Sydney: Jan 9, 3:55pm AEDT = Jan 9, 04:55 UTC");
        _output.WriteLine("");
        
        var trips = new List<Trip>
        {
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
                ArrivalDateTime = new DateTime(2025, 12, 25, 20, 0, 0, DateTimeKind.Utc),
                ArrivalTimezone = "Australia/Sydney"
            },
            new Trip
            {
                Id = 2,
                UserId = "user1",
                DepartureCountry = "Australia",
                DepartureCity = "Sydney",
                DepartureDateTime = new DateTime(2026, 1, 6, 0, 45, 0, DateTimeKind.Utc), // Jan 6, 11:45 AM AEDT
                DepartureTimezone = "Australia/Sydney",
                ArrivalCountry = "New Zealand",
                ArrivalCity = "Auckland",
                ArrivalDateTime = new DateTime(2026, 1, 6, 3, 50, 0, DateTimeKind.Utc), // Jan 6, 4:50 PM NZDT
                ArrivalTimezone = "Pacific/Auckland"
            },
            new Trip
            {
                Id = 3,
                UserId = "user1",
                DepartureCountry = "New Zealand",
                DepartureCity = "Auckland",
                DepartureDateTime = new DateTime(2026, 1, 9, 1, 10, 0, DateTimeKind.Utc), // Jan 9, 2:10 PM NZDT
                DepartureTimezone = "Pacific/Auckland",
                ArrivalCountry = "Australia",
                ArrivalCity = "Sydney",
                ArrivalDateTime = new DateTime(2026, 1, 9, 4, 55, 0, DateTimeKind.Utc), // Jan 9, 3:55 PM AEDT
                ArrivalTimezone = "Australia/Sydney"
            },
            new Trip
            {
                Id = 4,
                UserId = "user1",
                DepartureCountry = "Australia",
                DepartureCity = "Sydney",
                DepartureDateTime = new DateTime(2026, 1, 14, 23, 0, 0, DateTimeKind.Utc),
                DepartureTimezone = "Australia/Sydney",
                ArrivalCountry = "Canada",
                ArrivalCity = "Vancouver",
                ArrivalDateTime = new DateTime(2026, 1, 15, 14, 0, 0, DateTimeKind.Utc),
                ArrivalTimezone = "America/Vancouver"
            }
        };

        var dailyPresenceLog = _service.GenerateDailyPresenceLog(trips);

        _output.WriteLine("Midnight Rule Analysis:");
        _output.WriteLine("=======================");
        _output.WriteLine("");
        
        // Focus on Jan 6-9
        for (int day = 6; day <= 9; day++)
        {
            var date = new DateOnly(2026, 1, day);
            var presence = dailyPresenceLog.FirstOrDefault(dp => dp.Date == date);
            
            if (presence != null)
            {
                _output.WriteLine($"Jan {day}:");
                
                // Calculate midnight times
                var sydneyTz = TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");
                var aucklandTz = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");
                
                var midnightSydneyLocal = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Unspecified);
                var midnightSydneyUtc = TimeZoneInfo.ConvertTimeToUtc(midnightSydneyLocal, sydneyTz);
                
                var midnightAucklandLocal = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Unspecified);
                var midnightAucklandUtc = TimeZoneInfo.ConvertTimeToUtc(midnightAucklandLocal, aucklandTz);
                
                _output.WriteLine($"  Midnight Sydney time: {midnightSydneyLocal:yyyy-MM-dd HH:mm} AEDT = {midnightSydneyUtc:yyyy-MM-dd HH:mm} UTC");
                _output.WriteLine($"  Midnight Auckland time: {midnightAucklandLocal:yyyy-MM-dd HH:mm} NZDT = {midnightAucklandUtc:yyyy-MM-dd HH:mm} UTC");
                _output.WriteLine($"  Result: {presence.LocationAtMidnight}");
                _output.WriteLine($"  Reason: At midnight in the relevant timezone, person was physically in {presence.LocationAtMidnight}");
                _output.WriteLine("");
            }
            else
            {
                _output.WriteLine($"Jan {day}: MISSING FROM LOG");
                _output.WriteLine("");
            }
        }
        
        _output.WriteLine("");
        _output.WriteLine("Expected per MIDNIGHT RULE:");
        _output.WriteLine("Jan 6: Australia (at midnight, still in Sydney - departed later at 11:45am)");
        _output.WriteLine("Jan 7: New Zealand (at midnight, in Auckland - arrived previous day)");
        _output.WriteLine("Jan 8: New Zealand (at midnight, in Auckland)");
        _output.WriteLine("Jan 9: New Zealand (at midnight, still in Auckland - departed later at 2:10pm)");
        _output.WriteLine("");
        _output.WriteLine("Note: The midnight rule determines residency based on location at 00:00 local time.");
        _output.WriteLine("If you want Jan 9 to count as Australia, you'd need a different rule (like partial day rule).");
        
        // Assertions
        var jan6 = dailyPresenceLog.FirstOrDefault(dp => dp.Date == new DateOnly(2026, 1, 6));
        var jan7 = dailyPresenceLog.FirstOrDefault(dp => dp.Date == new DateOnly(2026, 1, 7));
        var jan8 = dailyPresenceLog.FirstOrDefault(dp => dp.Date == new DateOnly(2026, 1, 8));
        var jan9 = dailyPresenceLog.FirstOrDefault(dp => dp.Date == new DateOnly(2026, 1, 9));
        
        Assert.NotNull(jan6);
        Assert.NotNull(jan7);
        Assert.NotNull(jan8);
        Assert.NotNull(jan9);
        
        Assert.Equal("Australia", jan6.LocationAtMidnight);
        Assert.Equal("New Zealand", jan7.LocationAtMidnight);
        Assert.Equal("New Zealand", jan8.LocationAtMidnight);
        Assert.Equal("New Zealand", jan9.LocationAtMidnight); // Correct per midnight rule
    }
}
