using System.ComponentModel;
using System.Globalization;
using ResidencyRoll.Api.Models;
using ResidencyRoll.Api.Services;
using ResidencyRoll.Shared.Trips;

namespace ResidencyRoll.Api.Mcp;

internal static class McpTime
{
    public const string LocalTimeDescription = "Local time, yyyy-MM-ddTHH:mm, in the airport's timezone";

    public static string Format(DateTime value) =>
        value.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

    public static int DurationMinutes(DateTime departureLocal, string? departureZone, DateTime arrivalLocal, string? arrivalZone)
    {
        var minutes = (int)Math.Round((ToUtc(arrivalLocal, arrivalZone) - ToUtc(departureLocal, departureZone)).TotalMinutes);
        return Math.Max(0, minutes);
    }

    private static DateTime ToUtc(DateTime local, string? timezoneId)
    {
        var unspecified = local.Kind == DateTimeKind.Unspecified ? local : DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var zoneId = string.IsNullOrWhiteSpace(timezoneId) ? "UTC" : timezoneId;
        if (zoneId == "UTC")
        {
            return DateTime.SpecifyKind(unspecified, DateTimeKind.Utc);
        }

        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
            return TimeZoneInfo.ConvertTimeToUtc(unspecified, zone);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateTime.SpecifyKind(unspecified, DateTimeKind.Utc);
        }
        catch (InvalidTimeZoneException)
        {
            return DateTime.SpecifyKind(unspecified, DateTimeKind.Utc);
        }
    }
}

public class McpTrip
{
    public int Id { get; set; }
    public string DepartureCountry { get; set; } = string.Empty;
    public string DepartureCity { get; set; } = string.Empty;

    [Description(McpTime.LocalTimeDescription)]
    public string DepartureDateTime { get; set; } = string.Empty;

    public string DepartureTimezone { get; set; } = string.Empty;
    public string? DepartureIataCode { get; set; }
    public string ArrivalCountry { get; set; } = string.Empty;
    public string ArrivalCity { get; set; } = string.Empty;

    [Description(McpTime.LocalTimeDescription)]
    public string ArrivalDateTime { get; set; } = string.Empty;

    public string ArrivalTimezone { get; set; } = string.Empty;
    public string? ArrivalIataCode { get; set; }
    public string CountryName { get; set; } = string.Empty;

    public int DurationDays { get; set; }

    [Description("Whole hours from departure to arrival, in UTC. durationMinutes is the exact length.")]
    public int DurationHours { get; set; }

    [Description("Total minutes from departure to arrival, in UTC.")]
    public int DurationMinutes { get; set; }

    public static McpTrip From(TripDto dto)
    {
        var minutes = McpTime.DurationMinutes(dto.DepartureDateTime, dto.DepartureTimezone, dto.ArrivalDateTime, dto.ArrivalTimezone);
        return new McpTrip
        {
            Id = dto.Id,
            DepartureCountry = dto.DepartureCountry,
            DepartureCity = dto.DepartureCity,
            DepartureDateTime = McpTime.Format(dto.DepartureDateTime),
            DepartureTimezone = dto.DepartureTimezone,
            DepartureIataCode = dto.DepartureIataCode,
            ArrivalCountry = dto.ArrivalCountry,
            ArrivalCity = dto.ArrivalCity,
            ArrivalDateTime = McpTime.Format(dto.ArrivalDateTime),
            ArrivalTimezone = dto.ArrivalTimezone,
            ArrivalIataCode = dto.ArrivalIataCode,
            CountryName = dto.CountryName,
            DurationDays = dto.DurationDays,
            DurationMinutes = minutes,
            DurationHours = minutes / 60
        };
    }
}

public sealed class McpMaxEndDate
{
    [Description("Last day you can still be in the country at midnight while staying at or under the limit, yyyy-MM-dd.")]
    public DateOnly MaxEndDate { get; set; }

    [Description("First day of the rolling window in which daysAtLimit was reached, yyyy-MM-dd. Both ends count.")]
    public DateOnly WindowStart { get; set; }

    [Description("Last day of the rolling window in which daysAtLimit was reached, yyyy-MM-dd.")]
    public DateOnly WindowEnd { get; set; }

    [Description("Midnight or PartialDay for the arrival country.")]
    public string RuleType { get; set; } = string.Empty;

    [Description("rolling365, taxYear, or calendarYear. The limit applies to this window.")]
    public string WindowType { get; set; } = string.Empty;

    [Description("Peak rolling day count for the arrival country inside the window. This can be higher than the count on windowEnd.")]
    public int DaysAtLimit { get; set; }

    public static McpMaxEndDate From(MaxTripEndDateResponseDto dto) => new()
    {
        MaxEndDate = DateOnly.FromDateTime(dto.MaxEndDate),
        WindowStart = dto.WindowStart,
        WindowEnd = dto.WindowEnd,
        RuleType = dto.RuleType,
        WindowType = dto.WindowType,
        DaysAtLimit = dto.DaysAtLimit
    };
}

public sealed class DailyPresenceResponse
{
    [Description("days or runs.")]
    public string Format { get; set; } = "days";

    public List<DailyPresenceDto> Days { get; set; } = [];
    public List<PresenceRun> Runs { get; set; } = [];
}

public sealed class PresenceRun
{
    public string Country { get; set; } = string.Empty;
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public int Days { get; set; }
}

