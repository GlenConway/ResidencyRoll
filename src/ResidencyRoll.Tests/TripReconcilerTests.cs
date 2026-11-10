using ResidencyRoll.Api.Models;
using ResidencyRoll.Api.Services;
using Xunit;

namespace ResidencyRoll.Tests;

public class TripReconcilerTests
{
    [Fact]
    public void Compare_SameAirportsAndTimes_ShouldMatch()
    {
        var saved = Leg(4, "YHZ", "LHR", new DateTime(2024, 2, 1, 8, 0, 0), new DateTime(2024, 2, 1, 18, 0, 0));
        var incoming = Leg(0, "YHZ", "LHR", saved.DepartureDateTime, saved.ArrivalDateTime);

        var report = TripReconciler.Compare([incoming], [saved]);

        Assert.Equal(1, report.Matched);
        Assert.Empty(report.Missing);
        Assert.Empty(report.Different);
        Assert.Empty(report.Extra);
    }

    [Fact]
    public void Compare_SameDayDifferentTime_ShouldReportTheTime()
    {
        var saved = Leg(4, "YHZ", "LHR", new DateTime(2024, 2, 1, 8, 0, 0), new DateTime(2024, 2, 1, 18, 0, 0));
        var incoming = Leg(0, "YHZ", "LHR", new DateTime(2024, 2, 1, 9, 30, 0), new DateTime(2024, 2, 1, 19, 0, 0));

        var report = TripReconciler.Compare([incoming], [saved]);

        var difference = Assert.Single(report.Different);
        Assert.Contains("departureDateTime", difference.Fields);
        Assert.Contains("arrivalDateTime", difference.Fields);
        Assert.Equal(4, difference.Saved.Id);
    }

    [Fact]
    public void Compare_BookingRefOnAnotherDay_ShouldReportTheReschedule()
    {
        var saved = Leg(9, "OOL", "CBR", new DateTime(2024, 5, 1, 10, 0, 0), new DateTime(2024, 5, 1, 12, 0, 0), "OLD");
        var incoming = Leg(0, "OOL", "CBR", new DateTime(2024, 5, 3, 10, 0, 0), new DateTime(2024, 5, 3, 12, 0, 0), "old");
        var extra = Leg(10, "CMN", "LHR", new DateTime(2024, 6, 1, 8, 0, 0), new DateTime(2024, 6, 1, 12, 0, 0));

        var report = TripReconciler.Compare([incoming], [saved, extra]);

        var difference = Assert.Single(report.Different);
        Assert.Equal(9, difference.Saved.Id);
        Assert.Contains("departureDateTime", difference.Fields);
        Assert.Equal(10, Assert.Single(report.Extra).Id);
        Assert.Empty(report.Missing);
    }

    [Fact]
    public void Compare_UnknownLeg_ShouldBeMissing()
    {
        var saved = Leg(1, "YHZ", "LHR", new DateTime(2024, 1, 1, 8, 0, 0), new DateTime(2024, 1, 1, 18, 0, 0));
        var incoming = Leg(0, "YUL", "CDG", new DateTime(2024, 1, 2, 9, 0, 0), new DateTime(2024, 1, 2, 21, 0, 0));

        var report = TripReconciler.Compare([incoming], [saved]);

        Assert.Equal("YUL", Assert.Single(report.Missing).DepartureIataCode);
        Assert.Equal(1, Assert.Single(report.Extra).Id);
    }

    private static Trip Leg(int id, string from, string to, DateTime departure, DateTime arrival, string? bookingRef = null) => new()
    {
        Id = id,
        DepartureIataCode = from,
        ArrivalIataCode = to,
        DepartureDateTime = departure,
        ArrivalDateTime = arrival,
        BookingRef = bookingRef
    };
}
