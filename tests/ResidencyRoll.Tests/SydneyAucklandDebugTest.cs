using System;
using System.Collections.Generic;
using System.Linq;
using ResidencyRoll.Api.Models;
using ResidencyRoll.Api.Services;
using Xunit;
using Xunit.Abstractions;

namespace ResidencyRoll.Tests;

/// <summary>
/// Debug test for Sydney-Auckland scenario to understand what dates are being returned.
/// </summary>
public class SydneyAucklandDebugTest
{
    private readonly ITestOutputHelper _output;
    private readonly ResidencyCalculationService _service;

    public SydneyAucklandDebugTest(ITestOutputHelper output)
    {
        _output = output;
        _service = new ResidencyCalculationService();
    }

    [Fact]
    public void Debug_SydneyToAuckland_PrintAllDates()
    {
        // Arrange: User in Sydney from Dec 25, 2025 to Jan 15, 2026
        // With a trip to Auckland from Jan 6 to Jan 9
        // ACTUAL USER TIMES:
        // Jan 6, 11:45am Sydney (AEDT = UTC+11) = Jan 6, 00:45 UTC
        // Jan 6, 4:50pm Auckland (NZDT = UTC+13) = Jan 6, 03:50 UTC
        // Jan 9, 2:10pm Auckland (NZDT = UTC+13) = Jan 9, 01:10 UTC
        // Jan 9, 3:55pm Sydney (AEDT = UTC+11) = Jan 9, 04:55 UTC
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
            // Trip to Auckland - ACTUAL USER TIMES
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
            // Return to Sydney - ACTUAL USER TIMES
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

        // Print all dates
        _output.WriteLine("Daily Presence Log:");
        _output.WriteLine("==================");
        foreach (var presence in dailyPresenceLog.OrderBy(d => d.Date))
        {
            _output.WriteLine($"{presence.Date:yyyy-MM-dd} ({presence.Date.DayOfWeek}): {presence.LocationAtMidnight} (InTransit: {presence.IsInTransitAtMidnight})");
        }
        
        _output.WriteLine("");
        _output.WriteLine("Date Gaps:");
        _output.WriteLine("==========");
        
        var sortedDates = dailyPresenceLog.OrderBy(d => d.Date).ToList();
        for (int i = 0; i < sortedDates.Count - 1; i++)
        {
            var current = sortedDates[i];
            var next = sortedDates[i + 1];
            var daysBetween = (next.Date.DayNumber - current.Date.DayNumber);
            
            if (daysBetween > 1)
            {
                _output.WriteLine($"GAP: {daysBetween - 1} days between {current.Date:yyyy-MM-dd} and {next.Date:yyyy-MM-dd}");
                for (int j = 1; j < daysBetween; j++)
                {
                    var missingDate = current.Date.AddDays(j);
                    _output.WriteLine($"  Missing: {missingDate:yyyy-MM-dd} ({missingDate.DayOfWeek})");
                }
            }
        }
    }
}
