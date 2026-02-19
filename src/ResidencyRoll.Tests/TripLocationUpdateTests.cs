using System;
using ResidencyRoll.Api.Models;
using ResidencyRoll.Shared.Trips;
using Xunit;

namespace ResidencyRoll.Tests;

/// <summary>
/// Tests for changing departure and arrival locations in trips.
/// This test suite ensures that when a trip's departure or arrival airport
/// is updated, all related fields (city, country, timezone, and IATA code) are
/// properly persisted.
/// </summary>
public class TripLocationUpdateTests
{
    [Fact]
    public void Trip_CanUpdateArrivalAirportCode()
    {
        // Arrange
        var trip = new Trip
        {
            Id = 1,
            UserId = "user123",
            DepartureCountry = "Canada",
            DepartureCity = "Halifax",
            DepartureDateTime = new DateTime(2025, 1, 15, 14, 0, 0),
            DepartureTimezone = "America/Halifax",
            DepartureIataCode = "YHZ",
            ArrivalCountry = "United Kingdom",
            ArrivalCity = "London",
            ArrivalDateTime = new DateTime(2025, 1, 16, 8, 30, 0),
            ArrivalTimezone = "Europe/London",
            ArrivalIataCode = "LHR"
        };

        // Act - Change arrival airport from LHR to YYZ (Toronto)
        trip.ArrivalCountry = "Canada";
        trip.ArrivalCity = "Toronto";
        trip.ArrivalIataCode = "YYZ";
        trip.ArrivalTimezone = "America/Toronto";
        trip.ArrivalDateTime = new DateTime(2025, 1, 15, 13, 0, 0);

        // Assert
        Assert.Equal("Canada", trip.ArrivalCountry);
        Assert.Equal("Toronto", trip.ArrivalCity);
        Assert.Equal("YYZ", trip.ArrivalIataCode);
        Assert.Equal("America/Toronto", trip.ArrivalTimezone);
    }

    [Fact]
    public void Trip_CanUpdateDepartureAirportCode()
    {
        // Arrange
        var trip = new Trip
        {
            Id = 1,
            UserId = "user123",
            DepartureCountry = "Canada",
            DepartureCity = "Halifax",
            DepartureDateTime = new DateTime(2025, 1, 15, 14, 0, 0),
            DepartureTimezone = "America/Halifax",
            DepartureIataCode = "YHZ",
            ArrivalCountry = "United Kingdom",
            ArrivalCity = "London",
            ArrivalDateTime = new DateTime(2025, 1, 16, 8, 30, 0),
            ArrivalTimezone = "Europe/London",
            ArrivalIataCode = "LHR"
        };

        // Act - Change departure airport from YHZ to YYZ (Toronto)
        trip.DepartureCountry = "Canada";
        trip.DepartureCity = "Toronto";
        trip.DepartureIataCode = "YYZ";
        trip.DepartureTimezone = "America/Toronto";
        trip.DepartureDateTime = new DateTime(2025, 1, 15, 13, 0, 0);

        // Assert
        Assert.Equal("Canada", trip.DepartureCountry);
        Assert.Equal("Toronto", trip.DepartureCity);
        Assert.Equal("YYZ", trip.DepartureIataCode);
        Assert.Equal("America/Toronto", trip.DepartureTimezone);
    }

    [Fact]
    public void Trip_CanUpdateBothDepartureAndArrivalLocations()
    {
        // Arrange
        var trip = new Trip
        {
            Id = 1,
            UserId = "user123",
            DepartureCountry = "Canada",
            DepartureCity = "Halifax",
            DepartureDateTime = new DateTime(2025, 1, 15, 14, 0, 0),
            DepartureTimezone = "America/Halifax",
            DepartureIataCode = "YHZ",
            ArrivalCountry = "United Kingdom",
            ArrivalCity = "London",
            ArrivalDateTime = new DateTime(2025, 1, 16, 8, 30, 0),
            ArrivalTimezone = "Europe/London",
            ArrivalIataCode = "LHR"
        };

        // Act - Change both locations: Halifax to Toronto departure, London to Sydney arrival
        trip.DepartureCountry = "Canada";
        trip.DepartureCity = "Toronto";
        trip.DepartureIataCode = "YYZ";
        trip.DepartureTimezone = "America/Toronto";
        trip.DepartureDateTime = new DateTime(2025, 1, 15, 10, 0, 0);

        trip.ArrivalCountry = "Australia";
        trip.ArrivalCity = "Sydney";
        trip.ArrivalIataCode = "SYD";
        trip.ArrivalTimezone = "Australia/Sydney";
        trip.ArrivalDateTime = new DateTime(2025, 1, 17, 15, 30, 0);

        // Assert
        Assert.Equal("Canada", trip.DepartureCountry);
        Assert.Equal("Toronto", trip.DepartureCity);
        Assert.Equal("YYZ", trip.DepartureIataCode);
        Assert.Equal("Australia", trip.ArrivalCountry);
        Assert.Equal("Sydney", trip.ArrivalCity);
        Assert.Equal("SYD", trip.ArrivalIataCode);
    }

