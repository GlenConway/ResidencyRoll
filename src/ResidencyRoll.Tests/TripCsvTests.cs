using ResidencyRoll.Api.Models;
using ResidencyRoll.Api.Services;
using Xunit;

namespace ResidencyRoll.Tests;

public class TripCsvTests
{
    [Fact]
    public void Parse_OldTenColumns_ShouldImportTheLeg()
    {
        var parsed = TripCsv.Parse("Canada,Halifax,2025-02-07 10:40:00,America/Halifax,YHZ,United Kingdom,London,2025-02-07 20:30:00,Europe/London,LHR");

        Assert.Empty(parsed.Errors);
        var trip = Assert.Single(parsed.Rows).Trip;
        Assert.Equal("YHZ", trip.DepartureIataCode);
        Assert.Equal("LHR", trip.ArrivalIataCode);
        Assert.Null(trip.Source);
    }

    [Fact]
    public void Parse_HeaderWithProvenance_ShouldKeepSourceAndBookingRef()
    {
        var csv = """
            DepartureCountry,DepartureCity,DepartureDateTime,DepartureTimezone,DepartureIataCode,ArrivalCountry,ArrivalCity,ArrivalDateTime,ArrivalTimezone,ArrivalIataCode,Traveller,Source,BookingRef,Notes
            Canada,Halifax,2026-02-01 08:00:00,America/Halifax,YHZ,Australia,Gold Coast,2026-02-02 16:00:00,Australia/Brisbane,OOL,Sam,email,ABC123,"Window, aisle"
            """;

        var parsed = TripCsv.Parse(csv);

        Assert.Empty(parsed.Errors);
        var row = Assert.Single(parsed.Rows);
        Assert.Equal("Sam", row.TravellerName);
        Assert.Equal("email", row.Trip.Source);
        Assert.Equal("ABC123", row.Trip.BookingRef);
        Assert.Equal("Window, aisle", row.Trip.Notes);
        Assert.Equal("OOL", row.Trip.ArrivalIataCode);
    }

    [Fact]
    public void Parse_SeveralBadRows_ShouldReturnEveryError()
    {
        var csv = """
            Canada,Halifax,not-a-date,America/Halifax,YHZ,United Kingdom,London,2026-02-01 18:00:00,Europe/London,LHR
            Canada,Halifax,2026-02-02 08:00:00,AST,YHZ,United Kingdom,London,2026-02-02 18:00:00,Europe/London,LHR
            """;

        var parsed = TripCsv.Parse(csv);

        Assert.Empty(parsed.Rows);
        Assert.Equal(2, parsed.Errors.Count);
        Assert.Contains("date", parsed.Errors[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AST", parsed.Errors[1]);
    }

    [Fact]
    public void Format_ShouldRoundTripThroughParse()
    {
        var trip = new Trip
        {
            DepartureCountry = "Canada",
            DepartureCity = "Halifax",
            DepartureDateTime = new DateTime(2026, 3, 1, 8, 0, 0),
            DepartureTimezone = "America/Halifax",
            DepartureIataCode = "YHZ",
            ArrivalCountry = "Morocco",
            ArrivalCity = "Casablanca",
            ArrivalDateTime = new DateTime(2026, 3, 1, 20, 0, 0),
            ArrivalTimezone = "Africa/Casablanca",
            ArrivalIataCode = "CMN",
            Source = "calendar",
            BookingRef = "CMN1"
        };

        var parsed = TripCsv.Parse(TripCsv.Format([trip], "Local User"));

        Assert.Empty(parsed.Errors);
        var row = Assert.Single(parsed.Rows);
        Assert.Equal("Local User", row.TravellerName);
        Assert.Equal("CMN", row.Trip.ArrivalIataCode);
        Assert.Equal("calendar", row.Trip.Source);
        Assert.Equal("CMN1", row.Trip.BookingRef);
    }
}
