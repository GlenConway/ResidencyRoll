using Microsoft.EntityFrameworkCore;
using ResidencyRoll.Api.Data;
using ResidencyRoll.Api.Models;
using ResidencyRoll.Api.Services;
using ResidencyRoll.Shared.Trips;
using Xunit;

namespace ResidencyRoll.Tests;

public class ResidencyQueryServiceTests
{
    [Fact]
    public async Task GetResidencySummary_ShouldMatchMidnightThresholdRules()
    {
        await using var db = NewDatabase();
        var residency = new ResidencyCalculationService();
        var trips = new TripService(db, residency);
        var query = new ResidencyQueryService(trips, residency);
        db.Trips.Add(Leg("local", "Canada", "America/Toronto", new DateTime(2026, 1, 1, 12, 0, 0), "United Kingdom", "Europe/London", new DateTime(2026, 1, 1, 22, 0, 0)));
        db.Trips.Add(Leg("local", "United Kingdom", "Europe/London", new DateTime(2026, 1, 11, 9, 0, 0), "Canada", "America/Toronto", new DateTime(2026, 1, 11, 14, 0, 0)));
        await db.SaveChangesAsync();

        var summary = await query.GetResidencySummaryAsync("local", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        var uk = Assert.Single(summary, item => item.CountryName == "United Kingdom");
        Assert.Equal("Midnight", uk.RuleType);
        Assert.Equal(183, uk.ThresholdDays);
        Assert.True(uk.TotalDays > 0);
        Assert.Equal(Math.Max(0, 183 - uk.TotalDays), uk.DaysUntilThreshold);
    }

    [Fact]
    public async Task CalculateMaxEndDate_ShouldStayWithinTheDayLimit()
    {
        await using var db = NewDatabase();
        var query = new ResidencyQueryService(new TripService(db, new ResidencyCalculationService()), new ResidencyCalculationService());
        var request = new MaxTripEndDateRequestDto
        {
            DepartureCountry = "Canada",
            DepartureCity = "Halifax",
            DepartureTimezone = "America/Halifax",
            ArrivalCountry = "United Kingdom",
            ArrivalCity = "London",
            ArrivalTimezone = "Europe/London",
            TripStart = new DateTime(2026, 6, 1, 12, 0, 0),
            DayLimit = 10
        };

        var result = await query.CalculateMaxEndDateAsync("local", request);

        Assert.True(result.MaxEndDate >= request.TripStart);
        Assert.InRange(result.DaysAtLimit, 1, 10);
    }

    [Fact]
    public async Task GetStandardDurationForecasts_ShouldReturnOneRowPerDuration()
    {
        await using var db = NewDatabase();
        var query = new ResidencyQueryService(new TripService(db, new ResidencyCalculationService()), new ResidencyCalculationService());
        var rows = await query.GetStandardDurationForecastsAsync("local", new StandardDurationForecastRequestDto
        {
            DepartureCountry = "Canada",
            DepartureCity = "Halifax",
            DepartureTimezone = "America/Halifax",
            ArrivalCountry = "United Kingdom",
            ArrivalCity = "London",
            ArrivalTimezone = "Europe/London",
            TripStart = new DateTime(2026, 6, 1, 12, 0, 0),
            DayLimit = 183,
            Durations = [7, 30]
        });

        Assert.Equal(2, rows.Count);
        Assert.Equal(7, rows[0].DurationDays);
        Assert.False(rows[0].ExceedsLimit);
        Assert.True(rows[0].TotalDaysInCountry > 0);
    }

    [Fact]
    public async Task Forecast_DateLineCrossing_ShouldIncludeBothCountries()
    {
        await using var db = NewDatabase();
        var query = new ResidencyQueryService(new TripService(db, new ResidencyCalculationService()), new ResidencyCalculationService());
        var forecast = await query.ForecastAsync("local",
        [
            new Trip
            {
                UserId = "local",
                DepartureCountry = "New Zealand",
                DepartureCity = "Auckland",
                DepartureDateTime = new DateTime(2026, 12, 2, 10, 0, 0),
                DepartureTimezone = "Pacific/Auckland",
                DepartureIataCode = "AKL",
                ArrivalCountry = "United States",
                ArrivalCity = "Honolulu",
                ArrivalDateTime = new DateTime(2026, 12, 1, 12, 0, 0),
                ArrivalTimezone = "Pacific/Honolulu",
                ArrivalIataCode = "HNL"
            }
        ]);

        Assert.Contains(forecast.Forecast, row => row.CountryName is "New Zealand" or "United States" && row.Days > 0);
    }

    private static ApplicationDbContext NewDatabase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Trip Leg(string userId, string fromCountry, string fromZone, DateTime departure, string toCountry, string toZone, DateTime arrival) => new()
    {
        UserId = userId,
        DepartureCountry = fromCountry,
        DepartureCity = fromCountry,
        DepartureTimezone = fromZone,
        DepartureDateTime = departure,
        ArrivalCountry = toCountry,
        ArrivalCity = toCountry,
        ArrivalTimezone = toZone,
        ArrivalDateTime = arrival
    };
}
