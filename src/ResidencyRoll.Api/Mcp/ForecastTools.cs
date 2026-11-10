using System.ComponentModel;
using System.Security.Claims;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ResidencyRoll.Api.Models;
using ResidencyRoll.Api.Services;
using ResidencyRoll.Shared.Trips;

namespace ResidencyRoll.Api.Mcp;

[McpServerToolType]
public sealed class ForecastTools
{
    private const string Semantics = McpSemantics.Rules + McpSemantics.Forecast + McpSemantics.HomeCountry;

    [McpServerTool(Name = "forecast_trips", Title = "Forecast trips", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(ForecastToolResult))]
    [Description("Projects days per country if the hypothetical legs were added. Nothing is saved. Pass traveller, an id or a name, for someone other than the signed-in person; omit it for yourself. windowStart and windowEnd bound the scenario. Each country is counted on its own window (rolling365, taxYear, or calendarYear) and includes that window, totalDays, peakDays, peakDate, and thresholdStatus (under, approaching, reached, over, or home). reached means the count equals the threshold. Both lists are sorted by days, descending. country limits both lists to one country and adds rollingAroundPeak. through extends the scenario through that date so saved trips after the last leg are included. homeCalendarYears is the home country's count in each calendar year the plan touches. A year that runs past the last leg assumes you remain in the last arrival country through 31 December, and projectedFrom names the first assumed day. When that country has a minimum, such as Canada's 183 days for Nova Scotia MSI, each year includes minimumStatus. Pass IATA codes and local date-times (yyyy-MM-ddTHH:mm). City, country, and IANA timezone override the airport table and are required for unknown airports. " + Semantics)]
    public static async Task<CallToolResult> ForecastTrips(
        ResidencyQueryService query,
        ResidencyCalculationService rules,
        AirportCatalog airports,
        TravellerService travellerService,
        ClaimsPrincipal user,
        [Description("Hypothetical flight legs, in order.")] List<McpTripLeg> trips,
        [Description("Home country name. Optional. Uses the saved home country when omitted.")] string? homeCountry = null,
        [Description("Country name. Optional. Returns only this country, plus rollingAroundPeak.")] string? country = null,
        [Description("Extend the scenario through this date, yyyy-MM-dd. Optional. Includes saved trips after the last hypothetical leg.")] string? through = null,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Read);
        var person = await McpTravellers.Resolve(travellerService, user, traveller);
        var userId = person.OwnerUserId;
        var home = SavedHomeCountry.Resolve(person, homeCountry);
        var legs = (trips ?? []).Select(leg => TripLegResolver.Resolve(leg, userId, airports)).ToList();
        DateOnly? throughDate = string.IsNullOrWhiteSpace(through) ? null : TripLegResolver.ParseDate(through, "through");
        var forecast = await query.ForecastBreakdownAsync(userId, legs, throughDate, person);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var current = forecast.Current
            .Select(pair => ToStatus(pair.Key, pair.Value, rules, home, today))
            .OrderByDescending(row => row.TotalDays)
            .ThenBy(row => row.CountryName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var forecastRows = ForecastRows(forecast, rules, home);
        if (!string.IsNullOrWhiteSpace(country))
        {
            current = current.Where(row => row.CountryName.Equals(country.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            forecastRows = forecastRows.Where(row => row.CountryName.Equals(country.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        }
        int? awayCurrent = null;
        int? awayForecast = null;
        if (home is not null)
        {
            awayCurrent = DaysAway(current, home);
            awayForecast = DaysAway(forecastRows, home);
        }

        var result = new ForecastToolResult
        {
            WindowStart = forecast.WindowStart,
            WindowEnd = forecast.WindowEnd,
            Current = current,
            Forecast = forecastRows,
            HomeCountry = home,
            DaysAwayCurrent = awayCurrent,
            DaysAwayForecast = awayForecast,
            RollingAroundPeak = await RollingAroundPeak(query, userId, legs, country, forecastRows, person),
            HomeCalendarYears = string.IsNullOrWhiteSpace(home)
                ? []
                : (await query.CountCalendarYearsAsync(userId, home, legs, throughDate, person))
                    .Select(HomeCalendarYear.From)
                    .ToList()
        };
        var text = $"Forecast window {forecast.WindowStart:yyyy-MM-dd} to {forecast.WindowEnd:yyyy-MM-dd}. "
            + SummarizeCurrent(current) + " " + SummarizeForecast(forecastRows);
        text += home is null
            ? " No home country is saved, so days away from home are not calculated. Ask the user, then call set_home_country."
            : $" Days away from {home} at the end of each window: current {awayCurrent}, forecast {awayForecast}.";
        if (result.HomeCalendarYears.Count > 0)
        {
            text += " " + string.Join(" ", result.HomeCalendarYears.Select(DescribeYear));
        }

        return McpResults.For(person, text, result);
    }

    [McpServerTool(Name = "max_trip_end_date", Title = "Latest trip end under the day limit", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(McpMaxEndDate))]
    [Description("Last day the chosen traveller can still be in the arrival country at midnight while that country's count stays at or under dayLimit (default 183). Pass traveller, an id or a name, for someone other than the signed-in person. The home country is saved on that traveller. The count uses the country's window (rolling365, taxYear, or calendarYear). maxEndDate is that date (yyyy-MM-dd), not a departure time. daysAtLimit is the count at that limit. trips are unsaved legs included before the stay. Reaching the limit counts as residency. " + Semantics)]
    public static async Task<CallToolResult> MaxTripEndDate(
        ResidencyQueryService query,
        AirportCatalog airports,
        TravellerService travellerService,
        ClaimsPrincipal user,
        [Description("Local arrival date-time in the destination, yyyy-MM-ddTHH:mm.")] string tripStart,
        [Description("Day limit for the arrival country. Defaults to 183. Reaching the limit counts as residency.")] int dayLimit = 183,
        [Description("Unsaved legs to include before the stay, same shape as forecast_trips. Optional.")] List<McpTripLeg>? trips = null,
        string? departureIataCode = null,
        string? departureCity = null,
        string? departureCountry = null,
        string? departureTimezone = null,
        string? arrivalIataCode = null,
        string? arrivalCity = null,
        string? arrivalCountry = null,
        string? arrivalTimezone = null,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Read);
        var person = await McpTravellers.Resolve(travellerService, user, traveller);
        var userId = person.OwnerUserId;
        var request = StayRequest(airports, tripStart, dayLimit, departureIataCode, departureCity, departureCountry, departureTimezone, arrivalIataCode, arrivalCity, arrivalCountry, arrivalTimezone);
        var extra = ResolveLegs(trips, userId, airports);
        var result = await query.CalculateMaxEndDateAsync(userId, request, extra, person);
        var payload = McpMaxEndDate.From(result);
        return McpResults.For(
            person,
            $"Last midnight still in {request.ArrivalCountry} is {payload.MaxEndDate:yyyy-MM-dd} ({payload.RuleType}), with a peak of {payload.DaysAtLimit} days in the window {payload.WindowStart:yyyy-MM-dd} to {payload.WindowEnd:yyyy-MM-dd}, against a limit of {request.DayLimit}.",
            payload);
    }

    [McpServerTool(Name = "standard_duration_forecasts", Title = "Standard duration forecasts", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(McpDurationForecast[]))]
    [Description("Days in the arrival country for a set of stay lengths. Durations default to 7, 14, 30, 60, 90, and 180 days. The count uses the country's window. trips are unsaved legs included before the stay. " + Semantics)]
    public static async Task<CallToolResult> StandardDurationForecasts(
        ResidencyQueryService query,
        AirportCatalog airports,
        TravellerService travellerService,
        ClaimsPrincipal user,
        [Description("Local arrival date-time in the destination, yyyy-MM-ddTHH:mm.")] string tripStart,
        [Description("Day limit for the arrival country. Defaults to 183. Reaching the limit counts as residency.")] int dayLimit = 183,
        [Description("Stay lengths in days. Optional.")] int[]? durations = null,
        [Description("Unsaved legs to include before the stay, same shape as forecast_trips. Optional.")] List<McpTripLeg>? trips = null,
        string? departureIataCode = null,
        string? departureCity = null,
        string? departureCountry = null,
        string? departureTimezone = null,
        string? arrivalIataCode = null,
        string? arrivalCity = null,
        string? arrivalCountry = null,
        string? arrivalTimezone = null,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Read);
        var person = await McpTravellers.Resolve(travellerService, user, traveller);
        var userId = person.OwnerUserId;
        var stay = StayRequest(airports, tripStart, dayLimit, departureIataCode, departureCity, departureCountry, departureTimezone, arrivalIataCode, arrivalCity, arrivalCountry, arrivalTimezone);
        var extra = ResolveLegs(trips, userId, airports);
        var request = new StandardDurationForecastRequestDto
        {
            DepartureCountry = stay.DepartureCountry,
            DepartureCity = stay.DepartureCity,
            DepartureTimezone = stay.DepartureTimezone,
            DepartureIataCode = stay.DepartureIataCode,
            ArrivalCountry = stay.ArrivalCountry,
            ArrivalCity = stay.ArrivalCity,
            ArrivalTimezone = stay.ArrivalTimezone,
            ArrivalIataCode = stay.ArrivalIataCode,
            TripStart = stay.TripStart,
            DayLimit = stay.DayLimit,
            Durations = durations
        };
        var rows = (await query.GetStandardDurationForecastsAsync(userId, request, extra, person))
            .Select(McpDurationForecast.From)
            .ToList();
        var text = string.Join(" ", rows.Select(row => $"{row.DurationDays} days ends {row.EndDate} with {row.TotalDaysInCountry} in {stay.ArrivalCountry}{(row.ExceedsLimit ? " (over the limit)" : "")}."));
        return McpResults.For(person, text, rows);
    }

    [McpServerTool(Name = "lookup_airport", Title = "Look up an airport", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(AirportMatch[]))]
    [Description("Searches the shared airport table by IATA code, city, airport name, or country. Use the result's city, country, and IANA timezone on trip legs.")]
    public static Task<CallToolResult> LookupAirport(
        AirportCatalog airports,
        ClaimsPrincipal user,
        [Description("IATA code, city, airport name, or country.")] string query)
    {
        McpScopes.Require(user, McpScopes.Read);
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new McpException("query is required.");
        }

        var matches = airports.Search(query, limit: 10)
            .Select(airport => new AirportMatch
            {
                IataCode = airport.IataCode,
                AirportName = airport.AirportName,
                City = airport.City,
                Country = airport.Country,
                IanaTimezone = airport.IanaTimezone
            })
            .ToList();
        var text = matches.Count == 0
            ? $"No airport matched '{query}'. Pass city, country, and timezone explicitly."
            : string.Join("; ", matches.Select(airport => $"{airport.IataCode} {airport.City}, {airport.Country} ({airport.IanaTimezone})"));
        return Task.FromResult(McpResults.Ok(text, matches));
    }

    [McpServerTool(Name = "max_trip_start_date", Title = "Earliest arrival before a fixed departure", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(McpEarliestArrival))]
    [Description("Earliest arrival that keeps the arrival country at or under dayLimit when you leave on a fixed date. earliestArrival is that date (yyyy-MM-dd). The limit uses the country's window. trips are unsaved legs included before the stay. " + Semantics)]
    public static async Task<CallToolResult> MaxTripStartDate(
        ResidencyQueryService query,
        AirportCatalog airports,
        TravellerService travellerService,
        ClaimsPrincipal user,
        [Description("Local departure date-time from the destination, yyyy-MM-ddTHH:mm.")] string tripEnd,
        [Description("Day limit for the arrival country. Defaults to 183. Reaching the limit counts as residency.")] int dayLimit = 183,
        [Description("Unsaved legs to include, same shape as forecast_trips. Optional.")] List<McpTripLeg>? trips = null,
        string? departureIataCode = null,
        string? departureCity = null,
        string? departureCountry = null,
        string? departureTimezone = null,
        string? arrivalIataCode = null,
        string? arrivalCity = null,
        string? arrivalCountry = null,
        string? arrivalTimezone = null,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Read);
        var person = await McpTravellers.Resolve(travellerService, user, traveller);
        var userId = person.OwnerUserId;
        var request = StayRequest(airports, tripEnd, dayLimit, departureIataCode, departureCity, departureCountry, departureTimezone, arrivalIataCode, arrivalCity, arrivalCountry, arrivalTimezone);
        var departure = TripLegResolver.ParseLocal(tripEnd, "tripEnd");
        var result = await query.CalculateEarliestArrivalAsync(userId, request, departure, ResolveLegs(trips, userId, airports), person);
        var payload = new McpEarliestArrival
        {
            EarliestArrival = DateOnly.FromDateTime(result.Arrival),
            WindowStart = result.WindowStart,
            WindowEnd = result.WindowEnd,
            RuleType = result.RuleType,
            WindowType = result.WindowType,
            DaysAtLimit = result.DaysAtLimit
        };
        return McpResults.For(
            person,
            $"Earliest arrival in {request.ArrivalCountry} before leaving on {departure:yyyy-MM-dd} is {payload.EarliestArrival:yyyy-MM-dd} ({payload.RuleType}, {payload.WindowType}), with {payload.DaysAtLimit} days against a limit of {request.DayLimit}.",
            payload);
    }

