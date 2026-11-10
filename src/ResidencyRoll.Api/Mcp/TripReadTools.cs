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
        TravellerService travellers,
        ClaimsPrincipal user,
        [Description("Include legs that overlap this date (yyyy-MM-dd) or later. Optional.")] string? from = null,
        [Description("Include legs that overlap this date (yyyy-MM-dd) or earlier. Optional.")] string? to = null,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Read);
        var person = await McpTravellers.Resolve(travellers, user, traveller);
        var all = await trips.GetAllTripsAsync(person.OwnerUserId, person);
        DateOnly? fromDate = string.IsNullOrWhiteSpace(from) ? null : TripLegResolver.ParseDate(from, "from");
        DateOnly? toDate = string.IsNullOrWhiteSpace(to) ? null : TripLegResolver.ParseDate(to, "to");
        var selected = all
            .Where(trip => Overlaps(trip.DepartureDateTime, trip.ArrivalDateTime, fromDate, toDate))
            .Select(trip => McpTrip.From(trip.ToDto()))
            .ToList();
        var summary = selected.Count == 0
            ? "No trips."
            : $"{selected.Count} trip{(selected.Count == 1 ? "" : "s")}: " + string.Join("; ", selected.Select(trip => $"{trip.DepartureIataCode ?? trip.DepartureCity} to {trip.ArrivalIataCode ?? trip.ArrivalCity}"));
        return McpResults.For(person, summary, selected);
    }

    [McpServerTool(Name = "get_trip", Title = "Get trip", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(McpTrip))]
    [Description("Returns one flight leg by id. A leg owned by someone else is reported as not found.")]
    public static async Task<CallToolResult> GetTrip(
        TripService trips,
        TravellerService travellers,
        ClaimsPrincipal user,
        [Description("Trip id.")] int id,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Read);
        var person = await McpTravellers.Resolve(travellers, user, traveller);
        var trip = await trips.GetTripByIdAsync(id, person.OwnerUserId, person);
        if (trip is null)
        {
            throw new ModelContextProtocol.McpException("Trip not found.");
        }

        var dto = McpTrip.From(trip.ToDto());
        return McpResults.For(person, $"{dto.DepartureCity} ({dto.DepartureIataCode}) to {dto.ArrivalCity} ({dto.ArrivalIataCode}).", dto);
    }

    [McpServerTool(Name = "get_days_per_country", Title = "Days per country", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(McpCountryDays[]))]
    [Description("Days counted in each country, with ruleType (Midnight or PartialDay) and thresholdDays. window is \"last365\" for the rolling 365 days ending today, or \"all\" for the whole log. " + Semantics)]
    public static async Task<CallToolResult> GetDaysPerCountry(
        TripService trips,
        ResidencyCalculationService rules,
        TravellerService travellers,
        ClaimsPrincipal user,
        [Description("last365 or all.")] string window,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Read);
        var person = await McpTravellers.Resolve(travellers, user, traveller);
        Dictionary<string, int> totals = window switch
        {
            "last365" => await trips.GetDaysPerCountryInLast365DaysAsync(person.OwnerUserId, person),
            "all" => await trips.GetTotalDaysPerCountryAsync(person.OwnerUserId, person),
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
        return McpResults.For(person, summary, rows);
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
    [Description("Days per country for one traveller. Pass traveller, an id or a name, for someone other than the signed-in person; omit it for yourself. Includes days left, days over, and threshold status (under, approaching, reached, over, or home). reached means the count equals the threshold, which counts as residency. over means the count is past it, and daysOverThreshold is then the number of days past. The home country has thresholdStatus home, isApproachingThreshold false, and null days left and days over. When the home country has a minimum presence, such as Canada's 183 calendar-year days for Nova Scotia MSI, the row also includes daysTowardMinimum and minimumStatus. coverageStart and coverageEnd are the part of each window the saved trips actually cover. isComplete is false when the window runs past the records. unaccountedDays is the number of days in that window that no country counts, including a country gap and days before the first saved trip or after the last one. When startDate is omitted, each country uses its own window (rolling365, taxYear, or calendarYear) ending on the end date. " + Semantics)]
    public static async Task<CallToolResult> GetResidencySummary(
        ResidencyQueryService query,
        ResidencyCalculationService rules,
        TravellerService travellers,
        ClaimsPrincipal user,
        [Description("Range start, yyyy-MM-dd. Optional. When omitted, each country uses its own counting window.")] string? startDate = null,
        [Description("Range end, yyyy-MM-dd. Defaults to today. Country windows end on this date when startDate is omitted.")] string? endDate = null,
        [Description("Home country name. Optional. Uses the saved home country when omitted.")] string? homeCountry = null,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Read);
        var person = await McpTravellers.Resolve(travellers, user, traveller);
        var userId = person.OwnerUserId;
        var home = SavedHomeCountry.Resolve(person, homeCountry);
        DateOnly? end = string.IsNullOrWhiteSpace(endDate) ? null : TripLegResolver.ParseDate(endDate, "endDate");
        DateOnly? start = string.IsNullOrWhiteSpace(startDate) ? null : TripLegResolver.ParseDate(startDate, "startDate");
        var reference = end ?? DateOnly.FromDateTime(DateTime.Today);
        var summary = await query.GetResidencySummaryAsync(userId, start, end, person);
        var rows = summary.Select(item => ResidencyCountryStatus.From(item, home)).ToList();
        var coverage = await query.GetRecordCoverageAsync(userId, person);
        if (!string.IsNullOrWhiteSpace(home) && rows.All(row => !row.CountryName.Equals(home.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            var rule = rules.GetCountryRule(home);
            if (rule.MinimumPresenceDays is not null)
            {
                var period = CountingWindow.For(rule, reference);
                rows.Add(ResidencyCountryStatus.From(new ResidencySummaryDto
                {
                    CountryName = rule.CountryName,
                    TotalDays = 0,
                    RuleType = rule.RuleType == ResidencyRuleType.MidnightRule ? "Midnight" : "PartialDay",
                    ThresholdDays = rule.ResidencyThresholdDays ?? 183,
                    WindowType = start is null ? CountingWindow.Name(rule.WindowType) : "range",
                    WindowStart = start ?? period.Start,
                    WindowEnd = start is null ? period.End : reference
                }, home));
            }
        }

        var unaccountedByWindow = new Dictionary<(DateOnly Start, DateOnly End), int>();
        foreach (var row in rows)
        {
            ResidencyCountryStatus.ApplyCoverage(row, coverage);
            var key = (row.WindowStart, row.WindowEnd);
            if (!unaccountedByWindow.TryGetValue(key, out var unaccounted))
            {
                unaccounted = await query.CountUnaccountedDaysAsync(userId, row.WindowStart, row.WindowEnd, person);
                unaccountedByWindow[key] = unaccounted;
            }

            row.UnaccountedDays = unaccounted;
        }

        if (!string.IsNullOrWhiteSpace(home))
        {
            var minimum = await query.MeasureMinimumPresenceAsync(userId, home, reference, person);
            var homeRow = rows.FirstOrDefault(row => row.CountryName.Equals(home.Trim(), StringComparison.OrdinalIgnoreCase));
            if (homeRow is not null && minimum is not null)
            {
                ResidencyCountryStatus.ApplyMinimum(homeRow, minimum);
            }
        }

        var text = rows.Count == 0
            ? "No residency data for the requested window."
            : string.Join(" ", rows.Select(ResidencyCountryStatus.Describe));
        if (home is null)
        {
            text += " No home country is saved. Ask the user, then call set_home_country.";
        }

        return McpResults.For(person, text, rows);
    }

    private static bool Involves(DailyPresenceDto day, string country) =>
        day.LocationAtMidnight.Equals(country.Trim(), StringComparison.OrdinalIgnoreCase)
        || day.LocationsDuringDay.Any(place => place.Equals(country.Trim(), StringComparison.OrdinalIgnoreCase));

    private static List<PresenceRun> PresenceRuns(IReadOnlyList<DailyPresenceDto> days, string? country)
    {
        var selected = days
            .Where(day => !string.IsNullOrWhiteSpace(day.LocationAtMidnight) && day.LocationAtMidnight != "IN_TRANSIT")
            .Where(day => string.IsNullOrWhiteSpace(country) || day.LocationAtMidnight.Equals(country.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(day => day.Date)
            .ToList();
        var runs = new List<PresenceRun>();
        foreach (var day in selected)
        {
            var current = runs.LastOrDefault();
            if (current is not null
                && current.Country.Equals(day.LocationAtMidnight, StringComparison.OrdinalIgnoreCase)
                && day.Date == current.To.AddDays(1))
            {
                current.To = day.Date;
                current.Days++;
                continue;
            }

            runs.Add(new PresenceRun
            {
                Country = day.LocationAtMidnight,
                From = day.Date,
                To = day.Date,
                Days = 1
            });
        }

        return runs;
    }

    [McpServerTool(Name = "get_daily_presence", Title = "Daily presence", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(DailyPresenceResponse))]
    [Description("Midnight location for each day in a range. locationAtMidnight is the country at 00:00 at the start of that date. format \"days\" returns one row per day. format \"runs\" returns contiguous stretches as country, from, to, and days. country limits the rows to that country. At most 366 days per call. Days after the last saved trip are omitted; use get_location_on to project those. " + Semantics)]
    public static async Task<CallToolResult> GetDailyPresence(
        ResidencyQueryService query,
        TravellerService travellers,
        ClaimsPrincipal user,
        [Description("Range start, yyyy-MM-dd.")] string startDate,
        [Description("Range end, yyyy-MM-dd.")] string endDate,
        [Description("Country name. Optional. Limits the rows to that country.")] string? country = null,
        [Description("days or runs. Defaults to days.")] string format = "days",
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Read);
        var person = await McpTravellers.Resolve(travellers, user, traveller);
        var start = TripLegResolver.ParseDate(startDate, "startDate");
        var end = TripLegResolver.ParseDate(endDate, "endDate");
        if (end < start)
        {
            throw new ModelContextProtocol.McpException("endDate is before startDate.");
        }

        var span = end.DayNumber - start.DayNumber + 1;
        if (span > 366)
        {
            throw new ModelContextProtocol.McpException("get_daily_presence returns at most 366 days per call.");
        }

        var presence = await query.GetDailyPresenceAsync(person.OwnerUserId, start, end, person);
        if (!string.IsNullOrWhiteSpace(country))
        {
            presence = presence.Where(day => Involves(day, country)).ToList();
        }

        var mode = string.Equals(format, "runs", StringComparison.OrdinalIgnoreCase) ? "runs" : "days";
        var response = new DailyPresenceResponse { Format = mode };
        if (mode == "runs")
        {
            response.Runs = PresenceRuns(presence, country);
            return McpResults.For(person, $"{response.Runs.Count} runs from {start:yyyy-MM-dd} to {end:yyyy-MM-dd}.", response);
        }

        response.Days = presence;
        return McpResults.For(person, $"{presence.Count} days from {start:yyyy-MM-dd} to {end:yyyy-MM-dd}.", response);
    }

    [McpServerTool(Name = "get_rolling_count", Title = "Rolling day count", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(RollingCountPoint[]))]
    [Description("Rolling 365-day count for one country on each day in a range, counting both ends. This view is always the rolling 365-day window, including when the country's own rule uses a tax year or a calendar year. anniversaryDate is the day that leaves the window when this date is included (365 days earlier). anniversaryCounted means that day counted, so a counted day on this date does not raise the total. " + Semantics)]
    public static async Task<CallToolResult> GetRollingCount(
        ResidencyQueryService query,
        TravellerService travellers,
        ClaimsPrincipal user,
        [Description("Country name.")] string country,
        [Description("Range start, yyyy-MM-dd.")] string from,
        [Description("Range end, yyyy-MM-dd.")] string to,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Read);
        var person = await McpTravellers.Resolve(travellers, user, traveller);
        if (string.IsNullOrWhiteSpace(country))
        {
            throw new ModelContextProtocol.McpException("country is required.");
        }

        var start = TripLegResolver.ParseDate(from, "from");
        var end = TripLegResolver.ParseDate(to, "to");
        if (end < start)
        {
            throw new ModelContextProtocol.McpException("to is before from.");
        }

        if (end.DayNumber - start.DayNumber + 1 > 366)
        {
            throw new ModelContextProtocol.McpException("get_rolling_count returns at most 366 days per call.");
        }

        var rows = (await query.GetRollingCountAsync(person.OwnerUserId, country.Trim(), start, end, traveller: person))
            .Select(RollingCountPoint.From)
            .ToList();
        return McpResults.For(person, $"{rows.Count} days of rolling counts for {country.Trim()}.", rows);
    }

    [McpServerTool(Name = "get_location_on", Title = "Location on a date", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(McpLocationOn))]
    [Description("Where one traveller was on one date. Pass traveller, an id or a name, for someone other than the signed-in person. The home country is saved on that traveller. status is recorded, gapFilled, projected, or noData. A date after the last saved trip is projected from the last arrival country and marked projected. " + Semantics)]
    public static async Task<CallToolResult> GetLocationOn(
        ResidencyQueryService query,
        TravellerService travellers,
        ClaimsPrincipal user,
        [Description("Date, yyyy-MM-dd.")] string date,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Read);
        var person = await McpTravellers.Resolve(travellers, user, traveller);
        var day = TripLegResolver.ParseDate(date, "date");
        var location = await query.GetLocationOnAsync(person.OwnerUserId, day, person);
        var payload = McpLocationOn.From(location);
        var during = payload.LocationsDuringDay.Count == 0 ? "none" : string.Join(", ", payload.LocationsDuringDay);
        var text = location.Status switch
        {
            "noData" => $"No presence recorded for {day:yyyy-MM-dd}, and there is no earlier arrival to project from.",
            "projected" => $"No presence recorded for {day:yyyy-MM-dd}. Projected location is {location.LocationAtMidnight}. Status: projected.",
            _ => $"On {day:yyyy-MM-dd} the midnight location was {location.LocationAtMidnight}. Countries during the day: {during}. In transit at midnight: {(location.IsInTransitAtMidnight ? "yes" : "no")}. Status: {location.Status}."
        };
        return McpResults.For(person, text, payload);
    }

    [McpServerTool(Name = "get_current_location", Title = "Current location", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(McpCurrentLocation))]
    [Description("Country the user is in now, the last arrival airport at or before now, and that arrival's local date. Use the airport as the departure airport when forecasting a future trip. status is recorded, gapFilled, projected, inTransit, or noData. " + Semantics)]
    public static async Task<CallToolResult> GetCurrentLocation(
        ResidencyQueryService query,
        TravellerService travellers,
        ClaimsPrincipal user,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Read);
        var person = await McpTravellers.Resolve(travellers, user, traveller);
        var location = await query.GetCurrentLocationAsync(person.OwnerUserId, person);
        var payload = McpCurrentLocation.From(location);
        if (location.Status == "noData")
        {
            return McpResults.For(person, "No arrival at or before now.", payload);
        }

        var airport = string.IsNullOrWhiteSpace(location.LastArrivalIataCode) ? location.LastArrivalCity : location.LastArrivalIataCode;
        var text = location.Status == "inTransit"
            ? $"In transit toward {location.InTransitArrivalCity} ({location.InTransitArrivalIataCode}), {location.InTransitArrivalCountry}. Last arrival was {airport} in {location.LastArrivalCountry} on {location.LastArrivalDate:yyyy-MM-dd}."
            : $"Current country is {location.Country} ({location.Status}). Last arrival was {airport} in {location.LastArrivalCountry} on {location.LastArrivalDate:yyyy-MM-dd}.";
        return McpResults.For(person, text, payload);
    }

    [McpServerTool(Name = "get_country_rules", Title = "Country residency rules", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(CountryRuleResult[]))]
    [Description("Residency rule for one country, or every known country when country is omitted. Includes ruleType, thresholdDays, windowType (rolling365, taxYear, or calendarYear), and minimumPresenceDays when the home country has one. Unknown countries use the midnight rule, a 183-day threshold, and rolling365. The United Kingdom uses taxYear starting 6 April. Canada has a 183-day calendar-year minimum for Nova Scotia MSI; the log counts days in Canada.")]
    public static Task<CallToolResult> GetCountryRules(
        ResidencyCalculationService rules,
        ClaimsPrincipal user,
        [Description("Country name, such as United Kingdom. Optional.")] string? country = null)
    {
        McpScopes.Require(user, McpScopes.Read);
        if (string.IsNullOrWhiteSpace(country))
        {
            var known = rules.ListCountryRules().Select(rule => CountryRuleResult.From(rule, isDefault: false)).ToList();
            return Task.FromResult(McpResults.Ok(string.Join(" ", known.Select(rule => $"{rule.CountryName}: {rule.RuleType}, threshold {rule.ThresholdDays}, {rule.WindowType}.")), known));
        }

        var match = rules.GetCountryRule(country.Trim());
        var isDefault = !rules.ListCountryRules().Any(rule => string.Equals(rule.CountryName, match.CountryName, StringComparison.OrdinalIgnoreCase));
        var one = CountryRuleResult.From(match, isDefault);
        return Task.FromResult(McpResults.Ok($"{one.CountryName}: {one.RuleType}, threshold {one.ThresholdDays}.", new[] { one }));
    }

    [McpServerTool(Name = "get_record_coverage", Title = "Record coverage", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(RecordCoverageResult))]
    [Description("How far the saved legs reach. firstTripDate and lastTripDate are the earliest and latest local dates on any leg. legCount is the number of saved legs. A summary window that starts before firstTripDate or ends after lastTripDate is incomplete.")]
    public static async Task<CallToolResult> GetRecordCoverage(
        TripService trips,
        TravellerService travellers,
        ClaimsPrincipal user,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Read);
        var person = await McpTravellers.Resolve(travellers, user, traveller);
        var saved = await trips.GetAllTripsAsync(person.OwnerUserId, person);
        DateOnly? first = null;
        DateOnly? last = null;
        foreach (var trip in saved)
        {
            foreach (var date in new[] { DateOnly.FromDateTime(trip.DepartureDateTime), DateOnly.FromDateTime(trip.ArrivalDateTime) })
            {
                if (first is null || date < first)
                {
                    first = date;
                }

                if (last is null || date > last)
                {
                    last = date;
                }
            }
        }

        var result = new RecordCoverageResult { FirstTripDate = first, LastTripDate = last, LegCount = saved.Count };
        var text = saved.Count == 0
            ? "No trips are saved."
            : $"{saved.Count} legs, from {first:yyyy-MM-dd} to {last:yyyy-MM-dd}.";
        return McpResults.For(person, text, result);
    }

    [McpServerTool(Name = "find_suspect_trips", Title = "Find suspect trips", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(SuspectTripsResult))]
    [Description("Checks every saved leg. Warns when arrival is before departure in UTC, a non-stop leg is over 20 hours, the duration is far from the usual time for the timezone difference, a leg overlaps another, or the arrival country does not match the next leg's departure country. Nothing is changed.")]
    public static async Task<CallToolResult> FindSuspectTrips(
        TripService trips,
        TravellerService travellers,
        ClaimsPrincipal user,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Read);
        var person = await McpTravellers.Resolve(travellers, user, traveller);
        var saved = await trips.GetAllTripsAsync(person.OwnerUserId, person);
        var warnings = TripAuditor.InspectAll(saved);
        var text = warnings.Count == 0
            ? $"Checked {saved.Count} legs. None look suspicious."
            : $"{warnings.Count} warning{(warnings.Count == 1 ? "" : "s")}: " + string.Join(" ", warnings.Select(warning => warning.Message));
        return McpResults.For(person, text, new SuspectTripsResult { Warnings = warnings });
    }
}

