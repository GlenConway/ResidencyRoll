using System.ComponentModel;
using System.Security.Claims;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ResidencyRoll.Api.Mappings;
using ResidencyRoll.Api.Models;
using ResidencyRoll.Api.Services;
using ResidencyRoll.Shared.Trips;

namespace ResidencyRoll.Api.Mcp;

[McpServerToolType]
public sealed class TripReadTools
{
    private const string Semantics = McpSemantics.Rules;

    [McpServerTool(Name = "list_trips", Title = "List trips", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(McpTrip[]))]
    [Description("Lists the signed-in user's flight legs, optionally overlapping a date range. Leg times are local wall-clock times with no offset. " + Semantics)]
    public static async Task<CallToolResult> ListTrips(
        TripService trips,
        ClaimsPrincipal user,
        [Description("Include legs that overlap this date (yyyy-MM-dd) or later. Optional.")] string? from = null,
        [Description("Include legs that overlap this date (yyyy-MM-dd) or earlier. Optional.")] string? to = null)
    {
        McpScopes.Require(user, McpScopes.Read);
        var userId = McpScopes.UserId(user);
        var all = await trips.GetAllTripsAsync(userId);
        DateOnly? fromDate = string.IsNullOrWhiteSpace(from) ? null : TripLegResolver.ParseDate(from, "from");
        DateOnly? toDate = string.IsNullOrWhiteSpace(to) ? null : TripLegResolver.ParseDate(to, "to");
        var selected = all
            .Where(trip => Overlaps(trip.DepartureDateTime, trip.ArrivalDateTime, fromDate, toDate))
            .Select(trip => McpTrip.From(trip.ToDto()))
            .ToList();
        var summary = selected.Count == 0
            ? "No trips."
            : $"{selected.Count} trip{(selected.Count == 1 ? "" : "s")}: " + string.Join("; ", selected.Select(trip => $"{trip.DepartureIataCode ?? trip.DepartureCity} to {trip.ArrivalIataCode ?? trip.ArrivalCity}"));
        return McpResults.Ok(summary, selected);
    }

    [McpServerTool(Name = "get_trip", Title = "Get trip", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(McpTrip))]
    [Description("Returns one flight leg by id. A leg owned by someone else is reported as not found.")]
    public static async Task<CallToolResult> GetTrip(
        TripService trips,
        ClaimsPrincipal user,
        [Description("Trip id.")] int id)
    {
        McpScopes.Require(user, McpScopes.Read);
        var trip = await trips.GetTripByIdAsync(id, McpScopes.UserId(user));
        if (trip is null)
        {
            throw new ModelContextProtocol.McpException("Trip not found.");
        }

        var dto = McpTrip.From(trip.ToDto());
        return McpResults.Ok($"{dto.DepartureCity} ({dto.DepartureIataCode}) to {dto.ArrivalCity} ({dto.ArrivalIataCode}).", dto);
    }

    [McpServerTool(Name = "get_days_per_country", Title = "Days per country", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(McpCountryDays[]))]
    [Description("Days counted in each country, with ruleType (Midnight or PartialDay) and thresholdDays. window is \"last365\" for the rolling 365 days ending today, or \"all\" for the whole log. " + Semantics)]
    public static async Task<CallToolResult> GetDaysPerCountry(
        TripService trips,
        ResidencyCalculationService rules,
        ClaimsPrincipal user,
        [Description("last365 or all.")] string window)
    {
        McpScopes.Require(user, McpScopes.Read);
        var userId = McpScopes.UserId(user);
        Dictionary<string, int> totals = window switch
        {
            "last365" => await trips.GetDaysPerCountryInLast365DaysAsync(userId),
            "all" => await trips.GetTotalDaysPerCountryAsync(userId),
            _ => throw new ModelContextProtocol.McpException("window must be \"last365\" or \"all\".")
        };

        var rows = totals
            .Select(pair => McpCountryDays.From(pair.Key, pair.Value, rules))
            .OrderByDescending(row => row.Days)
            .ThenBy(row => row.CountryName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var summary = rows.Count == 0
            ? $"No days recorded ({window})."
            : string.Join(" ", rows.Select(row => $"{row.CountryName}: {row.Days} days, {row.RuleType}, threshold {row.ThresholdDays}."));
        return McpResults.Ok(summary, rows);
    }

    private static bool Overlaps(DateTime departure, DateTime arrival, DateOnly? from, DateOnly? to)
    {
        var start = DateOnly.FromDateTime(departure <= arrival ? departure : arrival);
        var end = DateOnly.FromDateTime(departure <= arrival ? arrival : departure);
        if (to is { } toDate && start > toDate)
        {
            return false;
        }

        if (from is { } fromDate && end < fromDate)
        {
            return false;
        }

        return true;
    }
}

[McpServerToolType]
public sealed class ResidencyTools
{
    private const string Semantics = McpSemantics.Rules + McpSemantics.HomeCountry;