    [McpServerTool(Name = "plan_max_presence", Title = "Most days under the limit", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(PresencePlanResult))]
    [Description("Arrival and departure dates that add the most days in a country while the country's count stays at or under dayLimit. It fills the earliest open days, then later days once earlier ones roll out of a rolling window. departure is the last midnight you are still there. blackoutRanges are dates you must be elsewhere, such as Christmas at home. Saved trips are included. Reaching dayLimit counts as residency, so pass 182 to stay short of a 183-day test. " + Semantics)]
    public static async Task<CallToolResult> PlanMaxPresence(
        ResidencyQueryService query,
        TravellerService travellerService,
        ClaimsPrincipal user,
        [Description("Country to maximize.")] string country,
        [Description("First date you could be there, yyyy-MM-dd.")] string from,
        [Description("Last date you could be there, yyyy-MM-dd.")] string to,
        [Description("Dates you cannot be in the country. Optional.")] List<DateSpan>? blackoutRanges = null,
        [Description("Maximum days in the country's window. Defaults to 183.")] int dayLimit = 183,
        [Description(McpTravellers.ParameterDescription)] string? traveller = null)
    {
        McpScopes.Require(user, McpScopes.Read);
        var person = await McpTravellers.Resolve(travellerService, user, traveller);
        if (string.IsNullOrWhiteSpace(country))
        {
            throw new McpException("country is required.");
        }

        var start = TripLegResolver.ParseDate(from, "from");
        var end = TripLegResolver.ParseDate(to, "to");
        if (end < start)
        {
            throw new McpException("to is before from.");
        }

        var blackouts = (blackoutRanges ?? [])
            .Select(range => (TripLegResolver.ParseDate(range.From, "blackout from"), TripLegResolver.ParseDate(range.To, "blackout to")))
            .ToList();
        var plan = await query.PlanMaxPresenceAsync(person.OwnerUserId, country.Trim(), start, end, blackouts, dayLimit, person);
        var payload = new PresencePlanResult
        {
            Country = plan.Country,
            DayLimit = plan.DayLimit,
            PlannedDays = plan.PlannedDays,
            PeakDays = plan.PeakDays,
            Stays = plan.Stays.Select(stay => new PlannedStayResult
            {
                Arrival = stay.Arrival,
                Departure = stay.Departure,
                Days = stay.Days
            }).ToList()
        };
        var stays = payload.Stays.Count == 0
            ? "no stays"
            : string.Join("; ", payload.Stays.Select(stay => $"{stay.Arrival:yyyy-MM-dd} to {stay.Departure:yyyy-MM-dd} ({stay.Days} days)"));
        return McpResults.For(
            person,
            $"{payload.PlannedDays} planned days in {payload.Country}, peak {payload.PeakDays} against a limit of {payload.DayLimit}. {stays}.",
            payload);
    }