public class ResidencyCountryStatus
{
    public McpTravellerRef Traveller { get; set; } = new();

    public string CountryName { get; set; } = string.Empty;
    public int TotalDays { get; set; }
    public string RuleType { get; set; } = string.Empty;
    public int ThresholdDays { get; set; }
    [Description("True when a country that is not home is within 30 days of the threshold and still under it. False at and past the threshold.")]
    public bool IsApproachingThreshold { get; set; }

    [Description("Days remaining before the threshold. Zero at or past the threshold. Null when this is the home country.")]
    public int? DaysUntilThreshold { get; set; }

    [Description("Days past the threshold. Zero until the count is greater than the threshold. Null when this is the home country.")]
    public int? DaysOverThreshold { get; set; }

    [Description("under, approaching, reached, over, or home. reached means the count equals the threshold and counts as residency. over means the count is past the threshold.")]
    public string ThresholdStatus { get; set; } = string.Empty;

    public bool IsHomeCountry { get; set; }

    [Description("rolling365, taxYear, calendarYear, or range when the caller set the dates.")]
    public string WindowType { get; set; } = string.Empty;

    public DateOnly WindowStart { get; set; }
    public DateOnly WindowEnd { get; set; }

    [Description("First date in this window that the saved trips cover. Null when the trips do not overlap the window.")]
    public DateOnly? CoverageStart { get; set; }