    [Fact]
    public void TripDto_CanUpdateArrivalAirportCode()
    {
        // Arrange
        var dto = new TripDto
        {
            Id = 1,
            DepartureCountry = "Canada",
            DepartureCity = "Halifax",
            DepartureDateTime = new DateTime(2025, 1, 15, 14, 0, 0),
            DepartureTimezone = "America/Halifax",
            DepartureIataCode = "YHZ",
            ArrivalCountry = "United Kingdom",
            ArrivalCity = "London",
            ArrivalDateTime = new DateTime(2025, 1, 16, 8, 30, 0),
            ArrivalTimezone = "Europe/London",
            ArrivalIataCode = "LHR"
        };

        // Act - Change arrival airport from LHR to YYZ (Toronto)
        dto.ArrivalCountry = "Canada";
        dto.ArrivalCity = "Toronto";
        dto.ArrivalIataCode = "YYZ";
        dto.ArrivalTimezone = "America/Toronto";
        dto.ArrivalDateTime = new DateTime(2025, 1, 15, 13, 0, 0);

        // Assert
        Assert.Equal("Canada", dto.ArrivalCountry);
        Assert.Equal("Toronto", dto.ArrivalCity);
        Assert.Equal("YYZ", dto.ArrivalIataCode);
        Assert.Equal("America/Toronto", dto.ArrivalTimezone);
    }

    [Fact]
    public void TripDto_CanUpdateDepartureAirportCode()
    {
        // Arrange
        var dto = new TripDto
        {
            Id = 1,
            DepartureCountry = "Canada",
            DepartureCity = "Halifax",
            DepartureDateTime = new DateTime(2025, 1, 15, 14, 0, 0),
            DepartureTimezone = "America/Halifax",
            DepartureIataCode = "YHZ",
            ArrivalCountry = "United Kingdom",
            ArrivalCity = "London",
            ArrivalDateTime = new DateTime(2025, 1, 16, 8, 30, 0),
            ArrivalTimezone = "Europe/London",
            ArrivalIataCode = "LHR"
        };

        // Act - Change departure airport from YHZ to YYZ (Toronto)
        dto.DepartureCountry = "Canada";
        dto.DepartureCity = "Toronto";
        dto.DepartureIataCode = "YYZ";
        dto.DepartureTimezone = "America/Toronto";
        dto.DepartureDateTime = new DateTime(2025, 1, 15, 13, 0, 0);

        // Assert
        Assert.Equal("Canada", dto.DepartureCountry);
        Assert.Equal("Toronto", dto.DepartureCity);
        Assert.Equal("YYZ", dto.DepartureIataCode);
        Assert.Equal("America/Toronto", dto.DepartureTimezone);
    }

    [Fact]
    public void TripDto_CanClearAirportCode()
    {
        // Arrange
        var dto = new TripDto
        {
            Id = 1,
            DepartureCountry = "Canada",
            DepartureCity = "Halifax",
            DepartureDateTime = new DateTime(2025, 1, 15, 14, 0, 0),
            DepartureTimezone = "America/Halifax",
            DepartureIataCode = "YHZ",
            ArrivalCountry = "United Kingdom",
            ArrivalCity = "London",
            ArrivalDateTime = new DateTime(2025, 1, 16, 8, 30, 0),
            ArrivalTimezone = "Europe/London",
            ArrivalIataCode = "LHR"
        };

        // Act - Clear the arrival IATA code
        dto.ArrivalIataCode = null;

        // Assert
        Assert.Null(dto.ArrivalIataCode);
        Assert.Equal("London", dto.ArrivalCity); // City still preserved
    }

    [Fact]
    public void Trip_UpdatesAllLocationFieldsIndependently()
    {
        // Arrange
        var trip = new Trip
        {
            Id = 1,
            UserId = "user123",
            DepartureCountry = "Canada",
            DepartureCity = "Halifax",
            DepartureDateTime = new DateTime(2025, 1, 15, 14, 0, 0),
            DepartureTimezone = "America/Halifax",
            DepartureIataCode = "YHZ",
            ArrivalCountry = "United Kingdom",
            ArrivalCity = "London",
            ArrivalDateTime = new DateTime(2025, 1, 16, 8, 30, 0),
            ArrivalTimezone = "Europe/London",
            ArrivalIataCode = "LHR"
        };

        // Act - Update only the city, keeping country and IATA code
        trip.ArrivalCity = "Manchester";
        
        // Assert
        Assert.Equal("United Kingdom", trip.ArrivalCountry); // Country unchanged
        Assert.Equal("Manchester", trip.ArrivalCity); // City changed
        Assert.Equal("LHR", trip.ArrivalIataCode); // IATA code unchanged

        // Act - Update IATA code as well
        trip.ArrivalIataCode = "MAN";
        
        // Assert
        Assert.Equal("Manchester", trip.ArrivalCity);
        Assert.Equal("MAN", trip.ArrivalIataCode); // Update persists after second change
    }

    [Theory]
    [InlineData("YHZ", "YYZ", "Halifax", "Toronto")]
    [InlineData("LHR", "CDG", "London", "Paris")]
    [InlineData("SYD", "MEL", "Sydney", "Melbourne")]
    [InlineData("JFK", "LAX", "New York", "Los Angeles")]
    public void Trip_CanUpdateToDifferentAirportCodes(
        string originalCode, 
        string newCode, 
        string originalCity, 
        string newCity)
    {
        // Arrange
        var trip = new Trip
        {
            Id = 1,
            UserId = "user123",
            DepartureCountry = "Country1",
            DepartureCity = "City1",
            DepartureDateTime = new DateTime(2025, 1, 15, 14, 0, 0),
            DepartureTimezone = "UTC",
            DepartureIataCode = "AAA",
            ArrivalCountry = "Country2",
            ArrivalCity = originalCity,
            ArrivalDateTime = new DateTime(2025, 1, 16, 8, 30, 0),
            ArrivalTimezone = "UTC",
            ArrivalIataCode = originalCode
        };

        // Act
        trip.ArrivalIataCode = newCode;
        trip.ArrivalCity = newCity;

        // Assert
        Assert.Equal(newCode, trip.ArrivalIataCode);
        Assert.Equal(newCity, trip.ArrivalCity);
        Assert.NotEqual(originalCode, trip.ArrivalIataCode);
        Assert.NotEqual(originalCity, trip.ArrivalCity);
    }
}
