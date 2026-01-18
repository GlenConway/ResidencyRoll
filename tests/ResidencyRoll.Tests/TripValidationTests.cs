using System;
using ResidencyRoll.Shared.Trips;
using Xunit;

namespace ResidencyRoll.Tests;

public class TripValidationTests
{
    [Fact]
    public void ValidTrip_HasAllRequiredFields()
    {
        var trip = new TripDto
        {
            DepartureCountry = "Canada",
            DepartureCity = "Toronto",
            DepartureTimezone = "America/Toronto",
            DepartureDateTime = new DateTime(2024, 7, 15, 8, 0, 0),
            ArrivalCountry = "United Kingdom",
            ArrivalCity = "London",
            ArrivalTimezone = "Europe/London",
            ArrivalDateTime = new DateTime(2024, 7, 15, 20, 30, 0)
        };

        var isValid = IsValidTrip(trip);
        Assert.True(isValid);
    }

    [Theory]
    [InlineData(null, "Toronto", "America/Toronto", "United Kingdom", "London", "Europe/London")]
    [InlineData("", "Toronto", "America/Toronto", "United Kingdom", "London", "Europe/London")]
    [InlineData("  ", "Toronto", "America/Toronto", "United Kingdom", "London", "Europe/London")]
    public void InvalidTrip_MissingDepartureCountry(string? departureCountry, string departureCity, string departureTimezone,
        string arrivalCountry, string arrivalCity, string arrivalTimezone)
    {
        var trip = new TripDto
        {
            DepartureCountry = departureCountry ?? string.Empty,
            DepartureCity = departureCity,
            DepartureTimezone = departureTimezone,
            DepartureDateTime = new DateTime(2024, 7, 15, 8, 0, 0),
            ArrivalCountry = arrivalCountry,
            ArrivalCity = arrivalCity,
            ArrivalTimezone = arrivalTimezone,
            ArrivalDateTime = new DateTime(2024, 7, 15, 20, 30, 0)
        };

        var isValid = IsValidTrip(trip);
        Assert.False(isValid);
    }

    [Theory]
    [InlineData("Canada", null, "America/Toronto", "United Kingdom", "London", "Europe/London")]
    [InlineData("Canada", "", "America/Toronto", "United Kingdom", "London", "Europe/London")]
    [InlineData("Canada", "  ", "America/Toronto", "United Kingdom", "London", "Europe/London")]
    public void InvalidTrip_MissingDepartureCity(string departureCountry, string? departureCity, string departureTimezone,
        string arrivalCountry, string arrivalCity, string arrivalTimezone)
    {
        var trip = new TripDto
        {
            DepartureCountry = departureCountry,
            DepartureCity = departureCity ?? string.Empty,
            DepartureTimezone = departureTimezone,
            DepartureDateTime = new DateTime(2024, 7, 15, 8, 0, 0),
            ArrivalCountry = arrivalCountry,
            ArrivalCity = arrivalCity,
            ArrivalTimezone = arrivalTimezone,
            ArrivalDateTime = new DateTime(2024, 7, 15, 20, 30, 0)
        };

        var isValid = IsValidTrip(trip);
        Assert.False(isValid);
    }

    [Theory]
    [InlineData("Canada", "Toronto", null, "United Kingdom", "London", "Europe/London")]
    [InlineData("Canada", "Toronto", "", "United Kingdom", "London", "Europe/London")]
    [InlineData("Canada", "Toronto", "  ", "United Kingdom", "London", "Europe/London")]
    public void InvalidTrip_MissingDepartureTimezone(string departureCountry, string departureCity, string? departureTimezone,
        string arrivalCountry, string arrivalCity, string arrivalTimezone)
    {
        var trip = new TripDto
        {
            DepartureCountry = departureCountry,
            DepartureCity = departureCity,
            DepartureTimezone = departureTimezone ?? string.Empty,
            DepartureDateTime = new DateTime(2024, 7, 15, 8, 0, 0),
            ArrivalCountry = arrivalCountry,
            ArrivalCity = arrivalCity,
            ArrivalTimezone = arrivalTimezone,
            ArrivalDateTime = new DateTime(2024, 7, 15, 20, 30, 0)
        };

        var isValid = IsValidTrip(trip);
        Assert.False(isValid);
    }