    [Description("Last date in this window that the saved trips cover. Null when the trips do not overlap the window.")]
    public DateOnly? CoverageEnd { get; set; }

    [Description("False when this window starts before the first saved date or ends after the last saved date. Days outside the covered span are not counted.")]
    public bool IsComplete { get; set; }

    [Description("Days in this window that no country counts. A country gap, or days before the first saved trip and after the last one, leaves these out. Zero when every day in the window counts for at least one country.")]
    public int? UnaccountedDays { get; set; }

    [Description("Days required in the home country, such as 183 for Nova Scotia MSI. Null unless this row is that home country.")]
    public int? MinimumDays { get; set; }

    [Description("calendarYear for Nova Scotia MSI. Null when this country has no minimum.")]
    public string? MinimumWindowType { get; set; }

    public DateOnly? MinimumWindowStart { get; set; }
    public DateOnly? MinimumWindowEnd { get; set; }

    [Description("Days counted toward the home-country minimum in its window.")]
    public int? DaysTowardMinimum { get; set; }

    [Description("Days still needed to meet the minimum. Zero once the minimum is met.")]
    public int? DaysShortOfMinimum { get; set; }

    [Description("under, approaching, or reached. approaching means 30 or fewer days short of the minimum. Null when this country has no minimum.")]
    public string? MinimumStatus { get; set; }

