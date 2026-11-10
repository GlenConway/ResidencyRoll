using System;
using System.Collections.Generic;
using System.Linq;
using ResidencyRoll.Api.Mcp;
using ResidencyRoll.Api.Models;
using ResidencyRoll.Api.Services;
using Xunit;

namespace ResidencyRoll.Tests;

public class TripCheckTests
{
    private readonly ResidencyCalculationService _residency = new();

    [Fact]
    public void InspectLeg_WhenTheFlightIsThirtyHours_ShouldWarn()
    {
        var trip = Leg(1, "YHZ", "America/Halifax", new DateTime(2024, 1, 1, 8, 0, 0), "LHR", "Europe/London", new DateTime(2024, 1, 2, 18, 0, 0));

        var warning = Assert.Single(TripAuditor.InspectLeg(trip));

        Assert.Equal("longHaul", warning.Code);
        Assert.Equal(1, warning.TripId);
    }

    [Fact]
    public void InspectLeg_WhenLondonToHalifaxIsFourHours_ShouldWarn()
    {
        var trip = Leg(67, "LHR", "Europe/London", new DateTime(2024, 1, 10, 10, 0, 0), "YHZ", "America/Halifax", new DateTime(2024, 1, 10, 10, 0, 0));

        var warning = Assert.Single(TripAuditor.InspectLeg(trip));

        Assert.Equal("unusualDuration", warning.Code);
    }

    [Fact]
    public void InspectLeg_WhenArrivalIsBeforeDepartureInUtc_ShouldWarn()
    {
        var trip = Leg(2, "LHR", "Europe/London", new DateTime(2024, 1, 10, 18, 0, 0), "YHZ", "America/Halifax", new DateTime(2024, 1, 10, 8, 0, 0));

        var warning = Assert.Single(TripAuditor.InspectLeg(trip));

        Assert.Equal("arrivalBeforeDeparture", warning.Code);
    }

    [Fact]
    public void InspectAll_WhenLegsOverlapOrChangeCountry_ShouldWarn()
    {
        var first = Leg(10, "YHZ", "America/Halifax", new DateTime(2024, 3, 1, 8, 0, 0), "LHR", "Europe/London", new DateTime(2024, 3, 1, 18, 0, 0));
        var overlap = Leg(11, "LHR", "Europe/London", new DateTime(2024, 3, 1, 16, 0, 0), "CDG", "Europe/Paris", new DateTime(2024, 3, 1, 18, 0, 0));
        var later = Leg(12, "YUL", "America/Toronto", new DateTime(2024, 3, 10, 9, 0, 0), "LHR", "Europe/London", new DateTime(2024, 3, 10, 18, 0, 0));

        var warnings = TripAuditor.InspectAll([first, overlap, later]);

        Assert.Contains(warnings, warning => warning.Code == "overlap");
        Assert.Contains(warnings, warning => warning.Code == "countryGap" && warning.TripId == 11 && warning.OtherTripId == 12);
    }

    [Fact]
    public void Compare_WhenAUkDayIsAddedIn2024_ShouldReportThatYear()
    {
        var before = new List<Trip>();
        var after = new List<Trip>
        {
            Leg(64, "YHZ", "America/Halifax", new DateTime(2024, 3, 1, 8, 0, 0), "LHR", "Europe/London", new DateTime(2024, 3, 1, 16, 0, 0)),
            Leg(65, "LHR", "Europe/London", new DateTime(2024, 3, 3, 10, 0, 0), "YHZ", "America/Halifax", new DateTime(2024, 3, 3, 14, 0, 0))
        };

        var impact = TripImpact.Compare(before, after, _residency, new DateOnly(2026, 11, 9));
        var uk = impact.Single(change => change.Country == "United Kingdom" && change.Year == 2024);

        Assert.True(uk.Delta > 0, TripImpact.Describe(impact));
        Assert.Equal("calendarYear", uk.WindowType);
        Assert.DoesNotContain(impact, change => change.Country == "United Kingdom" && change.WindowType == "rolling365");
        Assert.Contains($"United Kingdom +{uk.Delta} in 2024", TripImpact.Describe(impact));
    }

    [Fact]
    public void ApplyPatch_WhenOnlyTheArrivalTimeIsSent_ShouldKeepTheDeparture()
    {
        var airports = new AirportCatalog();
        var trip = Leg(1, "YHZ", "America/Halifax", new DateTime(2024, 6, 1, 8, 0, 0), "LHR", "Europe/London", new DateTime(2024, 6, 1, 16, 0, 0));

        TripLegResolver.ApplyPatch(trip, new McpTripLeg { ArrivalDateTime = "2024-06-01T18:30" }, airports);

        Assert.Equal(new DateTime(2024, 6, 1, 8, 0, 0), trip.DepartureDateTime);
        Assert.Equal("YHZ", trip.DepartureIataCode);
        Assert.Equal("Canada", trip.DepartureCountry);
        Assert.Equal(new DateTime(2024, 6, 1, 18, 30, 0), trip.ArrivalDateTime);
        Assert.Equal("LHR", trip.ArrivalIataCode);
    }

    private static Trip Leg(int id, string from, string fromZone, DateTime departure, string to, string toZone, DateTime arrival) => new()
    {
        Id = id,
        UserId = "local",
        DepartureIataCode = from,
        DepartureCity = from,
        DepartureCountry = from == "LHR" || from == "CDG" ? "United Kingdom" : "Canada",
        DepartureTimezone = fromZone,
        DepartureDateTime = departure,
        ArrivalIataCode = to,
        ArrivalCity = to,
        ArrivalCountry = to == "LHR" || to == "CDG" ? (to == "CDG" ? "France" : "United Kingdom") : "Canada",
        ArrivalTimezone = toZone,
        ArrivalDateTime = arrival
    };
}
