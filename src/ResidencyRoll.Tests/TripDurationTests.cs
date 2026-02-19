using System;
using ResidencyRoll.Web.Models;
using Xunit;

namespace ResidencyRoll.Tests;

public class TripDurationTests
{
    [Fact]
    public void DurationDays_MatchesExcelStyleDayCount_ForCrossMonthTrip()
    {
        var trip = new Trip
        {
            StartDate = new DateTime(2025, 2, 7),
            EndDate = new DateTime(2025, 3, 30)
        };

        var expectedDays = 51; // Midnight rule: arrival counts, departure does not

        Assert.Equal(expectedDays, trip.DurationDays);
    }

    [Fact]
    public void DurationDays_ReturnsZero_WhenEndBeforeStart()
    {
        var trip = new Trip
        {
            StartDate = new DateTime(2025, 3, 30),
            EndDate = new DateTime(2025, 2, 7)
        };

        Assert.Equal(0, trip.DurationDays);
    }

    [Fact]
    public void DurationDays_CountsSingleNight_Properly()
    {
        var trip = new Trip
        {
            StartDate = new DateTime(2025, 2, 7),
            EndDate = new DateTime(2025, 2, 8)
        };

        Assert.Equal(1, trip.DurationDays);
    }
}