    public static void ApplyCoverage(ResidencyCountryStatus row, RecordCoverage coverage)
    {
        if (coverage.FirstTripDate is not DateOnly first || coverage.LastTripDate is not DateOnly last)
        {
            row.IsComplete = false;
            return;
        }

        var start = row.WindowStart > first ? row.WindowStart : first;
        var end = row.WindowEnd < last ? row.WindowEnd : last;
        if (start > end)
        {
            row.IsComplete = false;
            return;
        }

        row.CoverageStart = start;
        row.CoverageEnd = end;
        row.IsComplete = first <= row.WindowStart && last >= row.WindowEnd;
    }

    public static void ApplyMinimum(ResidencyCountryStatus row, MinimumPresence minimum)
    {
        row.MinimumDays = minimum.MinimumDays;
        row.MinimumWindowType = minimum.WindowType;
        row.MinimumWindowStart = minimum.WindowStart;
        row.MinimumWindowEnd = minimum.WindowEnd;
        row.DaysTowardMinimum = minimum.Days;
        row.DaysShortOfMinimum = minimum.DaysShort;
        row.MinimumStatus = minimum.Status;
    }

    public static ResidencyCountryStatus From(ResidencySummaryDto summary, string? homeCountry)
    {
        var home = !string.IsNullOrWhiteSpace(homeCountry)
            && summary.CountryName.Equals(homeCountry.Trim(), StringComparison.OrdinalIgnoreCase);
        var past = summary.TotalDays > summary.ThresholdDays;
        var reached = summary.TotalDays == summary.ThresholdDays;
        var status = home
            ? "home"
            : past
                ? "over"
                : reached
                    ? "reached"
                    : summary.IsApproachingThreshold ? "approaching" : "under";
        return new ResidencyCountryStatus
        {
            CountryName = summary.CountryName,
            TotalDays = summary.TotalDays,
            RuleType = summary.RuleType,
            ThresholdDays = summary.ThresholdDays,
            IsApproachingThreshold = !home && !past && !reached && summary.IsApproachingThreshold,
            DaysUntilThreshold = home ? null : past || reached ? 0 : summary.DaysUntilThreshold,
            DaysOverThreshold = home ? null : past ? summary.TotalDays - summary.ThresholdDays : 0,
            ThresholdStatus = status,
            IsHomeCountry = home,
            WindowType = summary.WindowType,
            WindowStart = summary.WindowStart,
            WindowEnd = summary.WindowEnd
        };
    }