    private static MaxTripEndDateRequestDto StayRequest(
        AirportCatalog airports,
        string tripStart,
        int dayLimit,
        string? departureIataCode,
        string? departureCity,
        string? departureCountry,
        string? departureTimezone,
        string? arrivalIataCode,
        string? arrivalCity,
        string? arrivalCountry,
        string? arrivalTimezone)
    {
        var departure = TripLegResolver.ResolvePlace(departureIataCode, departureCity, departureCountry, departureTimezone, "Departure", airports);
        var arrival = TripLegResolver.ResolvePlace(arrivalIataCode, arrivalCity, arrivalCountry, arrivalTimezone, "Arrival", airports);
        return new MaxTripEndDateRequestDto
        {
            DepartureIataCode = departure.IataCode,
            DepartureCity = departure.City,
            DepartureCountry = departure.Country,
            DepartureTimezone = departure.Timezone,
            ArrivalIataCode = arrival.IataCode,
            ArrivalCity = arrival.City,
            ArrivalCountry = arrival.Country,
            ArrivalTimezone = arrival.Timezone,
            TripStart = TripLegResolver.ParseLocal(tripStart, "tripStart"),
            DayLimit = dayLimit <= 0 ? 183 : dayLimit
        };
    }

    private static List<Trip> ResolveLegs(List<McpTripLeg>? trips, string userId, AirportCatalog airports) =>
        (trips ?? []).Select(leg => TripLegResolver.Resolve(leg, userId, airports)).ToList();

