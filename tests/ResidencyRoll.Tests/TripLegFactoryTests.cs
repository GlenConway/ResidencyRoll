using System;
using ResidencyRoll.Shared.Trips;
using ResidencyRoll.Web.Helpers;
using Xunit;

namespace ResidencyRoll.Tests;

public class TripLegFactoryTests
{
    [Fact]
    public void CreatesNextLeg_UsesArrivalAsNewDeparture()
    {
        var baseTrip = new TripDto
        {
            ArrivalCountry = "Canada",
            ArrivalCity = "Toronto",
            ArrivalIataCode = "YYZ",
            ArrivalTimezone = "America/Toronto",
            ArrivalDateTime = new DateTime(2024, 8, 1, 14, 0, 0)
        };

        var next = TripLegFactory.CreateNextLeg(baseTrip, layover: TimeSpan.FromHours(2), defaultDuration: TimeSpan.FromHours(3));

        Assert.Equal("Canada", next.DepartureCountry);
        Assert.Equal("Toronto", next.DepartureCity);
        Assert.Equal("YYZ", next.DepartureIataCode);
        Assert.Equal(new DateTime(2024, 8, 1, 16, 0, 0), next.DepartureDateTime); // +2h layover
        Assert.Equal(new DateTime(2024, 8, 1, 19, 0, 0), next.ArrivalDateTime);   // +3h duration
        Assert.Equal("America/Toronto", next.DepartureTimezone);
        Assert.Equal("America/Toronto", next.ArrivalTimezone);
    }

    [Fact]
    public void CreatesNextLeg_BlanksArrivalFields()
    {
        var baseTrip = new TripDto
        {
            ArrivalCountry = "France",
            ArrivalCity = "Paris",
            ArrivalIataCode = "CDG",
            ArrivalTimezone = "Europe/Paris",
            ArrivalDateTime = new DateTime(2024, 9, 10, 9, 30, 0)
        };

        var next = TripLegFactory.CreateNextLeg(baseTrip);

        Assert.Equal(string.Empty, next.ArrivalCountry);
        Assert.Equal(string.Empty, next.ArrivalCity);
        Assert.Null(next.ArrivalIataCode);
    }

    [Fact]
    public void CreatesNextLeg_FallsBackToUtcWhenArrivalTimezoneMissing()
    {
        var baseTrip = new TripDto
        {
            ArrivalCountry = "USA",
            ArrivalCity = "Chicago",
            ArrivalIataCode = "ORD",
            ArrivalTimezone = "",
            ArrivalDateTime = new DateTime(2024, 10, 5, 6, 0, 0)
        };

        var next = TripLegFactory.CreateNextLeg(baseTrip, layover: TimeSpan.FromHours(1));

        Assert.Equal("UTC", next.DepartureTimezone);
        Assert.Equal(new DateTime(2024, 10, 5, 7, 0, 0), next.DepartureDateTime);
    }
}