    internal static string Describe(ResidencyCountryStatus row)
    {
        var text = row.IsHomeCountry
            ? $"{row.CountryName}: {row.TotalDays} days, home, {row.WindowType} {row.WindowStart:yyyy-MM-dd} to {row.WindowEnd:yyyy-MM-dd}."
            : $"{row.CountryName}: {row.TotalDays} days, {row.ThresholdStatus}, {row.DaysUntilThreshold} left, {row.DaysOverThreshold} over, {row.WindowType} {row.WindowStart:yyyy-MM-dd} to {row.WindowEnd:yyyy-MM-dd}.";
        if (row.MinimumStatus is not null)
        {
            text += $" Minimum {row.DaysTowardMinimum} of {row.MinimumDays} in {row.MinimumWindowType} {row.MinimumWindowStart:yyyy-MM-dd} to {row.MinimumWindowEnd:yyyy-MM-dd}, {row.MinimumStatus}, {row.DaysShortOfMinimum} short.";
        }

        if (!row.IsComplete)
        {
            text += row.CoverageStart is DateOnly start && row.CoverageEnd is DateOnly end
                ? $" Records cover {start:yyyy-MM-dd} to {end:yyyy-MM-dd}."
                : " No saved trips cover this window.";
        }

        if (row.UnaccountedDays is int unaccounted && unaccounted > 0)
        {
            var span = row.WindowEnd.DayNumber - row.WindowStart.DayNumber + 1;
            text += $" {unaccounted} of {span} days in this window are not counted for any country.";
        }

        return text;
    }
}