    private static async Task<List<RollingCountPoint>> RollingAroundPeak(
        ResidencyQueryService query,
        string userId,
        List<Trip> legs,
        string? country,
        IReadOnlyList<McpForecastCountry> forecastRows,
        Traveller person)
    {
        if (string.IsNullOrWhiteSpace(country))
        {
            return [];
        }

        var match = forecastRows.FirstOrDefault(row => row.CountryName.Equals(country.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return [];
        }

        var from = match.PeakDate.AddDays(-7);
        var to = match.PeakDate.AddDays(7);
        var rows = await query.GetRollingCountAsync(userId, country.Trim(), from, to, legs, person);
        return rows.Select(RollingCountPoint.From).ToList();
    }

    private static List<McpForecastCountry> ForecastRows(
        ForecastBreakdown forecast,
        ResidencyCalculationService rules,
        string? homeCountry)
    {
        var names = new HashSet<string>(forecast.Forecast.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var name in forecast.Peaks.Keys)
        {
            names.Add(name);
        }

        var rows = new List<McpForecastCountry>();
        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var endDays = Lookup(forecast.Forecast, name);
            var rule = rules.GetCountryRule(name);
            var peakDays = endDays;
            var peakDate = CountingWindow.For(rule, forecast.WindowEnd).End;
            if (rule.WindowType == CountingWindowType.Rolling365
                && forecast.Peaks.TryGetValue(name, out var peak)
                && peak.Days >= endDays)
            {
                peakDays = peak.Days;
                peakDate = peak.Date;
            }

            if (endDays == 0 && peakDays == 0)
            {
                continue;
            }

            var status = ToStatus(name, peakDays, rules, homeCountry, forecast.WindowEnd);
            rows.Add(McpForecastCountry.Create(status, endDays, peakDays, peakDate));
        }

        return rows
            .OrderByDescending(row => row.TotalDays)
            .ThenBy(row => row.CountryName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int Lookup(Dictionary<string, int> counts, string name)
    {
        if (counts.TryGetValue(name, out var days))
        {
            return days;
        }

        foreach (var pair in counts)
        {
            if (pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return 0;
    }

    private static ResidencyCountryStatus ToStatus(
        string name,
        int days,
        ResidencyCalculationService rules,
        string? homeCountry,
        DateOnly referenceEnd)
    {
        var rule = rules.GetCountryRule(name);
        var period = CountingWindow.For(rule, referenceEnd);
        var threshold = rule.ResidencyThresholdDays ?? 183;
        var until = threshold - days;
        return ResidencyCountryStatus.From(new ResidencySummaryDto
        {
            CountryName = name,
            TotalDays = days,
            RuleType = rule.RuleType == ResidencyRuleType.MidnightRule ? "Midnight" : "PartialDay",
            ThresholdDays = threshold,
            IsApproachingThreshold = until <= 30 && until > 0,
            DaysUntilThreshold = Math.Max(0, until),
            WindowType = CountingWindow.Name(rule.WindowType),
            WindowStart = period.Start,
            WindowEnd = period.End
        }, homeCountry);
    }

    private static int DaysAway(IEnumerable<ResidencyCountryStatus> rows, string homeCountry) =>
        rows.Where(row => !row.CountryName.Equals(homeCountry.Trim(), StringComparison.OrdinalIgnoreCase)).Sum(row => row.TotalDays);

    private static string DescribeYear(HomeCalendarYear year)
    {
        var text = year.MinimumStatus is null
            ? $"{year.Country} {year.Days} days in {year.Year}."
            : $"{year.Country} {year.Days} of {year.MinimumDays} in {year.Year}, {year.MinimumStatus}, {year.DaysShortOfMinimum} short.";
        if (year.ProjectedFrom is DateOnly from)
        {
            text += $" Assumes you remain in {year.ProjectedCountry} from {from:yyyy-MM-dd} through {year.WindowEnd:yyyy-MM-dd}.";
        }

        return text;
    }

    private static string SummarizeCurrent(IReadOnlyList<ResidencyCountryStatus> rows) =>
        rows.Count == 0
            ? "Current: no days."
            : "Current: " + string.Join(", ", rows.Select(row => $"{row.CountryName} {row.TotalDays} ({row.RuleType}, {row.ThresholdStatus})"));

    private static string SummarizeForecast(IReadOnlyList<McpForecastCountry> rows) =>
        rows.Count == 0
            ? "Forecast: no days."
            : "Forecast: " + string.Join(", ", rows.Select(row =>
                $"{row.CountryName} {row.TotalDays} at window end, peak {row.PeakDays} on {row.PeakDate:yyyy-MM-dd} ({row.RuleType}, {row.ThresholdStatus})"));
}

public sealed class ForecastToolResult
{
    public McpTravellerRef Traveller { get; set; } = new();

    [Description("First day of the rolling forecast window, yyyy-MM-dd.")]
    public DateOnly WindowStart { get; set; }

    [Description("Last day of the rolling forecast window. totalDays is the count on this date.")]
    public DateOnly WindowEnd { get; set; }

    public List<ResidencyCountryStatus> Current { get; set; } = [];
    public List<McpForecastCountry> Forecast { get; set; } = [];
    public string? HomeCountry { get; set; }
    public int? DaysAwayCurrent { get; set; }
    public int? DaysAwayForecast { get; set; }

    [Description("Rolling counts from seven days before the chosen country's peak through seven days after. Empty unless country is set.")]
    public List<RollingCountPoint> RollingAroundPeak { get; set; } = [];

    [Description("Home country's day count in each calendar year the hypothetical legs touch, including saved trips. A year that runs past the last leg continues the last arrival country through 31 December. Empty when no home country is saved.")]
    public List<HomeCalendarYear> HomeCalendarYears { get; set; } = [];
}

public sealed class HomeCalendarYear
{
    public string Country { get; set; } = string.Empty;
    public int Year { get; set; }

    [Description("Days counted in this country during the calendar year, after the hypothetical legs are included.")]
    public int Days { get; set; }

    public DateOnly WindowStart { get; set; }
    public DateOnly WindowEnd { get; set; }

    [Description("Days required in this calendar year, such as 183 for Nova Scotia MSI. Null when the home country has no calendar-year minimum.")]
    public int? MinimumDays { get; set; }

    [Description("Days still needed to meet the minimum. Zero once the minimum is met. Null when there is no minimum.")]
    public int? DaysShortOfMinimum { get; set; }

    [Description("under, approaching, or reached. approaching means 30 or fewer days short. Null when there is no minimum. Uses the assumed stay when projectedFrom is set.")]
    public string? MinimumStatus { get; set; }

    [Description("First day the count assumes you remain in projectedCountry, through 31 December. Null when the saved and hypothetical legs already cover the year.")]
    public DateOnly? ProjectedFrom { get; set; }

    [Description("Country assumed from projectedFrom through 31 December, the last arrival country. Null when the year is fully covered.")]
    public string? ProjectedCountry { get; set; }

    public static HomeCalendarYear From(CalendarYearPresence year) => new()
    {
        Country = year.Country,
        Year = year.Year,
        Days = year.Days,
        WindowStart = year.WindowStart,
        WindowEnd = year.WindowEnd,
        MinimumDays = year.MinimumDays,
        DaysShortOfMinimum = year.DaysShort,
        MinimumStatus = year.Status,
        ProjectedFrom = year.ProjectedFrom,
        ProjectedCountry = year.ProjectedCountry
    };
}

public sealed class McpEarliestArrival
{
    public McpTravellerRef Traveller { get; set; } = new();

    [Description("Earliest day you can arrive and still be at or under the limit when you leave on the fixed date, yyyy-MM-dd.")]
    public DateOnly EarliestArrival { get; set; }

    public DateOnly WindowStart { get; set; }
    public DateOnly WindowEnd { get; set; }
    public string RuleType { get; set; } = string.Empty;
    public string WindowType { get; set; } = string.Empty;
    public int DaysAtLimit { get; set; }
}

public sealed class DateSpan
{
    [Description("First blackout date, yyyy-MM-dd.")]
    public string From { get; set; } = string.Empty;

    [Description("Last blackout date, yyyy-MM-dd.")]
    public string To { get; set; } = string.Empty;
}

public sealed class PresencePlanResult
{
    public McpTravellerRef Traveller { get; set; } = new();

    public string Country { get; set; } = string.Empty;
    public int DayLimit { get; set; }
    public int PlannedDays { get; set; }
    public int PeakDays { get; set; }
    public List<PlannedStayResult> Stays { get; set; } = [];
}

public sealed class PlannedStayResult
{
    [Description("First midnight in the country, yyyy-MM-dd.")]
    public DateOnly Arrival { get; set; }

    [Description("Last midnight in the country, yyyy-MM-dd.")]
    public DateOnly Departure { get; set; }

    public int Days { get; set; }
}

public sealed class AirportMatch
{
    public string IataCode { get; set; } = string.Empty;
    public string AirportName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string IanaTimezone { get; set; } = string.Empty;
}

[McpServerToolType]
public sealed class ItineraryTools
{
    [McpServerTool(Name = "parse_itinerary", Title = "Parse an itinerary", ReadOnly = true, OpenWorld = true, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(ItineraryParsingResponseDto))]
    [Description("Turns pasted booking text into draft flight legs. Saves nothing. Listed only when itinerary parsing is configured.")]
    public static async Task<CallToolResult> ParseItinerary(
        ItineraryParsingService parser,
        ItineraryParseLimiter limiter,
        ClaimsPrincipal user,
        IProgress<ProgressNotificationValue> progress,
        [Description("Raw itinerary or booking text.")] string text,
        CancellationToken cancellationToken)
    {
        McpScopes.Require(user, McpScopes.Read);
        if (!parser.IsConfigured())
        {
            throw new McpException("Itinerary parsing is not configured.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new McpException("text is required.");
        }

        await limiter.EnsureAllowedAsync(McpScopes.UserId(user), cancellationToken);
        progress.Report(new ProgressNotificationValue { Progress = 0, Total = 1, Message = "Reading itinerary" });
        var parsed = await parser.ParseItineraryAsync(text);
        progress.Report(new ProgressNotificationValue { Progress = 1, Total = 1, Message = "Parsed itinerary" });
        var summary = string.IsNullOrWhiteSpace(parsed.Error)
            ? $"Draft only, nothing saved. {parsed.Legs.Count} leg{(parsed.Legs.Count == 1 ? "" : "s")}."
            : $"Draft only, nothing saved. {parsed.Error}";
        return McpResults.Ok(summary, parsed);
    }
}
