using System.ComponentModel;
using System.Security.Claims;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ResidencyRoll.Api.Services;
using ResidencyRoll.Shared.Trips;

namespace ResidencyRoll.Api.Mcp;

[McpServerToolType]
public sealed class ForecastTools
{
    private const string Semantics = "The forecast uses the same location-at-midnight rules as saved trips, including partial-day countries and International Date Line crossings. The home country is stored in the user's browser, so ask for it when you need days away from home.";

    [McpServerTool(Name = "forecast_trips", Title = "Forecast trips", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(ForecastToolResult))]
    [Description("Projects days per country if the hypothetical legs were added. Nothing is saved. Pass IATA codes and local date-times (yyyy-MM-ddTHH:mm). City, country, and IANA timezone override the airport table and are required for unknown airports. " + Semantics)]
    public static async Task<CallToolResult> ForecastTrips(
        ResidencyQueryService query,
        AirportCatalog airports,
        ClaimsPrincipal user,
        [Description("Hypothetical flight legs, in order.")] List<McpTripLeg> trips,
        [Description("Home country name. Ask the user when it is missing.")] string? homeCountry = null)
    {
        McpScopes.Require(user, McpScopes.Read);
        var userId = McpScopes.UserId(user);
        var legs = (trips ?? []).Select(leg => TripLegResolver.Resolve(leg, userId, airports)).ToList();
        var forecast = await query.ForecastAsync(userId, legs);
        int? awayCurrent = null;
        int? awayForecast = null;
        if (!string.IsNullOrWhiteSpace(homeCountry))
        {
            awayCurrent = DaysAway(forecast.Current, homeCountry);
            awayForecast = DaysAway(forecast.Forecast, homeCountry);
        }

        var result = new ForecastToolResult
        {
            Current = forecast.Current,
            Forecast = forecast.Forecast,
            HomeCountry = string.IsNullOrWhiteSpace(homeCountry) ? null : homeCountry.Trim(),
            DaysAwayCurrent = awayCurrent,
            DaysAwayForecast = awayForecast
        };
        var text = Summarize("Current", forecast.Current) + " " + Summarize("Forecast", forecast.Forecast);
        text += string.IsNullOrWhiteSpace(homeCountry)
            ? " Home country was not provided, so days away from home are not calculated."
            : $" Days away from {homeCountry.Trim()}: current {awayCurrent}, forecast {awayForecast}.";
        return McpResults.Ok(text, result);
    }

    [McpServerTool(Name = "max_trip_end_date", Title = "Latest trip end under the day limit", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(MaxTripEndDateResponseDto))]
    [Description("Latest departure from the arrival country that keeps that country at or under dayLimit (default 183) in the forecast window. " + Semantics)]
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
        return McpResults.Ok(
            $"Latest end in {request.ArrivalCountry} is {result.MaxEndDate:yyyy-MM-ddTHH:mm}, with {result.DaysAtLimit} days against a limit of {request.DayLimit}.",
            result);
    }

    [McpServerTool(Name = "standard_duration_forecasts", Title = "Standard duration forecasts", ReadOnly = true, OpenWorld = false, Destructive = false, UseStructuredContent = true, OutputSchemaType = typeof(StandardDurationForecastItemDto[]))]
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
        var rows = await query.GetStandardDurationForecastsAsync(McpScopes.UserId(user), request);
        var text = string.Join(" ", rows.Select(row => $"{row.DurationDays} days ends {row.EndDate:yyyy-MM-dd} with {row.TotalDaysInCountry} in {stay.ArrivalCountry}{(row.ExceedsLimit ? " (over the limit)" : "")}."));
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

    private static int DaysAway(IEnumerable<CountryDaysDto> rows, string homeCountry) =>
        rows.Where(row => !row.CountryName.Equals(homeCountry.Trim(), StringComparison.OrdinalIgnoreCase)).Sum(row => row.Days);

    private static string Summarize(string label, IReadOnlyList<CountryDaysDto> rows) =>
        rows.Count == 0
            ? $"{label}: no days."
            : $"{label}: " + string.Join(", ", rows.Select(row => $"{row.CountryName} {row.Days}"));
}

public sealed class ForecastToolResult
{
    public List<CountryDaysDto> Current { get; set; } = [];
    public List<CountryDaysDto> Forecast { get; set; } = [];
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