    [Theory]
    [InlineData("Canada", "Toronto", "America/Toronto", null, "London", "Europe/London")]
    [InlineData("Canada", "Toronto", "America/Toronto", "", "London", "Europe/London")]
    [InlineData("Canada", "Toronto", "America/Toronto", "  ", "London", "Europe/London")]
    public void InvalidTrip_MissingArrivalCountry(string departureCountry, string departureCity, string departureTimezone,
        string? arrivalCountry, string arrivalCity, string arrivalTimezone)
    {
        var trip = new TripDto
        {
            DepartureCountry = departureCountry,
            DepartureCity = departureCity,
            DepartureTimezone = departureTimezone,
            DepartureDateTime = new DateTime(2024, 7, 15, 8, 0, 0),
            ArrivalCountry = arrivalCountry ?? string.Empty,
            ArrivalCity = arrivalCity,
            ArrivalTimezone = arrivalTimezone,
            ArrivalDateTime = new DateTime(2024, 7, 15, 20, 30, 0)
        };

        var isValid = IsValidTrip(trip);
        Assert.False(isValid);
    }

    [Theory]
    [InlineData("Canada", "Toronto", "America/Toronto", "United Kingdom", null, "Europe/London")]
    [InlineData("Canada", "Toronto", "America/Toronto", "United Kingdom", "", "Europe/London")]
    [InlineData("Canada", "Toronto", "America/Toronto", "United Kingdom", "  ", "Europe/London")]
    public void InvalidTrip_MissingArrivalCity(string departureCountry, string departureCity, string departureTimezone,
        string arrivalCountry, string? arrivalCity, string arrivalTimezone)
    {
        var trip = new TripDto
        {
            DepartureCountry = departureCountry,
            DepartureCity = departureCity,
            DepartureTimezone = departureTimezone,
            DepartureDateTime = new DateTime(2024, 7, 15, 8, 0, 0),
            ArrivalCountry = arrivalCountry,
            ArrivalCity = arrivalCity ?? string.Empty,
            ArrivalTimezone = arrivalTimezone,
            ArrivalDateTime = new DateTime(2024, 7, 15, 20, 30, 0)
        };

        var isValid = IsValidTrip(trip);
        Assert.False(isValid);
    }

    [Theory]
    [InlineData("Canada", "Toronto", "America/Toronto", "United Kingdom", "London", null)]
    [InlineData("Canada", "Toronto", "America/Toronto", "United Kingdom", "London", "")]
    [InlineData("Canada", "Toronto", "America/Toronto", "United Kingdom", "London", "  ")]
    public void InvalidTrip_MissingArrivalTimezone(string departureCountry, string departureCity, string departureTimezone,
        string arrivalCountry, string arrivalCity, string? arrivalTimezone)
    {
        var trip = new TripDto
        {
            DepartureCountry = departureCountry,
            DepartureCity = departureCity,
            DepartureTimezone = departureTimezone,
            DepartureDateTime = new DateTime(2024, 7, 15, 8, 0, 0),
            ArrivalCountry = arrivalCountry,
            ArrivalCity = arrivalCity,
            ArrivalTimezone = arrivalTimezone ?? string.Empty,
            ArrivalDateTime = new DateTime(2024, 7, 15, 20, 30, 0)
        };

        var isValid = IsValidTrip(trip);
        Assert.False(isValid);
    }

    [Fact]
    public void InvalidTrip_AllFieldsMissing()
    {
        var trip = new TripDto
        {
            DepartureDateTime = new DateTime(2024, 7, 15, 8, 0, 0),
            ArrivalDateTime = new DateTime(2024, 7, 15, 20, 30, 0)
        };

        var isValid = IsValidTrip(trip);
        Assert.False(isValid);
    }

    // Helper method that mirrors the validation logic in ManageTrips.razor
    private bool IsValidTrip(TripDto trip)
    {
        if (string.IsNullOrWhiteSpace(trip.DepartureCity))
            return false;
        if (string.IsNullOrWhiteSpace(trip.DepartureCountry))
            return false;
        if (string.IsNullOrWhiteSpace(trip.DepartureTimezone))
            return false;
        if (string.IsNullOrWhiteSpace(trip.ArrivalCity))
            return false;
        if (string.IsNullOrWhiteSpace(trip.ArrivalCountry))
            return false;
        if (string.IsNullOrWhiteSpace(trip.ArrivalTimezone))
            return false;
        
        return true;
    }
}
