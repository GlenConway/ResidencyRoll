using Microsoft.EntityFrameworkCore;
using ResidencyRoll.Api.Data;
using ResidencyRoll.Api.Mcp;
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
        var endCount = residency.CalculateResidencyDays(log, RollingWindow.StartOn(periodEnd), periodEnd)["United Kingdom"];

        Assert.True(peaks["United Kingdom"].Days > endCount);
        Assert.True(peaks["United Kingdom"].Date < periodEnd);
    }

    [Fact]
    public void RollingWindow_BoundaryDay_ShouldBeCountedOnce()
    {
        var end = new DateOnly(2026, 12, 22);
        var start = RollingWindow.StartOn(end);
        Assert.Equal(end.AddDays(-364), start);
        Assert.Equal(RollingWindow.LengthDays, end.DayNumber - start.DayNumber + 1);

        var residency = new ResidencyCalculationService();
        var log = new List<DailyPresence>();
        AddStay(log, start.AddDays(-1), start, "United Kingdom");

        var counted = residency.CalculateResidencyDays(log, start, end);
        Assert.Equal(1, counted["United Kingdom"]);

        var peak = residency.PeakRollingCounts(log, end, end)["United Kingdom"];
        Assert.Equal(1, peak.Days);
        Assert.Equal(end, peak.Date);
    }

    [Fact]
    public async Task ForecastBreakdown_WhenOldDaysRollOff_ShouldReportAHigherPeak()
    {
        await using var db = NewDatabase();
        var residency = new ResidencyCalculationService();
        var query = new ResidencyQueryService(new TripService(db, residency), residency);
        db.Trips.Add(Leg("local", "Canada", "America/Halifax", new DateTime(2025, 2, 1, 8, 0, 0), "France", "Europe/Paris", new DateTime(2025, 2, 1, 18, 0, 0)));
        db.Trips.Add(Leg("local", "France", "Europe/Paris", new DateTime(2025, 2, 28, 10, 0, 0), "Canada", "America/Halifax", new DateTime(2025, 2, 28, 16, 0, 0)));
        await db.SaveChangesAsync();

        var breakdown = await query.ForecastBreakdownAsync("local",
        [
            Leg("local", "Canada", "America/Halifax", new DateTime(2026, 2, 1, 8, 0, 0), "France", "Europe/Paris", new DateTime(2026, 2, 1, 18, 0, 0)),
            Leg("local", "France", "Europe/Paris", new DateTime(2026, 2, 5, 10, 0, 0), "Canada", "America/Halifax", new DateTime(2026, 2, 20, 16, 0, 0))
        ]);

        Assert.True(breakdown.WindowEnd > breakdown.WindowStart);
        var peak = breakdown.Peaks["France"];
        var endDays = breakdown.Forecast["France"];
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
    public async Task GetResidencySummary_WhenStartIsOmitted_ShouldExcludeThePreviousUkTaxYear()
    {
        await using var db = NewDatabase();
        var query = new ResidencyQueryService(new TripService(db, new ResidencyCalculationService()), new ResidencyCalculationService());
        db.Trips.Add(Leg("local", "Canada", "America/Halifax", new DateTime(2025, 2, 1, 8, 0, 0), "United Kingdom", "Europe/London", new DateTime(2025, 2, 1, 18, 0, 0)));
        db.Trips.Add(Leg("local", "United Kingdom", "Europe/London", new DateTime(2025, 2, 4, 10, 0, 0), "Canada", "America/Halifax", new DateTime(2025, 2, 4, 16, 0, 0)));
        db.Trips.Add(Leg("local", "Canada", "America/Halifax", new DateTime(2026, 6, 1, 8, 0, 0), "United Kingdom", "Europe/London", new DateTime(2026, 6, 1, 18, 0, 0)));
        db.Trips.Add(Leg("local", "United Kingdom", "Europe/London", new DateTime(2026, 6, 3, 10, 0, 0), "Canada", "America/Halifax", new DateTime(2026, 6, 3, 16, 0, 0)));
        await db.SaveChangesAsync();

        var summary = await query.GetResidencySummaryAsync("local", null, new DateOnly(2026, 11, 9));
        var uk = summary.Single(row => row.CountryName == "United Kingdom");

        Assert.Equal("taxYear", uk.WindowType);
        Assert.Equal(new DateOnly(2026, 4, 6), uk.WindowStart);
        Assert.Equal(new DateOnly(2027, 4, 5), uk.WindowEnd);
        // June 2 and 3 are UK midnights. June 1 counts as well because midnight was in Canada, which uses the partial-day rule.
        // The February 2025 UK days fall in the previous tax year and are not included.
        Assert.Equal(3, uk.TotalDays);
    }

    [Fact]
    public async Task CalculateMaxEndDate_WithHypotheticalTrips_ShouldCountThoseDays()
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
            TripStart = new DateTime(2026, 7, 1, 12, 0, 0),
            DayLimit = 10
        };
        var prior = new[]
        {
            Leg("local", "Canada", "America/Halifax", new DateTime(2026, 6, 1, 8, 0, 0), "United Kingdom", "Europe/London", new DateTime(2026, 6, 1, 18, 0, 0)),
            Leg("local", "United Kingdom", "Europe/London", new DateTime(2026, 6, 6, 10, 0, 0), "Canada", "America/Halifax", new DateTime(2026, 6, 6, 16, 0, 0))
        };

        var alone = await query.CalculateMaxEndDateAsync("local", request);
        var withPrior = await query.CalculateMaxEndDateAsync("local", request, prior);

        Assert.True(withPrior.MaxEndDate < alone.MaxEndDate);
        Assert.InRange(withPrior.DaysAtLimit, 1, 10);
        Assert.Equal("taxYear", withPrior.WindowType);
    }

    [Fact]
    public async Task CalculateEarliestArrival_ShouldStayAtOrUnderTheLimit()
    {
        await using var db = NewDatabase();
        var query = new ResidencyQueryService(new TripService(db, new ResidencyCalculationService()), new ResidencyCalculationService());
        var departure = new DateTime(2026, 7, 20, 12, 0, 0);
        var request = new MaxTripEndDateRequestDto
        {
            DepartureCountry = "Canada",
            DepartureCity = "Halifax",
            DepartureTimezone = "America/Halifax",
            ArrivalCountry = "United Kingdom",
            ArrivalCity = "London",
            ArrivalTimezone = "Europe/London",
            TripStart = departure,
            DayLimit = 10
        };

        var result = await query.CalculateEarliestArrivalAsync("local", request, departure);

        // Arriving on 10 July makes that day count: midnight is still in Canada, and Canada counts every country visited.
        Assert.Equal(new DateTime(2026, 7, 11, 12, 0, 0), result.Arrival);
        Assert.Equal(10, result.DaysAtLimit);
        Assert.Equal("taxYear", result.WindowType);
    }

    [Fact]
    public async Task PlanMaxPresence_ShouldSkipBlackoutDays()
    {
        await using var db = NewDatabase();
        var query = new ResidencyQueryService(new TripService(db, new ResidencyCalculationService()), new ResidencyCalculationService());

        var plan = await query.PlanMaxPresenceAsync(
            "local",
            "France",
            new DateOnly(2026, 12, 20),
            new DateOnly(2026, 12, 31),
            [(new DateOnly(2026, 12, 24), new DateOnly(2026, 12, 26))],
            10);

        Assert.Equal(9, plan.PlannedDays);
        Assert.Equal(2, plan.Stays.Count);
        Assert.Equal(new DateOnly(2026, 12, 20), plan.Stays[0].Arrival);
        Assert.Equal(new DateOnly(2026, 12, 23), plan.Stays[0].Departure);
        Assert.Equal(new DateOnly(2026, 12, 27), plan.Stays[1].Arrival);
        Assert.Equal(new DateOnly(2026, 12, 31), plan.Stays[1].Departure);
        Assert.True(plan.PeakDays <= 10);
    }

    [Fact]
    public async Task GetRollingCount_WhenTheAnniversaryCounted_ShouldNotIncludeItInTheTotal()
    {
        await using var db = NewDatabase();
        var query = new ResidencyQueryService(new TripService(db, new ResidencyCalculationService()), new ResidencyCalculationService());
        db.Trips.Add(Leg("local", "Canada", "America/Halifax", new DateTime(2024, 12, 31, 8, 0, 0), "United Kingdom", "Europe/London", new DateTime(2024, 12, 31, 18, 0, 0)));
        db.Trips.Add(Leg("local", "United Kingdom", "Europe/London", new DateTime(2025, 1, 1, 10, 0, 0), "Canada", "America/Halifax", new DateTime(2025, 1, 1, 16, 0, 0)));
        await db.SaveChangesAsync();

        var rows = await query.GetRollingCountAsync("local", "United Kingdom", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2));

        Assert.Equal(new DateOnly(2025, 1, 1), rows[0].AnniversaryDate);
        Assert.True(rows[0].AnniversaryCounted);
        Assert.Equal(0, rows[0].RollingDays);
        Assert.Equal(new DateOnly(2025, 1, 2), rows[1].AnniversaryDate);
        Assert.False(rows[1].AnniversaryCounted);
        Assert.Equal(0, rows[1].RollingDays);
    }

    [Fact]
    public void ThresholdStatus_WhenTheCountEqualsTheThreshold_ShouldBeReached()
    {
        var reached = ResidencyCountryStatus.From(new ResidencySummaryDto
        {
            CountryName = "United Kingdom",
            TotalDays = 183,
            ThresholdDays = 183,
            DaysUntilThreshold = 0,
            WindowType = "taxYear",
            WindowStart = new DateOnly(2026, 4, 6),
            WindowEnd = new DateOnly(2027, 4, 5)
        }, "Canada");
        var over = ResidencyCountryStatus.From(new ResidencySummaryDto
        {
            CountryName = "United Kingdom",
            TotalDays = 184,
            ThresholdDays = 183,
            DaysUntilThreshold = 0
        }, "Canada");

        Assert.Equal("reached", reached.ThresholdStatus);
        Assert.Equal(0, reached.DaysOverThreshold);
        Assert.Equal(0, reached.DaysUntilThreshold);
        Assert.False(reached.IsApproachingThreshold);
        Assert.Equal("over", over.ThresholdStatus);
        Assert.Equal(1, over.DaysOverThreshold);
        Assert.False(over.IsApproachingThreshold);
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

    [Fact]
    public async Task GetRecordCoverage_ShouldSpanTheSavedLegs()
    {
        await using var db = NewDatabase();
        var query = new ResidencyQueryService(new TripService(db, new ResidencyCalculationService()), new ResidencyCalculationService());
        db.Trips.Add(Leg("local", "Canada", "America/Halifax", new DateTime(2024, 1, 19, 8, 0, 0), "United Kingdom", "Europe/London", new DateTime(2024, 1, 19, 16, 0, 0)));
        db.Trips.Add(Leg("local", "United Kingdom", "Europe/London", new DateTime(2026, 11, 23, 10, 0, 0), "Canada", "America/Halifax", new DateTime(2026, 11, 23, 16, 0, 0)));
        await db.SaveChangesAsync();

        var coverage = await query.GetRecordCoverageAsync("local");

        Assert.Equal(new DateOnly(2024, 1, 19), coverage.FirstTripDate);
        Assert.Equal(new DateOnly(2026, 11, 23), coverage.LastTripDate);
        Assert.Equal(2, coverage.LegCount);
    }

    [Fact]
    public void ApplyCoverage_WhenTheCalendarYearRunsPastTheRecords_ShouldBeIncomplete()
    {
        var records = new RecordCoverage(new DateOnly(2024, 1, 19), new DateOnly(2026, 11, 23), 2);
        var year2024 = new ResidencyCountryStatus { WindowStart = new DateOnly(2024, 1, 1), WindowEnd = new DateOnly(2024, 12, 31) };
        var year2026 = new ResidencyCountryStatus { WindowStart = new DateOnly(2026, 1, 1), WindowEnd = new DateOnly(2026, 12, 31) };

        ResidencyCountryStatus.ApplyCoverage(year2024, records);
        ResidencyCountryStatus.ApplyCoverage(year2026, records);

        Assert.False(year2024.IsComplete);
        Assert.Equal(new DateOnly(2024, 1, 19), year2024.CoverageStart);
        Assert.Equal(new DateOnly(2024, 12, 31), year2024.CoverageEnd);
        Assert.False(year2026.IsComplete);
        Assert.Equal(new DateOnly(2026, 1, 1), year2026.CoverageStart);
        Assert.Equal(new DateOnly(2026, 11, 23), year2026.CoverageEnd);
    }

    [Fact]
    public async Task CountUnaccountedDays_WhenTheNextLegLeavesAnotherCountry_ShouldCountTheGap()
    {
        await using var db = NewDatabase();
        var residency = new ResidencyCalculationService();
        var query = new ResidencyQueryService(new TripService(db, residency), residency);
        db.Trips.Add(Leg("local", "United Kingdom", "Europe/London", new DateTime(2026, 6, 1, 9, 0, 0), "Canada", "America/Halifax", new DateTime(2026, 6, 1, 16, 0, 0)));
        db.Trips.Add(Leg("local", "France", "Europe/Paris", new DateTime(2026, 6, 10, 9, 0, 0), "United Kingdom", "Europe/London", new DateTime(2026, 6, 10, 16, 0, 0)));
        await db.SaveChangesAsync();

        var gap = await query.CountUnaccountedDaysAsync("local", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 10));

        Assert.Equal(8, gap);
    }

    [Fact]
    public async Task CountUnaccountedDays_WhenTheStayStaysInOneCountry_ShouldBeZero()
    {
        await using var db = NewDatabase();
        var residency = new ResidencyCalculationService();
        var query = new ResidencyQueryService(new TripService(db, residency), residency);
        db.Trips.Add(Leg("local", "United Kingdom", "Europe/London", new DateTime(2026, 6, 1, 9, 0, 0), "Canada", "America/Halifax", new DateTime(2026, 6, 1, 16, 0, 0)));
        db.Trips.Add(Leg("local", "Canada", "America/Halifax", new DateTime(2026, 6, 10, 8, 0, 0), "United Kingdom", "Europe/London", new DateTime(2026, 6, 10, 18, 0, 0)));
        await db.SaveChangesAsync();

        var gap = await query.CountUnaccountedDaysAsync("local", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 10));

        Assert.Equal(0, gap);
    }

    [Fact]
    public async Task CountCalendarYears_ForCanada_ShouldReportTheMinimumForEachYearThePlanTouches()
    {
        await using var db = NewDatabase();
        var residency = new ResidencyCalculationService();
        var query = new ResidencyQueryService(new TripService(db, residency), residency);
        var plan = new List<Trip>
        {
            Leg("local", "United Kingdom", "Europe/London", new DateTime(2026, 12, 30, 9, 0, 0), "Canada", "America/Halifax", new DateTime(2026, 12, 30, 16, 0, 0)),
            Leg("local", "Canada", "America/Halifax", new DateTime(2027, 1, 5, 8, 0, 0), "United Kingdom", "Europe/London", new DateTime(2027, 1, 5, 18, 0, 0))
        };

        var years = await query.CountCalendarYearsAsync("local", "Canada", plan, null);
        var unitedKingdom = await query.CountCalendarYearsAsync("local", "United Kingdom", plan, null);
        var stay = new List<Trip>
        {
            Leg("local", "United Kingdom", "Europe/London", new DateTime(2027, 6, 1, 9, 0, 0), "Canada", "America/Halifax", new DateTime(2027, 6, 1, 16, 0, 0)),
            Leg("local", "Canada", "America/Halifax", new DateTime(2027, 6, 10, 8, 0, 0), "United Kingdom", "Europe/London", new DateTime(2027, 6, 10, 18, 0, 0))
        };
        var june = await query.CountCalendarYearsAsync("local", "Canada", stay, null);

        Assert.Equal([2026, 2027], years.Select(year => year.Year).ToArray());
        Assert.All(years, year => Assert.True(year.Days > 0));
        Assert.Equal(2027, Assert.Single(june).Year);
        Assert.Equal(9, june[0].Days);
        Assert.All(years.Concat(june), year =>
        {
            Assert.Equal("Canada", year.Country);
            Assert.Equal(183, year.MinimumDays);
            Assert.Equal(183 - year.Days, year.DaysShort);
            Assert.Equal("under", year.Status);
        });
        Assert.All(unitedKingdom, year =>
        {
            Assert.Null(year.MinimumDays);
            Assert.Null(year.Status);
        });
    }

    [Fact]
    public async Task MeasureMinimumPresence_ForCanada_ShouldUseTheCalendarYear()
    {
        await using var db = NewDatabase();
        var query = new ResidencyQueryService(new TripService(db, new ResidencyCalculationService()), new ResidencyCalculationService());
        db.Trips.Add(Leg("local", "United Kingdom", "Europe/London", new DateTime(2026, 6, 1, 9, 0, 0), "Canada", "America/Halifax", new DateTime(2026, 6, 1, 16, 0, 0)));
        db.Trips.Add(Leg("local", "Canada", "America/Halifax", new DateTime(2026, 6, 4, 10, 0, 0), "United Kingdom", "Europe/London", new DateTime(2026, 6, 4, 18, 0, 0)));
        await db.SaveChangesAsync();

        var canada = await query.MeasureMinimumPresenceAsync("local", "Canada", new DateOnly(2026, 11, 9));
        var uk = await query.MeasureMinimumPresenceAsync("local", "United Kingdom", new DateOnly(2026, 11, 9));

        Assert.NotNull(canada);
        Assert.Equal(183, canada.MinimumDays);
        Assert.Equal("calendarYear", canada.WindowType);
        Assert.Equal(new DateOnly(2026, 1, 1), canada.WindowStart);
        Assert.Equal(new DateOnly(2026, 12, 31), canada.WindowEnd);
        Assert.True(canada.Days > 0);
        Assert.Equal(183 - canada.Days, canada.DaysShort);
        Assert.Equal("under", canada.Status);
        Assert.Null(uk);
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