public sealed class CountryRuleResult
{
    public string CountryName { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public int ThresholdDays { get; set; }
    public bool HasTransitException { get; set; }
    public bool IsDefaultRule { get; set; }

    [Description("rolling365, taxYear, or calendarYear.")]
    public string WindowType { get; set; } = string.Empty;

    [Description("Month the tax year starts. Null unless windowType is taxYear.")]
    public int? TaxYearStartMonth { get; set; }

    [Description("Day the tax year starts. Null unless windowType is taxYear.")]
    public int? TaxYearStartDay { get; set; }

    [Description("Days the home country must contain in minimumWindowType. 183 for Canada, which is how Nova Scotia MSI is counted here. Null when there is no minimum.")]
    public int? MinimumPresenceDays { get; set; }

    [Description("calendarYear when a home-country minimum applies. Null otherwise.")]
    public string? MinimumWindowType { get; set; }

    public static CountryRuleResult From(CountryResidencyRule rule, bool isDefault) => new()
    {
        CountryName = rule.CountryName,
        CountryCode = rule.CountryCode,
        RuleType = rule.RuleType == ResidencyRuleType.MidnightRule ? "Midnight" : "PartialDay",
        ThresholdDays = rule.ResidencyThresholdDays ?? 183,
        HasTransitException = rule.HasTransitException,
        IsDefaultRule = isDefault,
        WindowType = Services.CountingWindow.Name(rule.WindowType),
        TaxYearStartMonth = rule.TaxYearStartMonth,
        TaxYearStartDay = rule.TaxYearStartDay,
        MinimumPresenceDays = rule.MinimumPresenceDays,
        MinimumWindowType = rule.MinimumWindowType is CountingWindowType window ? Services.CountingWindow.Name(window) : null
    };
}

public sealed class SuspectTripsResult
{
    public McpTravellerRef Traveller { get; set; } = new();
    public List<TripWarning> Warnings { get; set; } = [];
}

public sealed class RecordCoverageResult
{
    public McpTravellerRef Traveller { get; set; } = new();

    public DateOnly? FirstTripDate { get; set; }
    public DateOnly? LastTripDate { get; set; }
    public int LegCount { get; set; }
}