public sealed class RollingCountPoint
{
    public DateOnly Date { get; set; }
    public int RollingDays { get; set; }

    [Description("The day that leaves the 365-day window when Date is included. A counted anniversary means a counted day on Date does not raise the total.")]
    public DateOnly AnniversaryDate { get; set; }

    public bool AnniversaryCounted { get; set; }

    public static RollingCountPoint From(RollingDay day) => new()
    {
        Date = day.Date,
        RollingDays = day.RollingDays,
        AnniversaryDate = day.AnniversaryDate,
        AnniversaryCounted = day.AnniversaryCounted
    };
}

public sealed class McpDurationForecast
{
    public int DurationDays { get; set; }

    [Description(McpTime.LocalTimeDescription)]
    public string EndDate { get; set; } = string.Empty;

    public int TotalDaysInCountry { get; set; }
    public bool ExceedsLimit { get; set; }

    public static McpDurationForecast From(StandardDurationForecastItemDto dto) => new()
    {
        DurationDays = dto.DurationDays,
        EndDate = McpTime.Format(dto.EndDate),
        TotalDaysInCountry = dto.TotalDaysInCountry,
        ExceedsLimit = dto.ExceedsLimit
    };
}

public sealed class McpCountryDays
{
    public string CountryName { get; set; } = string.Empty;
    public int Days { get; set; }

    [Description("Midnight or PartialDay.")]
    public string RuleType { get; set; } = string.Empty;

    [Description("Day count that counts as resident. Countries without a specific rule use 183.")]
    public int ThresholdDays { get; set; }

    public static McpCountryDays From(string countryName, int days, ResidencyCalculationService rules)
    {
        var rule = rules.GetCountryRule(countryName);
        return new McpCountryDays
        {
            CountryName = countryName,
            Days = days,
            RuleType = rule.RuleType == ResidencyRuleType.MidnightRule ? "Midnight" : "PartialDay",
            ThresholdDays = rule.ResidencyThresholdDays ?? 183
        };
    }
}

public sealed class McpLocationOn
{
    public DateOnly Date { get; set; }
    public string LocationAtMidnight { get; set; } = string.Empty;
    public List<string> LocationsDuringDay { get; set; } = [];
    public bool IsInTransitAtMidnight { get; set; }

    [Description("recorded, gapFilled, projected, or noData. projected extends the last arrival country past the last saved day. noData means there is no earlier arrival.")]
    public string Status { get; set; } = string.Empty;

    public static McpLocationOn From(LocationOnDate location) => new()
    {
        Date = location.Date,
        LocationAtMidnight = location.LocationAtMidnight,
        LocationsDuringDay = location.LocationsDuringDay.ToList(),
        IsInTransitAtMidnight = location.IsInTransitAtMidnight,
        Status = location.Status
    };
}

public sealed class McpCurrentLocation
{
    [Description("Country for today. IN_TRANSIT while a saved leg is in the air. Empty when there is no arrival at or before now.")]
    public string Country { get; set; } = string.Empty;

    [Description("recorded, gapFilled, projected, inTransit, or noData.")]
    public string Status { get; set; } = string.Empty;

    public string? LastArrivalIataCode { get; set; }
    public string? LastArrivalCity { get; set; }
    public string? LastArrivalCountry { get; set; }

    [Description("Local arrival date, yyyy-MM-dd, in the airport's timezone.")]
    public DateOnly? LastArrivalDate { get; set; }

    public string? InTransitArrivalIataCode { get; set; }
    public string? InTransitArrivalCity { get; set; }
    public string? InTransitArrivalCountry { get; set; }

    public static McpCurrentLocation From(CurrentLocation location) => new()
    {
        Country = location.Country,
        Status = location.Status,
        LastArrivalIataCode = location.LastArrivalIataCode,
        LastArrivalCity = location.LastArrivalCity,
        LastArrivalCountry = location.LastArrivalCountry,
        LastArrivalDate = location.LastArrivalDate,
        InTransitArrivalIataCode = location.InTransitArrivalIataCode,
        InTransitArrivalCity = location.InTransitArrivalCity,
        InTransitArrivalCountry = location.InTransitArrivalCountry
    };
}

public sealed class McpForecastCountry : ResidencyCountryStatus
{
    [Description("For rolling365, the highest rolling count in the scenario. For taxYear and calendarYear, the count in that period. thresholdStatus uses this count.")]
    public int PeakDays { get; set; }

    [Description("Date the peak was reached, yyyy-MM-dd. For taxYear and calendarYear this is the end of that period.")]
    public DateOnly PeakDate { get; set; }

    public static McpForecastCountry Create(ResidencyCountryStatus status, int endDays, int peakDays, DateOnly peakDate) => new()
    {
        CountryName = status.CountryName,
        TotalDays = endDays,
        RuleType = status.RuleType,
        ThresholdDays = status.ThresholdDays,
        IsApproachingThreshold = status.IsApproachingThreshold,
        DaysUntilThreshold = status.DaysUntilThreshold,
        DaysOverThreshold = status.DaysOverThreshold,
        ThresholdStatus = status.ThresholdStatus,
        IsHomeCountry = status.IsHomeCountry,
        WindowType = status.WindowType,
        WindowStart = status.WindowStart,
        WindowEnd = status.WindowEnd,
        PeakDays = peakDays,
        PeakDate = peakDate
    };
}