    [McpServerTool(Name = "get_residency_summary", Title = "Residency summary", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(ResidencyCountryStatus[]))]
    [Description("Days per country between two dates, plus days left, days over, and threshold status (under, approaching, over, or home). The home country has thresholdStatus home, isApproachingThreshold false, and null days left and days over. Defaults to the 365 days ending on the end date, counting both ends. " + Semantics)]
    public static async Task<CallToolResult> GetResidencySummary(
        ResidencyQueryService query,
        UserProfileService profiles,
        ClaimsPrincipal user,
        [Description("Range start, yyyy-MM-dd. Defaults to 364 days before the end date, so both ends count and the range is 365 days.")] string? startDate = null,
        [Description("Range end, yyyy-MM-dd. Defaults to today.")] string? endDate = null,
        [Description("Home country name. Optional. Uses the saved home country when omitted.")] string? homeCountry = null)
    {
        McpScopes.Require(user, McpScopes.Read);
        var userId = McpScopes.UserId(user);
        var home = await SavedHomeCountry.Resolve(profiles, userId, homeCountry);
        var end = string.IsNullOrWhiteSpace(endDate) ? DateOnly.FromDateTime(DateTime.Today) : TripLegResolver.ParseDate(endDate, "endDate");
        var start = string.IsNullOrWhiteSpace(startDate) ? RollingWindow.StartOn(end) : TripLegResolver.ParseDate(startDate, "startDate");
        var summary = await query.GetResidencySummaryAsync(userId, start, end);
        var rows = summary.Select(item => ResidencyCountryStatus.From(item, home)).ToList();
        var text = rows.Count == 0
            ? $"No residency data from {start:yyyy-MM-dd} to {end:yyyy-MM-dd}."
            : string.Join(" ", rows.Select(ResidencyCountryStatus.Describe));
        if (home is null)
        {
            text += " No home country is saved. Ask the user, then call set_home_country.";
        }

        return McpResults.Ok(text, rows);
    }

    [McpServerTool(Name = "get_daily_presence", Title = "Daily presence", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(DailyPresenceDto[]))]
    [Description("Midnight location, countries visited, and transit flag for each day in a range. At most 366 days per call. Days after the last saved trip are omitted; use get_location_on to project those. " + Semantics)]
    public static async Task<CallToolResult> GetDailyPresence(
        ResidencyQueryService query,
        ClaimsPrincipal user,
        [Description("Range start, yyyy-MM-dd.")] string startDate,
        [Description("Range end, yyyy-MM-dd.")] string endDate)
    {
        McpScopes.Require(user, McpScopes.Read);
        var start = TripLegResolver.ParseDate(startDate, "startDate");
        var end = TripLegResolver.ParseDate(endDate, "endDate");
        if (end < start)
        {
            throw new ModelContextProtocol.McpException("endDate is before startDate.");
        }

        var days = end.DayNumber - start.DayNumber + 1;
        if (days > 366)
        {
            throw new ModelContextProtocol.McpException("get_daily_presence returns at most 366 days per call.");
        }

        var presence = await query.GetDailyPresenceAsync(McpScopes.UserId(user), start, end);
        return McpResults.Ok($"{presence.Count} days from {start:yyyy-MM-dd} to {end:yyyy-MM-dd}.", presence);
    }

    [McpServerTool(Name = "get_location_on", Title = "Location on a date", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(McpLocationOn))]
    [Description("Where the user was on one date. status is recorded, gapFilled, projected, or noData. A date after the last saved trip is projected from the last arrival country and marked projected. " + Semantics)]
    public static async Task<CallToolResult> GetLocationOn(
        ResidencyQueryService query,
        ClaimsPrincipal user,
        [Description("Date, yyyy-MM-dd.")] string date)
    {
        McpScopes.Require(user, McpScopes.Read);
        var day = TripLegResolver.ParseDate(date, "date");
        var location = await query.GetLocationOnAsync(McpScopes.UserId(user), day);
        var payload = McpLocationOn.From(location);
        var during = payload.LocationsDuringDay.Count == 0 ? "none" : string.Join(", ", payload.LocationsDuringDay);
        var text = location.Status switch
        {
            "noData" => $"No presence recorded for {day:yyyy-MM-dd}, and there is no earlier arrival to project from.",
            "projected" => $"No presence recorded for {day:yyyy-MM-dd}. Projected location is {location.LocationAtMidnight}. Status: projected.",
            _ => $"On {day:yyyy-MM-dd} the midnight location was {location.LocationAtMidnight}. Countries during the day: {during}. In transit at midnight: {(location.IsInTransitAtMidnight ? "yes" : "no")}. Status: {location.Status}."
        };
        return McpResults.Ok(text, payload);
    }

