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
    [Description("Projects days per country if the hypothetical legs were added. Nothing is saved. windowStart and windowEnd bound the forecast. Each country includes totalDays at windowEnd, peakDays and peakDate, and thresholdStatus (under, approaching, over, or home). Both lists are sorted by days, descending. Pass IATA codes and local date-times (yyyy-MM-ddTHH:mm). City, country, and IANA timezone override the airport table and are required for unknown airports. " + Semantics)]
    public static async Task<CallToolResult> ForecastTrips(
        ResidencyQueryService query,
        ResidencyCalculationService rules,
        UserProfileService profiles,
        AirportCatalog airports,
        ClaimsPrincipal user,
        [Description("Hypothetical flight legs, in order.")] List<McpTripLeg> trips,
        [Description("Home country name. Optional. Uses the saved home country when omitted.")] string? homeCountry = null)
    {
        McpScopes.Require(user, McpScopes.Read);
        var userId = McpScopes.UserId(user);
        var home = await SavedHomeCountry.Resolve(profiles, userId, homeCountry);
        var legs = (trips ?? []).Select(leg => TripLegResolver.Resolve(leg, userId, airports)).ToList();
        var forecast = await query.ForecastBreakdownAsync(userId, legs);
        var current = forecast.Current
            .Select(pair => ToStatus(pair.Key, pair.Value, rules, home))
            .OrderByDescending(row => row.TotalDays)
            .ThenBy(row => row.CountryName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var forecastRows = ForecastRows(forecast, rules, home);
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
            DaysAwayForecast = awayForecast
        };
        var text = $"Forecast window {forecast.WindowStart:yyyy-MM-dd} to {forecast.WindowEnd:yyyy-MM-dd}. "
            + SummarizeCurrent(current) + " " + SummarizeForecast(forecastRows);
        text += home is null
            ? " No home country is saved, so days away from home are not calculated. Ask the user, then call set_home_country."
            : $" Days away from {home} at the end of each window: current {awayCurrent}, forecast {awayForecast}.";
        return McpResults.Ok(text, result);
    }

    [McpServerTool(Name = "max_trip_end_date", Title = "Latest trip end under the day limit", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(McpMaxEndDate))]
    [Description("Latest departure from the arrival country that keeps that country's peak rolling count at or under dayLimit (default 183). DaysAtLimit is that peak, which can be higher than the count on the end date. MaxEndDate is a local time in the arrival airport's timezone. " + Semantics)]
    public static async Task<CallToolResult> MaxTripEndDate(
        ResidencyQueryService query,
        AirportCatalog airports,
        ClaimsPrincipal user,
        [Description("Local arrival date-time in the destination, yyyy-MM-ddTHH:mm.")] string tripStart,
        [Description("Day limit for the arrival country. Defaults to 183.")] int dayLimit = 183,
        string? departureIataCode = null,
        string? departureCity = null,
        string? departureCountry = null,
        string? departureTimezone = null,
        string? arrivalIataCode = null,
        string? arrivalCity = null,
        string? arrivalCountry = null,
        string? arrivalTimezone = null)
    {
        McpScopes.Require(user, McpScopes.Read);
        var request = StayRequest(airports, tripStart, dayLimit, departureIataCode, departureCity, departureCountry, departureTimezone, arrivalIataCode, arrivalCity, arrivalCountry, arrivalTimezone);
        var result = await query.CalculateMaxEndDateAsync(McpScopes.UserId(user), request);
        var payload = McpMaxEndDate.From(result);
        return McpResults.Ok(
            $"Latest end in {request.ArrivalCountry} is {payload.MaxEndDate}, with a peak of {payload.DaysAtLimit} days against a limit of {request.DayLimit}.",
            payload);
    }

    [McpServerTool(Name = "standard_duration_forecasts", Title = "Standard duration forecasts", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(McpDurationForecast[]))]
    [Description("Days in the arrival country for a set of stay lengths. Durations default to 7, 14, 30, 60, 90, and 180 days. " + Semantics)]
    public static async Task<CallToolResult> StandardDurationForecasts(
        ResidencyQueryService query,
        AirportCatalog airports,
        ClaimsPrincipal user,
        [Description("Local arrival date-time in the destination, yyyy-MM-ddTHH:mm.")] string tripStart,
        [Description("Day limit for the arrival country. Defaults to 183.")] int dayLimit = 183,
        [Description("Stay lengths in days. Optional.")] int[]? durations = null,
        string? departureIataCode = null,
        string? departureCity = null,
        string? departureCountry = null,
        string? departureTimezone = null,
        string? arrivalIataCode = null,
        string? arrivalCity = null,
        string? arrivalCountry = null,
        string? arrivalTimezone = null)
    {
        McpScopes.Require(user, McpScopes.Read);
        var stay = StayRequest(airports, tripStart, dayLimit, departureIataCode, departureCity, departureCountry, departureTimezone, arrivalIataCode, arrivalCity, arrivalCountry, arrivalTimezone);
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
        var rows = (await query.GetStandardDurationForecastsAsync(McpScopes.UserId(user), request))
            .Select(McpDurationForecast.From)
            .ToList();
        var text = string.Join(" ", rows.Select(row => $"{row.DurationDays} days ends {row.EndDate} with {row.TotalDaysInCountry} in {stay.ArrivalCountry}{(row.ExceedsLimit ? " (over the limit)" : "")}."));
        return McpResults.Ok(text, rows);
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
            var peakDays = endDays;
            var peakDate = forecast.WindowEnd;
            if (forecast.Peaks.TryGetValue(name, out var peak) && peak.Days >= endDays)
            {
                peakDays = peak.Days;
                peakDate = peak.Date;
            }

            if (endDays == 0 && peakDays == 0)
            {
                continue;
            }

            var status = ToStatus(name, peakDays, rules, homeCountry);
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

    private static ResidencyCountryStatus ToStatus(string name, int days, ResidencyCalculationService rules, string? homeCountry)
    {
        var rule = rules.GetCountryRule(name);
        var threshold = rule.ResidencyThresholdDays ?? 183;
        var until = threshold - days;
        return ResidencyCountryStatus.From(new ResidencySummaryDto
        {
            CountryName = name,
            TotalDays = days,
            RuleType = rule.RuleType == ResidencyRuleType.MidnightRule ? "Midnight" : "PartialDay",
            ThresholdDays = threshold,
            IsApproachingThreshold = until <= 30 && until > 0,
            DaysUntilThreshold = Math.Max(0, until)
        }, homeCountry);
    }

    private static int DaysAway(IEnumerable<ResidencyCountryStatus> rows, string homeCountry) =>
        rows.Where(row => !row.CountryName.Equals(homeCountry.Trim(), StringComparison.OrdinalIgnoreCase)).Sum(row => row.TotalDays);

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
    [Description("First day of the rolling forecast window, yyyy-MM-dd.")]
    public DateOnly WindowStart { get; set; }

    [Description("Last day of the rolling forecast window. totalDays is the count on this date.")]
    public DateOnly WindowEnd { get; set; }

    public List<ResidencyCountryStatus> Current { get; set; } = [];
    public List<McpForecastCountry> Forecast { get; set; } = [];
    public string? HomeCountry { get; set; }
    public int? DaysAwayCurrent { get; set; }
    public int? DaysAwayForecast { get; set; }
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
