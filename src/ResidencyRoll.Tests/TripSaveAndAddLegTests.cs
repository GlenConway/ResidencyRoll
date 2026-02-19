using System;
using ResidencyRoll.Shared.Trips;
using ResidencyRoll.Web.Helpers;
using Xunit;

namespace ResidencyRoll.Tests;

public class TripSaveAndAddLegTests
{
    [Fact]
    public void SaveAndAddLeg_PreservesAllAirportInformation()
    {
        // Simulate editing a trip with full airport information
        var editedTrip = new TripDto
        {
            Id = 42,
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

        // Simulate: User clicks "Save and Add Leg"
        // 1. The trip is saved to the API
        // 2. The trip is fetched back from the API (simulated here - in reality this would go through HTTP)
        var savedTrip = SimulateApiRoundTrip(editedTrip);
        
        // 3. Create the next leg from the saved trip
        var nextLeg = TripLegFactory.CreateNextLeg(savedTrip);

        // Verify that the next leg's departure matches the saved trip's arrival
        Assert.Equal(savedTrip.ArrivalCountry, nextLeg.DepartureCountry);
        Assert.Equal(savedTrip.ArrivalCity, nextLeg.DepartureCity);
        Assert.Equal(savedTrip.ArrivalIataCode, nextLeg.DepartureIataCode);
        Assert.Equal(savedTrip.ArrivalTimezone, nextLeg.DepartureTimezone);
        
        // Specifically test the reported issue: Country should not be empty
        Assert.NotEmpty(nextLeg.DepartureCountry);
        Assert.Equal("United Kingdom", nextLeg.DepartureCountry);
        Assert.Equal("Europe/London", nextLeg.DepartureTimezone);
    }

    [Fact]
    public void SaveAndAddLeg_HandlesMissingArrivalCountry()
    {
        // Test the bug scenario: arrival city set but country is empty
        var editedTrip = new TripDto
        {
            Id = 42,
            DepartureCountry = "Canada",
            DepartureCity = "Montreal",
            DepartureIataCode = "YUL",
            DepartureTimezone = "America/Toronto",
            DepartureDateTime = new DateTime(2024, 7, 15, 8, 0, 0),
            ArrivalCountry = "", // BUG: Country not set
            ArrivalCity = "London",
            ArrivalIataCode = "LHR",
            ArrivalTimezone = "UTC", // BUG: Wrong timezone
            ArrivalDateTime = new DateTime(2024, 7, 15, 20, 30, 0)
        };

        var savedTrip = SimulateApiRoundTrip(editedTrip);
        var nextLeg = TripLegFactory.CreateNextLeg(savedTrip);

        // This test documents the bug: if the arrival country wasn't properly saved,
        // the next leg will also have an empty country
        Assert.Equal("", nextLeg.DepartureCountry); // BUG REPRODUCED
        Assert.Equal("UTC", nextLeg.DepartureTimezone); // BUG REPRODUCED
    }

    private TripDto SimulateApiRoundTrip(TripDto trip)
    {
        // Simulates sending a trip to the API and getting it back
        // In the real app, this goes through HTTP serialization/deserialization
        // and database save/load, but the DTO should be unchanged
        return new TripDto
        {
            Id = trip.Id,
            DepartureCountry = trip.DepartureCountry,
            DepartureCity = trip.DepartureCity,
            DepartureIataCode = trip.DepartureIataCode,
            DepartureTimezone = trip.DepartureTimezone,
            DepartureDateTime = trip.DepartureDateTime,
            ArrivalCountry = trip.ArrivalCountry,
            ArrivalCity = trip.ArrivalCity,
            ArrivalIataCode = trip.ArrivalIataCode,
            ArrivalTimezone = trip.ArrivalTimezone,
            ArrivalDateTime = trip.ArrivalDateTime
        };
    }
}