    [McpServerTool(Name = "get_current_location", Title = "Current location", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(McpCurrentLocation))]
    [Description("Country the user is in now, the last arrival airport at or before now, and that arrival's local date. Use the airport as the departure airport when forecasting a future trip. status is recorded, gapFilled, projected, inTransit, or noData. " + Semantics)]
    public static async Task<CallToolResult> GetCurrentLocation(
        ResidencyQueryService query,
        ClaimsPrincipal user)
    {
        McpScopes.Require(user, McpScopes.Read);
        var location = await query.GetCurrentLocationAsync(McpScopes.UserId(user));
        var payload = McpCurrentLocation.From(location);
        if (location.Status == "noData")
        {
            return McpResults.Ok("No arrival at or before now.", payload);
        }

        var airport = string.IsNullOrWhiteSpace(location.LastArrivalIataCode) ? location.LastArrivalCity : location.LastArrivalIataCode;
        var text = location.Status == "inTransit"
            ? $"In transit toward {location.InTransitArrivalCity} ({location.InTransitArrivalIataCode}), {location.InTransitArrivalCountry}. Last arrival was {airport} in {location.LastArrivalCountry} on {location.LastArrivalDate:yyyy-MM-dd}."
            : $"Current country is {location.Country} ({location.Status}). Last arrival was {airport} in {location.LastArrivalCountry} on {location.LastArrivalDate:yyyy-MM-dd}.";
        return McpResults.Ok(text, payload);
    }

    [McpServerTool(Name = "get_country_rules", Title = "Country residency rules", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(CountryRuleResult[]))]
    [Description("Residency rule for one country, or every known country when country is omitted. Unknown countries use the midnight rule and a 183-day threshold.")]
    public static Task<CallToolResult> GetCountryRules(
        ResidencyCalculationService rules,
        ClaimsPrincipal user,
        [Description("Country name, such as United Kingdom. Optional.")] string? country = null)
    {
        McpScopes.Require(user, McpScopes.Read);
        if (string.IsNullOrWhiteSpace(country))
        {
            var known = rules.ListCountryRules().Select(rule => CountryRuleResult.From(rule, isDefault: false)).ToList();
            return Task.FromResult(McpResults.Ok(string.Join(" ", known.Select(rule => $"{rule.CountryName}: {rule.RuleType}, threshold {rule.ThresholdDays}.")), known));
        }

        var match = rules.GetCountryRule(country.Trim());
        var isDefault = !rules.ListCountryRules().Any(rule => string.Equals(rule.CountryName, match.CountryName, StringComparison.OrdinalIgnoreCase));
        var one = CountryRuleResult.From(match, isDefault);
        return Task.FromResult(McpResults.Ok($"{one.CountryName}: {one.RuleType}, threshold {one.ThresholdDays}.", new[] { one }));
    }
}

public class ResidencyCountryStatus
{
    public string CountryName { get; set; } = string.Empty;
    public int TotalDays { get; set; }
    public string RuleType { get; set; } = string.Empty;
    public int ThresholdDays { get; set; }
    [Description("True when a country that is not home is within 30 days of the threshold and still under it.")]
    public bool IsApproachingThreshold { get; set; }

    [Description("Days remaining before the threshold. Null when this is the home country.")]
    public int? DaysUntilThreshold { get; set; }

    [Description("Days past the threshold. Null when this is the home country.")]
    public int? DaysOverThreshold { get; set; }

    public string ThresholdStatus { get; set; } = string.Empty;
    public bool IsHomeCountry { get; set; }

    public static ResidencyCountryStatus From(ResidencySummaryDto summary, string? homeCountry)
    {
        var home = !string.IsNullOrWhiteSpace(homeCountry)
            && summary.CountryName.Equals(homeCountry.Trim(), StringComparison.OrdinalIgnoreCase);
        var over = summary.TotalDays >= summary.ThresholdDays;
        var status = home ? "home" : over ? "over" : summary.IsApproachingThreshold ? "approaching" : "under";
        return new ResidencyCountryStatus
        {
            CountryName = summary.CountryName,
            TotalDays = summary.TotalDays,
            RuleType = summary.RuleType,
            ThresholdDays = summary.ThresholdDays,
            IsApproachingThreshold = !home && summary.IsApproachingThreshold,
            DaysUntilThreshold = home ? null : summary.DaysUntilThreshold,
            DaysOverThreshold = home ? null : Math.Max(0, summary.TotalDays - summary.ThresholdDays),
            ThresholdStatus = status,
            IsHomeCountry = home
        };
    }

    internal static string Describe(ResidencyCountryStatus row) =>
        row.IsHomeCountry
            ? $"{row.CountryName}: {row.TotalDays} days, home."
            : $"{row.CountryName}: {row.TotalDays} days, {row.ThresholdStatus}, {row.DaysUntilThreshold} left, {row.DaysOverThreshold} over.";
}

public sealed class CountryRuleResult
{
    public string CountryName { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public int ThresholdDays { get; set; }
    public bool HasTransitException { get; set; }
    public bool IsDefaultRule { get; set; }

    public static CountryRuleResult From(CountryResidencyRule rule, bool isDefault) => new()
    {
        CountryName = rule.CountryName,
        CountryCode = rule.CountryCode,
        RuleType = rule.RuleType == ResidencyRuleType.MidnightRule ? "Midnight" : "PartialDay",
        ThresholdDays = rule.ResidencyThresholdDays ?? 183,
        HasTransitException = rule.HasTransitException,
        IsDefaultRule = isDefault
    };
}
