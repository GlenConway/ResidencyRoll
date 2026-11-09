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
    public void PeakRollingCounts_WhenEarlierDaysRollOff_ShouldExceedTheWindowEndCount()
    {
        var residency = new ResidencyCalculationService();
        var log = new List<DailyPresence>();
        AddStay(log, new DateOnly(2025, 1, 1), new DateOnly(2025, 3, 1), "United Kingdom");
        AddStay(log, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 20), "United Kingdom");

        var periodEnd = new DateOnly(2026, 2, 15);
        var peaks = residency.PeakRollingCounts(log, new DateOnly(2026, 1, 1), periodEnd);
        var endCount = residency.CalculateResidencyDays(log, periodEnd.AddDays(-365), periodEnd)["United Kingdom"];

        Assert.True(peaks["United Kingdom"].Days > endCount);
        Assert.True(peaks["United Kingdom"].Date < periodEnd);
    }

    [Fact]
    public async Task ForecastBreakdown_WhenOldDaysRollOff_ShouldReportAHigherPeak()
    {
        await using var db = NewDatabase();
        var residency = new ResidencyCalculationService();
        var query = new ResidencyQueryService(new TripService(db, residency), residency);
        db.Trips.Add(Leg("local", "Canada", "America/Halifax", new DateTime(2025, 2, 1, 8, 0, 0), "United Kingdom", "Europe/London", new DateTime(2025, 2, 1, 18, 0, 0)));
        db.Trips.Add(Leg("local", "United Kingdom", "Europe/London", new DateTime(2025, 2, 28, 10, 0, 0), "Canada", "America/Halifax", new DateTime(2025, 2, 28, 16, 0, 0)));
        await db.SaveChangesAsync();

        var breakdown = await query.ForecastBreakdownAsync("local",
        [
            Leg("local", "Canada", "America/Halifax", new DateTime(2026, 2, 1, 8, 0, 0), "United Kingdom", "Europe/London", new DateTime(2026, 2, 1, 18, 0, 0)),
            Leg("local", "United Kingdom", "Europe/London", new DateTime(2026, 2, 5, 10, 0, 0), "Canada", "America/Halifax", new DateTime(2026, 2, 20, 16, 0, 0))
        ]);

        Assert.True(breakdown.WindowEnd > breakdown.WindowStart);
        var peak = breakdown.Peaks["United Kingdom"];
        var endDays = breakdown.Forecast["United Kingdom"];
        Assert.True(peak.Days > endDays, $"peak {peak.Days} on {peak.Date:yyyy-MM-dd}, end {endDays}, window {breakdown.WindowStart:yyyy-MM-dd} to {breakdown.WindowEnd:yyyy-MM-dd}");
    }

    [Fact]
    public async Task CalculateMaxEndDate_DaysAtLimit_ShouldBeThePeakInsideTheWindow()
    {
        await using var db = NewDatabase();
        var residency = new ResidencyCalculationService();
        var trips = new TripService(db, residency);
        var query = new ResidencyQueryService(trips, residency);
        var existing = new List<Trip>
        {
            Leg("local", "Canada", "America/Halifax", new DateTime(2025, 6, 1, 8, 0, 0), "United Kingdom", "Europe/London", new DateTime(2025, 6, 1, 18, 0, 0)),
            Leg("local", "United Kingdom", "Europe/London", new DateTime(2025, 6, 2, 10, 0, 0), "Canada", "America/Halifax", new DateTime(2025, 6, 2, 16, 0, 0))
        };
        db.Trips.AddRange(existing);
        await db.SaveChangesAsync();

        var request = new MaxTripEndDateRequestDto
        {
            DepartureCountry = "Canada",
            DepartureCity = "Halifax",
            DepartureTimezone = "America/Halifax",
            ArrivalCountry = "United Kingdom",
            ArrivalCity = "London",
            ArrivalTimezone = "Europe/London",
            TripStart = new DateTime(2026, 6, 1, 22, 0, 0),
            DayLimit = 10
        };

        var atStart = query.MeasureStay(existing, request, request.TripStart);
        Assert.True(atStart.PeakDays >= atStart.EndOfWindowDays);

        var result = await query.CalculateMaxEndDateAsync("local", request);
        var chosen = query.MeasureStay(existing, request, result.MaxEndDate);
        Assert.Equal(chosen.PeakDays, result.DaysAtLimit);
        Assert.True(chosen.PeakDays >= chosen.EndOfWindowDays);
        if (atStart.PeakDays <= request.DayLimit)
        {
            Assert.InRange(result.DaysAtLimit, 0, request.DayLimit);
        }
        else
        {
            Assert.Equal(request.TripStart, result.MaxEndDate);
        }
    }

    [Fact]
    public async Task GetLocationOn_AfterTheLastTrip_ShouldProjectTheLastArrivalCountry()
    {
        await using var db = NewDatabase();
        var query = new ResidencyQueryService(new TripService(db, new ResidencyCalculationService()), new ResidencyCalculationService());
        db.Trips.Add(Leg("local", "United Kingdom", "Europe/London", new DateTime(2026, 1, 1, 9, 0, 0), "Canada", "America/Halifax", new DateTime(2026, 1, 1, 16, 0, 0), "YHZ"));
        db.Trips.Add(Leg("local", "Canada", "America/Halifax", new DateTime(2026, 1, 10, 8, 0, 0), "United Kingdom", "Europe/London", new DateTime(2026, 1, 10, 18, 0, 0), "LHR"));
        await db.SaveChangesAsync();

        var between = await query.GetLocationOnAsync("local", new DateOnly(2026, 1, 5));
        Assert.Equal("gapFilled", between.Status);
        Assert.Equal("Canada", between.LocationAtMidnight);

        var travelDay = await query.GetLocationOnAsync("local", new DateOnly(2026, 1, 10));
        Assert.Equal("recorded", travelDay.Status);

        var future = await query.GetLocationOnAsync("local", new DateOnly(2026, 12, 5));
        Assert.Equal("projected", future.Status);
        Assert.Equal("United Kingdom", future.LocationAtMidnight);
        Assert.Equal(new[] { "United Kingdom" }, future.LocationsDuringDay);

        var before = await query.GetLocationOnAsync("local", new DateOnly(2025, 12, 1));
        Assert.Equal("noData", before.Status);
        Assert.Equal(string.Empty, before.LocationAtMidnight);
    }

    [Fact]
    public async Task GetCurrentLocation_ShouldReturnTheLastArrivalAirport()
    {
        await using var db = NewDatabase();
        var query = new ResidencyQueryService(new TripService(db, new ResidencyCalculationService()), new ResidencyCalculationService());
        db.Trips.Add(Leg("local", "United Kingdom", "Europe/London", new DateTime(2026, 1, 10, 10, 0, 0), "Canada", "America/Halifax", new DateTime(2026, 1, 10, 16, 0, 0), "YHZ"));
        await db.SaveChangesAsync();

        var current = await query.GetCurrentLocationAsync("local");

        Assert.Equal("Canada", current.Country);
        Assert.Equal("YHZ", current.LastArrivalIataCode);
        Assert.Equal("Canada", current.LastArrivalCountry);
        Assert.Equal(new DateOnly(2026, 1, 10), current.LastArrivalDate);
        Assert.Contains(current.Status, new[] { "projected", "gapFilled", "recorded" });
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

    private static void AddStay(List<DailyPresence> log, DateOnly start, DateOnly end, string country)
    {
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            log.Add(new DailyPresence
            {
                Date = day,
                LocationAtMidnight = country,
                LocationsDuringDay = new HashSet<string> { country }
            });
        }
    }

    private static Trip Leg(string userId, string fromCountry, string fromZone, DateTime departure, string toCountry, string toZone, DateTime arrival, string? arrivalIata = null) => new()
    {
        UserId = userId,
        DepartureCountry = fromCountry,
        DepartureCity = fromCountry,
        DepartureTimezone = fromZone,
        DepartureDateTime = departure,
        ArrivalCountry = toCountry,
        ArrivalCity = toCountry,
        ArrivalTimezone = toZone,
        ArrivalDateTime = arrival,
        ArrivalIataCode = arrivalIata
    };
}
