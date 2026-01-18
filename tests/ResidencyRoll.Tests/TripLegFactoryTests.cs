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

    [Fact]
    public void CreatesNextLeg_PreservesAllArrivalAirportData()
    {
        // Simulates the "Save and Add Leg" scenario where all airport data should be preserved
        var baseTrip = new TripDto
        {
            DepartureCountry = "Canada",
            DepartureCity = "Montreal",
            DepartureIataCode = "YUL",
            DepartureTimezone = "America/Toronto",
            DepartureDateTime = new DateTime(2024, 7, 15, 8, 0, 0),
            ArrivalCountry = "United Kingdom",
            ArrivalCity = "London",
            ArrivalIataCode = "LHR",
            ArrivalTimezone = "Europe/London",
            ArrivalDateTime = new DateTime(2024, 7, 15, 20, 30, 0)
        };

        var next = TripLegFactory.CreateNextLeg(baseTrip);

        // Verify all arrival airport data becomes departure data for the next leg
        Assert.Equal("United Kingdom", next.DepartureCountry);
        Assert.Equal("London", next.DepartureCity);
        Assert.Equal("LHR", next.DepartureIataCode);
        Assert.Equal("Europe/London", next.DepartureTimezone);
        
        // Verify times are calculated correctly
        Assert.Equal(new DateTime(2024, 7, 15, 22, 30, 0), next.DepartureDateTime); // +2h default layover
        Assert.Equal(new DateTime(2024, 7, 16, 0, 30, 0), next.ArrivalDateTime);   // +2h default duration
        
        // Verify arrival is still initialized with the same timezone
        Assert.Equal("Europe/London", next.ArrivalTimezone);
    }
}
