using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Json.Schema;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ResidencyRoll.Api;
using Xunit;

namespace ResidencyRoll.Tests;

public class McpServerTests : IClassFixture<McpApiFactory>
{
    private readonly McpApiFactory _factory;

    public McpServerTests(McpApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Initialize_ShouldListReadTools_AndAnswerLast365Days()
    {
        var http = _factory.CreateClient();
        await using var mcp = await McpClientConnector.ConnectAsync(http);

        Assert.Equal("ResidencyRoll", mcp.ServerInfo!.Name);
        var tools = await mcp.ListToolsAsync();
        Assert.Contains(tools, tool => tool.Name == "list_trips");
        Assert.Contains(tools, tool => tool.Name == "get_days_per_country");
        Assert.Contains(tools, tool => tool.Name == "get_residency_summary");
        Assert.Contains(tools, tool => tool.Name == "forecast_trips");
        Assert.DoesNotContain(tools, tool => tool.Name == "parse_itinerary");

        var daysTool = Assert.Single(tools, tool => tool.Name == "get_days_per_country");
        Assert.NotNull(daysTool.ReturnJsonSchema);
        Assert.Equal(true, daysTool.ProtocolTool.Annotations?.ReadOnlyHint);

        var result = await mcp.CallToolAsync("get_days_per_country", new Dictionary<string, object?> { ["window"] = "last365" });
        Assert.NotEqual(true, result.IsError);
        var structured = result.StructuredContent!.Value;

        var rest = await http.GetAsync("/api/v1/trips/days-per-country/last365");
        Assert.Equal(HttpStatusCode.OK, rest.StatusCode);
        using var restDocument = JsonDocument.Parse(await rest.Content.ReadAsStringAsync());
        var expected = restDocument.RootElement.EnumerateArray()
            .ToDictionary(row => row.GetProperty("countryName").GetString()!, row => row.GetProperty("days").GetInt32());
        var actual = structured.EnumerateArray()
            .ToDictionary(row => row.GetProperty("countryName").GetString()!, row => row.GetProperty("days").GetInt32());
        Assert.Equal(expected, actual);
        Assert.True(actual["United Kingdom"] > 0);
        var unitedKingdom = structured.EnumerateArray().Single(row => row.GetProperty("countryName").GetString() == "United Kingdom");
        Assert.Equal("Midnight", unitedKingdom.GetProperty("ruleType").GetString());
        Assert.Equal(183, unitedKingdom.GetProperty("thresholdDays").GetInt32());
        var canada = structured.EnumerateArray().Single(row => row.GetProperty("countryName").GetString() == "Canada");
        Assert.Equal("PartialDay", canada.GetProperty("ruleType").GetString());
        Assert.Equal(183, canada.GetProperty("thresholdDays").GetInt32());
    }

    [Fact]
    public async Task ListTrips_ShouldMatchTheRestResponse()
    {
        var http = _factory.CreateClient();
        await using var mcp = await McpClientConnector.ConnectAsync(http);
        var result = await mcp.CallToolAsync("list_trips", new Dictionary<string, object?>());
        Assert.NotEqual(true, result.IsError);

        var rest = await http.GetStringAsync("/api/v1/trips");
        using var restDocument = JsonDocument.Parse(rest);
        var restIds = restDocument.RootElement.EnumerateArray().Select(trip => trip.GetProperty("id").GetInt32()).Order().ToArray();
        var toolIds = result.StructuredContent!.Value.EnumerateArray().Select(trip => trip.GetProperty("id").GetInt32()).Order().ToArray();
        Assert.Equal(restIds, toolIds);
    }

    [Fact]
    public async Task Forecast_DateLineCrossing_ShouldSucceed()
    {
        var http = _factory.CreateClient();
        await using var mcp = await McpClientConnector.ConnectAsync(http);
        var result = await mcp.CallToolAsync("forecast_trips", new Dictionary<string, object?>
        {
            ["trips"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["departureIataCode"] = "AKL",
                    ["departureDateTime"] = "2026-12-02T10:00",
                    ["arrivalIataCode"] = "HNL",
                    ["arrivalDateTime"] = "2026-12-01T12:00"
                }
            }
        });

        Assert.NotEqual(true, result.IsError);
        var countries = result.StructuredContent!.Value.GetProperty("forecast").EnumerateArray()
            .Select(row => row.GetProperty("countryName").GetString())
            .ToArray();
        Assert.Contains(countries, country => country is "New Zealand" or "United States");
        var windowStart = DateOnly.Parse(result.StructuredContent.Value.GetProperty("windowStart").GetString()!);
        var windowEnd = DateOnly.Parse(result.StructuredContent.Value.GetProperty("windowEnd").GetString()!);
        Assert.True(windowEnd > windowStart);
        var totals = result.StructuredContent.Value.GetProperty("forecast").EnumerateArray().Select(row => row.GetProperty("totalDays").GetInt32()).ToArray();
        Assert.Equal(totals.OrderByDescending(days => days), totals);
        foreach (var row in result.StructuredContent.Value.GetProperty("forecast").EnumerateArray())
        {
            Assert.True(row.GetProperty("peakDays").GetInt32() >= row.GetProperty("totalDays").GetInt32());
            Assert.False(string.IsNullOrEmpty(row.GetProperty("thresholdStatus").GetString()));
            Assert.False(string.IsNullOrEmpty(row.GetProperty("ruleType").GetString()));
        }
    }

    [Fact]
    public async Task LookupAirport_ShouldResolvePearson()
    {
        var http = _factory.CreateClient();
        await using var mcp = await McpClientConnector.ConnectAsync(http);
        var result = await mcp.CallToolAsync("lookup_airport", new Dictionary<string, object?> { ["query"] = "pearson" });
        Assert.NotEqual(true, result.IsError);
        Assert.Equal("YYZ", result.StructuredContent!.Value[0].GetProperty("iataCode").GetString());
    }

    [Fact]
    public async Task CreateTrips_InvalidLeg_ShouldSaveNothing()
    {
        var http = _factory.CreateClient();
        var before = await TripCount(http);
        await using var mcp = await McpClientConnector.ConnectAsync(http);
        var result = await mcp.CallToolAsync("create_trips", new Dictionary<string, object?>
        {
            ["legs"] = new object[]
            {
                Leg("YHZ", "2026-04-01T08:00", "LHR", "2026-04-01T18:00"),
                new Dictionary<string, object?>
                {
                    ["departureIataCode"] = "LHR",
                    ["departureDateTime"] = "2026-04-10T10:00",
                    ["arrivalIataCode"] = "ZZZ",
                    ["arrivalDateTime"] = "2026-04-10T12:00",
                    ["arrivalCity"] = "Nowhere",
                    ["arrivalCountry"] = "Nowhere",
                    ["arrivalTimezone"] = "Not/AZone"
                }
            }
        });

        Assert.Equal(true, result.IsError);
        Assert.Contains("timezone", McpClientConnector.Text(result), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await TripCount(http));
    }

    [Fact]
    public async Task CreateUpdateDelete_ShouldRoundTrip()
    {
        var http = _factory.CreateClient();
        await using var mcp = await McpClientConnector.ConnectAsync(http);
        var created = await mcp.CallToolAsync("create_trip", new Dictionary<string, object?>
        {
            ["leg"] = Leg("YUL", "2026-05-01T09:00", "CDG", "2026-05-01T21:00")
        });
        Assert.NotEqual(true, created.IsError);
        var id = created.StructuredContent!.Value.GetProperty("id").GetInt32();

        var updated = await mcp.CallToolAsync("update_trip", new Dictionary<string, object?>
        {
            ["id"] = id,
            ["changes"] = Leg("YUL", "2026-05-01T09:00", "LHR", "2026-05-01T20:00")
        });
        Assert.NotEqual(true, updated.IsError);
        Assert.Equal("LHR", updated.StructuredContent!.Value.GetProperty("arrivalIataCode").GetString());

        var deleted = await mcp.CallToolAsync("delete_trip", new Dictionary<string, object?> { ["id"] = id });
        Assert.NotEqual(true, deleted.IsError);
        var missing = await mcp.CallToolAsync("get_trip", new Dictionary<string, object?> { ["id"] = id });
        Assert.Equal(true, missing.IsError);
        Assert.Contains("not found", McpClientConnector.Text(missing), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownIataWithoutPlace_ShouldFail()
    {
        var http = _factory.CreateClient();
        await using var mcp = await McpClientConnector.ConnectAsync(http);
        var result = await mcp.CallToolAsync("create_trip", new Dictionary<string, object?>
        {
            ["leg"] = new Dictionary<string, object?>
            {
                ["departureIataCode"] = "ZZZ",
                ["departureDateTime"] = "2026-05-02T09:00",
                ["arrivalIataCode"] = "LHR",
                ["arrivalDateTime"] = "2026-05-02T18:00"
            }
        });

        Assert.Equal(true, result.IsError);
        Assert.Contains("ZZZ", McpClientConnector.Text(result));
    }

    [Fact]
    public async Task BrowserOrigin_ThatIsNotAllowed_ShouldBeRejected()
    {
        var http = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("Origin", "https://evil.example");

        var response = await http.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DisabledEndpoint_ShouldNotBeMapped()
    {
        await using var factory = new McpApiFactory { McpEnabled = false };
        var response = await factory.CreateClient().GetAsync("/mcp");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void DateTimeFormat_ShouldRejectALocalTimeWithoutAnOffset()
    {
        var schema = JsonSchema.FromText("""{"type":"string","format":"date-time"}""");
        var options = new EvaluationOptions { RequireFormatValidation = true };
        var local = schema.Evaluate(JsonDocument.Parse("\"2026-12-05T21:00:00\"").RootElement, options);
        var offset = schema.Evaluate(JsonDocument.Parse("\"2026-12-05T21:00:00Z\"").RootElement, options);

        Assert.False(local.IsValid);
        Assert.True(offset.IsValid);
    }

    [Fact]
    public async Task ToolResults_ShouldMatchTheirOutputSchemas()
    {
        var http = _factory.CreateClient();
        await using var mcp = await McpClientConnector.ConnectAsync(http);
        var tools = (await mcp.ListToolsAsync()).ToList();
        foreach (var tool in tools)
        {
            Assert.True(tool.ProtocolTool.OutputSchema.HasValue, tool.Name);
            Assert.DoesNotContain("date-time", tool.ProtocolTool.OutputSchema.Value.GetRawText());
        }

        var listed = await mcp.CallToolAsync("list_trips", new Dictionary<string, object?>());
        AssertSchema(tools, "list_trips", listed);
        var trip = listed.StructuredContent!.Value.EnumerateArray().First();
        Assert.Matches(LocalTimePattern(), trip.GetProperty("departureDateTime").GetString());
        Assert.Matches(LocalTimePattern(), trip.GetProperty("arrivalDateTime").GetString());
        Assert.Contains("yyyy-MM-ddTHH:mm", tools.Single(tool => tool.Name == "list_trips").ProtocolTool.OutputSchema!.Value.GetRawText());

        await AssertCall(mcp, tools, "get_trip", new Dictionary<string, object?> { ["id"] = trip.GetProperty("id").GetInt32() });
        await AssertCall(mcp, tools, "get_days_per_country", new Dictionary<string, object?> { ["window"] = "last365" });
        await AssertCall(mcp, tools, "get_days_per_country", new Dictionary<string, object?> { ["window"] = "all" });
        await AssertCall(mcp, tools, "get_residency_summary", new Dictionary<string, object?> { ["homeCountry"] = "Canada" });
        var today = DateOnly.FromDateTime(DateTime.Today);
        await AssertCall(mcp, tools, "get_daily_presence", new Dictionary<string, object?>
        {
            ["startDate"] = today.AddDays(-20).ToString("yyyy-MM-dd"),
            ["endDate"] = today.AddDays(-10).ToString("yyyy-MM-dd")
        });
        var future = await AssertCall(mcp, tools, "get_location_on", new Dictionary<string, object?>
        {
            ["date"] = today.AddDays(30).ToString("yyyy-MM-dd")
        });
        Assert.Equal("projected", future.StructuredContent!.Value.GetProperty("status").GetString());
        Assert.False(string.IsNullOrEmpty(future.StructuredContent.Value.GetProperty("locationAtMidnight").GetString()));
        await AssertCall(mcp, tools, "get_location_on", new Dictionary<string, object?>
        {
            ["date"] = today.AddDays(-20).ToString("yyyy-MM-dd")
        });
        var current = await AssertCall(mcp, tools, "get_current_location", new Dictionary<string, object?>());
        Assert.Equal("YHZ", current.StructuredContent!.Value.GetProperty("lastArrivalIataCode").GetString());
        Assert.Equal("Canada", current.StructuredContent.Value.GetProperty("country").GetString());
        await AssertCall(mcp, tools, "get_country_rules", new Dictionary<string, object?>());
        var forecast = await AssertCall(mcp, tools, "forecast_trips", new Dictionary<string, object?>
        {
            ["homeCountry"] = "Canada",
            ["trips"] = new object[] { Leg("YHZ", today.AddDays(20).ToString("yyyy-MM-dd") + "T09:00", "LHR", today.AddDays(20).ToString("yyyy-MM-dd") + "T18:00") }
        });
        var forecastDays = forecast.StructuredContent!.Value.GetProperty("forecast").EnumerateArray().Select(row => row.GetProperty("totalDays").GetInt32()).ToArray();
        var currentDays = forecast.StructuredContent.Value.GetProperty("current").EnumerateArray().Select(row => row.GetProperty("totalDays").GetInt32()).ToArray();
        Assert.Equal(forecastDays.OrderByDescending(days => days), forecastDays);
        Assert.Equal(currentDays.OrderByDescending(days => days), currentDays);
        var maxEnd = await AssertCall(mcp, tools, "max_trip_end_date", new Dictionary<string, object?>
        {
            ["tripStart"] = today.AddDays(40).ToString("yyyy-MM-dd") + "T12:00",
            ["dayLimit"] = 30,
            ["departureIataCode"] = "YHZ",
            ["arrivalIataCode"] = "LHR"
        });
        var maxEndDate = maxEnd.StructuredContent!.Value.GetProperty("maxEndDate").GetString();
        var summary = McpClientConnector.Text(maxEnd);
        Assert.Matches(DatePattern(), maxEndDate);
        Assert.Contains($"is {maxEndDate}", summary);
        Assert.False(trip.TryGetProperty("startDate", out _));
        Assert.False(trip.TryGetProperty("endDate", out _));
        var daysAtLimit = maxEnd.StructuredContent.Value.GetProperty("daysAtLimit").GetInt32();
        Assert.Contains($"with a peak of {daysAtLimit} days", summary);
        Assert.InRange(daysAtLimit, 0, 30);
        await AssertCall(mcp, tools, "standard_duration_forecasts", new Dictionary<string, object?>
        {
            ["tripStart"] = today.AddDays(40).ToString("yyyy-MM-dd") + "T12:00",
            ["dayLimit"] = 183,
            ["durations"] = new[] { 7, 14 },
            ["departureIataCode"] = "YHZ",
            ["arrivalIataCode"] = "LHR"
        });
        await AssertCall(mcp, tools, "lookup_airport", new Dictionary<string, object?> { ["query"] = "pearson" });
        await AssertCall(mcp, tools, "get_home_country", new Dictionary<string, object?>());
        await AssertCall(mcp, tools, "set_home_country", new Dictionary<string, object?> { ["homeCountry"] = "Canada" });
        await AssertCall(mcp, tools, "set_home_country", new Dictionary<string, object?> { ["homeCountry"] = "" });

        var created = await AssertCall(mcp, tools, "create_trip", new Dictionary<string, object?>
        {
            ["leg"] = Leg("YUL", "2026-08-01T09:00", "CDG", "2026-08-01T21:00")
        });
        var createdId = created.StructuredContent!.Value.GetProperty("id").GetInt32();
        Assert.Equal("2026-08-01T09:00", created.StructuredContent.Value.GetProperty("departureDateTime").GetString());
        var createdMany = await AssertCall(mcp, tools, "create_trips", new Dictionary<string, object?>
        {
            ["legs"] = new object[] { Leg("YHZ", "2026-08-10T08:00", "LHR", "2026-08-10T18:00") }
        });
        var createdManyId = createdMany.StructuredContent!.Value.EnumerateArray().First().GetProperty("id").GetInt32();
        await AssertCall(mcp, tools, "update_trip", new Dictionary<string, object?>
        {
            ["id"] = createdId,
            ["changes"] = Leg("YUL", "2026-08-01T09:00", "LHR", "2026-08-01T20:00")
        });
        await AssertCall(mcp, tools, "delete_trip", new Dictionary<string, object?> { ["id"] = createdId });
        await AssertCall(mcp, tools, "delete_trip", new Dictionary<string, object?> { ["id"] = createdManyId });

        var called = new[]
        {
            "list_trips", "get_trip", "get_days_per_country", "get_residency_summary", "get_daily_presence",
            "get_location_on", "get_current_location", "get_country_rules", "forecast_trips", "max_trip_end_date",
            "standard_duration_forecasts", "lookup_airport", "get_home_country", "set_home_country",
            "create_trip", "create_trips", "update_trip", "delete_trip"
        };
        Assert.Equal(tools.Select(tool => tool.Name).OrderBy(name => name), called.OrderBy(name => name));
    }

    [Fact]
    public async Task SavedHomeCountry_ShouldApplyWhenTheArgumentIsOmitted()
    {
        var http = _factory.CreateClient();
        var saved = await http.PutAsJsonAsync("/api/v1/profile/home-country", new { homeCountry = "Canada" });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        try
        {
            await using var mcp = await McpClientConnector.ConnectAsync(http);
            var home = await mcp.CallToolAsync("get_home_country", new Dictionary<string, object?>());
            Assert.NotEqual(true, home.IsError);
            Assert.Equal("Canada", home.StructuredContent!.Value.GetProperty("homeCountry").GetString());

            var summary = await mcp.CallToolAsync("get_residency_summary", new Dictionary<string, object?>());
            Assert.NotEqual(true, summary.IsError);
            var canada = summary.StructuredContent!.Value.EnumerateArray().Single(row => row.GetProperty("countryName").GetString() == "Canada");
            Assert.True(canada.GetProperty("isHomeCountry").GetBoolean());
            Assert.Equal("home", canada.GetProperty("thresholdStatus").GetString());
            Assert.DoesNotContain("No home country is saved", McpClientConnector.Text(summary));
        }
        finally
        {
            var cleared = await http.PutAsJsonAsync("/api/v1/profile/home-country", new { homeCountry = "" });
            Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        }
    }

    [Fact]
    public async Task HomeCountryOverTheThreshold_ShouldHideDaysLeftAndDaysOver()
    {
        var http = _factory.CreateClient();
        await using var mcp = await McpClientConnector.ConnectAsync(http);
        var tools = (await mcp.ListToolsAsync()).ToList();
        var arrive = DateOnly.FromDateTime(DateTime.Today).AddDays(30);
        var leave = arrive.AddDays(200);
        var forecast = await AssertCall(mcp, tools, "forecast_trips", new Dictionary<string, object?>
        {
            ["homeCountry"] = "Canada",
            ["trips"] = new object[]
            {
                Leg("LHR", arrive.ToString("yyyy-MM-dd") + "T09:00", "YHZ", arrive.ToString("yyyy-MM-dd") + "T16:00"),
                Leg("YHZ", leave.ToString("yyyy-MM-dd") + "T08:00", "LHR", leave.ToString("yyyy-MM-dd") + "T18:00")
            }
        });

        var canada = forecast.StructuredContent!.Value.GetProperty("forecast").EnumerateArray()
            .Single(row => row.GetProperty("countryName").GetString() == "Canada");
        Assert.True(canada.GetProperty("peakDays").GetInt32() > 183);
        Assert.True(canada.GetProperty("isHomeCountry").GetBoolean());
        Assert.Equal("home", canada.GetProperty("thresholdStatus").GetString());
        Assert.False(canada.GetProperty("isApproachingThreshold").GetBoolean());
        Assert.Equal(JsonValueKind.Null, canada.GetProperty("daysUntilThreshold").ValueKind);
        Assert.Equal(JsonValueKind.Null, canada.GetProperty("daysOverThreshold").ValueKind);

        var summary = await AssertCall(mcp, tools, "get_residency_summary", new Dictionary<string, object?>
        {
            ["homeCountry"] = "Canada"
        });
        var current = summary.StructuredContent!.Value.EnumerateArray()
            .Single(row => row.GetProperty("countryName").GetString() == "Canada");
        Assert.Equal("home", current.GetProperty("thresholdStatus").GetString());
        Assert.False(current.GetProperty("isApproachingThreshold").GetBoolean());
        Assert.Equal(JsonValueKind.Null, current.GetProperty("daysUntilThreshold").ValueKind);
        Assert.Equal(JsonValueKind.Null, current.GetProperty("daysOverThreshold").ValueKind);
    }

    [Fact]
    public async Task RollingWindowBoundary_ShouldMatchBetweenRestAndMcp()
    {
        await using var factory = new McpApiFactory { SeedExampleTrips = false };
        var http = factory.CreateClient();
        var end = DateOnly.FromDateTime(DateTime.Today);
        var start = end.AddDays(-(365 - 1));
        var outside = start.AddDays(-1);
        var arrival = outside.AddDays(-1).ToString("yyyy-MM-dd");
        var boundary = start.ToString("yyyy-MM-dd");
        PostBoundaryTrip(http, "Canada", "America/Halifax", arrival + "T08:00:00", "United Kingdom", "Europe/London", arrival + "T18:00:00");
        PostBoundaryTrip(http, "United Kingdom", "Europe/London", boundary + "T10:00:00", "Canada", "America/Halifax", boundary + "T16:00:00");

        using var restDocument = JsonDocument.Parse(await http.GetStringAsync("/api/v1/trips/days-per-country/last365"));
        var restDays = restDocument.RootElement.EnumerateArray()
            .Single(row => row.GetProperty("countryName").GetString() == "United Kingdom")
            .GetProperty("days").GetInt32();

        await using var mcp = await McpClientConnector.ConnectAsync(http);
        var days = await mcp.CallToolAsync("get_days_per_country", new Dictionary<string, object?> { ["window"] = "last365" });
        var summary = await mcp.CallToolAsync("get_residency_summary", new Dictionary<string, object?>());
        Assert.NotEqual(true, days.IsError);
        Assert.NotEqual(true, summary.IsError);
        var toolDays = days.StructuredContent!.Value.EnumerateArray()
            .Single(row => row.GetProperty("countryName").GetString() == "United Kingdom")
            .GetProperty("days").GetInt32();
        var summaryDays = summary.StructuredContent!.Value.EnumerateArray()
            .Single(row => row.GetProperty("countryName").GetString() == "United Kingdom")
            .GetProperty("totalDays").GetInt32();

        Assert.Equal(1, restDays);
        Assert.Equal(restDays, toolDays);
        Assert.Equal(restDays, summaryDays);
    }

    [Fact]
    public async Task TripDuration_ShouldCountMinutesAcrossTimezones()
    {
        var http = _factory.CreateClient();
        await using var mcp = await McpClientConnector.ConnectAsync(http);
        var tools = (await mcp.ListToolsAsync()).ToList();
        var created = await AssertCall(mcp, tools, "create_trip", new Dictionary<string, object?>
        {
            ["leg"] = Leg("YHZ", "2026-09-04T11:05", "LHR", "2026-09-04T21:00")
        });
        try
        {
            Assert.Equal(355, created.StructuredContent!.Value.GetProperty("durationMinutes").GetInt32());
            Assert.False(created.StructuredContent.Value.TryGetProperty("startDate", out _));
            Assert.False(created.StructuredContent.Value.TryGetProperty("endDate", out _));
        }
        finally
        {
            await mcp.CallToolAsync("delete_trip", new Dictionary<string, object?>
            {
                ["id"] = created.StructuredContent!.Value.GetProperty("id").GetInt32()
            });
        }
    }

    [Fact]
    public async Task MaxTripEndDate_ShouldMatchTheForecastPeakForThatStay()
    {
        await using var factory = new McpApiFactory { SeedExampleTrips = false };
        var http = factory.CreateClient();
        await using var mcp = await McpClientConnector.ConnectAsync(http);
        var tools = (await mcp.ListToolsAsync()).ToList();
        var start = DateOnly.FromDateTime(DateTime.Today).AddDays(30);
        var tripStart = start.ToString("yyyy-MM-dd") + "T12:00";
        var maxEnd = await AssertCall(mcp, tools, "max_trip_end_date", new Dictionary<string, object?>
        {
            ["tripStart"] = tripStart,
            ["dayLimit"] = 10,
            ["departureIataCode"] = "YHZ",
            ["arrivalIataCode"] = "LHR"
        });
        var payload = maxEnd.StructuredContent!.Value;
        var maxEndDate = payload.GetProperty("maxEndDate").GetString()!;
        Assert.Matches(DatePattern(), maxEndDate);
        Assert.Equal("Midnight", payload.GetProperty("ruleType").GetString());
        Assert.Equal(10, payload.GetProperty("daysAtLimit").GetInt32());
        var windowStart = DateOnly.Parse(payload.GetProperty("windowStart").GetString()!);
        var windowEnd = DateOnly.Parse(payload.GetProperty("windowEnd").GetString()!);
        Assert.Equal(365, windowEnd.DayNumber - windowStart.DayNumber + 1);

        var outbound = DateTime.Parse(tripStart).AddHours(-6).ToString("yyyy-MM-ddTHH:mm");
        var homeAgain = DateTime.Parse(maxEndDate + "T12:00").AddHours(6).ToString("yyyy-MM-ddTHH:mm");
        var forecast = await AssertCall(mcp, tools, "forecast_trips", new Dictionary<string, object?>
        {
            ["trips"] = new object[]
            {
                Leg("YHZ", outbound, "LHR", tripStart),
                Leg("LHR", maxEndDate + "T12:00", "YHZ", homeAgain)
            }
        });
        var unitedKingdom = forecast.StructuredContent!.Value.GetProperty("forecast").EnumerateArray()
            .Single(row => row.GetProperty("countryName").GetString() == "United Kingdom");
        Assert.Equal(10, unitedKingdom.GetProperty("peakDays").GetInt32());
        Assert.Equal(windowStart, DateOnly.Parse(forecast.StructuredContent.Value.GetProperty("windowStart").GetString()!));
        Assert.Equal(windowEnd, DateOnly.Parse(forecast.StructuredContent.Value.GetProperty("windowEnd").GetString()!));
    }

    [Fact]
    public async Task ParseItineraryOutputSchema_ShouldNotUseDateTimeFormat()
    {
        await using var factory = new McpApiFactory { ParserConfigured = true };
        var http = factory.CreateClient();
        await using var mcp = await McpClientConnector.ConnectAsync(http);
        var parser = (await mcp.ListToolsAsync()).Single(tool => tool.Name == "parse_itinerary");
        var schema = parser.ProtocolTool.OutputSchema!.Value.GetRawText();
        Assert.DoesNotContain("date-time", schema);
        Assert.Contains("departure_airport", schema);
    }

    [Fact]
    public async Task ParseItinerary_ShouldBeListedOnlyWhenConfigured()
    {
        await using var factory = new McpApiFactory { ParserConfigured = true };
        var http = factory.CreateClient();
        await using var mcp = await McpClientConnector.ConnectAsync(http);
        var tools = await mcp.ListToolsAsync();
        Assert.Contains(tools, tool => tool.Name == "parse_itinerary");
        var parser = Assert.Single(tools, tool => tool.Name == "parse_itinerary");
        Assert.Equal(true, parser.ProtocolTool.Annotations?.OpenWorldHint);
    }

    private static async Task<CallToolResult> AssertCall(
        McpClient mcp,
        IReadOnlyList<McpClientTool> tools,
        string name,
        Dictionary<string, object?> args)
    {
        var result = await mcp.CallToolAsync(name, args);
        AssertSchema(tools, name, result);
        return result;
    }

    private static void AssertSchema(IReadOnlyList<McpClientTool> tools, string name, CallToolResult result)
    {
        Assert.NotEqual(true, result.IsError);
        var schemaElement = tools.Single(tool => tool.Name == name).ProtocolTool.OutputSchema!.Value;
        var schema = JsonSchema.FromText(schemaElement.GetRawText());
        var evaluation = schema.Evaluate(result.StructuredContent!.Value, new EvaluationOptions
        {
            RequireFormatValidation = true,
            OutputFormat = OutputFormat.List
        });
        if (evaluation.IsValid)
        {
            return;
        }

        var errors = string.Join("; ", (evaluation.Details ?? [])
            .Where(detail => detail.Errors is { Count: > 0 })
            .SelectMany(detail => detail.Errors!.Select(error => $"{detail.InstanceLocation} {error.Key}: {error.Value}")));
        Assert.Fail($"{name} structured content does not match its output schema. {errors}");
    }

    private static Regex LocalTimePattern() => new(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$");

    private static Regex DatePattern() => new(@"^\d{4}-\d{2}-\d{2}$");

    private static async Task<int> TripCount(HttpClient http)
    {
        using var document = JsonDocument.Parse(await http.GetStringAsync("/api/v1/trips"));
        return document.RootElement.GetArrayLength();
    }

    private static void PostBoundaryTrip(
        HttpClient http,
        string fromCountry,
        string fromZone,
        string departure,
        string toCountry,
        string toZone,
        string arrival)
    {
        var response = http.PostAsJsonAsync("/api/v1/trips", new
        {
            departureCountry = fromCountry,
            departureCity = fromCountry,
            departureDateTime = departure,
            departureTimezone = fromZone,
            arrivalCountry = toCountry,
            arrivalCity = toCountry,
            arrivalDateTime = arrival,
            arrivalTimezone = toZone
        }).GetAwaiter().GetResult();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static Dictionary<string, object?> Leg(string from, string departure, string to, string arrival) => new()
    {
        ["departureIataCode"] = from,
        ["departureDateTime"] = departure,
        ["arrivalIataCode"] = to,
        ["arrivalDateTime"] = arrival
    };
}

public class McpApiFactory : WebApplicationFactory<ApiEntryPoint>
{
    private readonly string _databasePath = Path.Join(Path.GetTempPath(), $"residencyroll-mcp-{Guid.NewGuid():N}.db");

    public bool McpEnabled { get; set; } = true;
    public bool ParserConfigured { get; set; }
    public bool SeedExampleTrips { get; set; } = true;

    private int _seeded;

    public new HttpClient CreateClient()
    {
        var client = base.CreateClient();
        if (Interlocked.Exchange(ref _seeded, 1) == 0 && McpEnabled && SeedExampleTrips)
        {
            Seed(client);
        }

        return client;
    }

    private static void Seed(HttpClient client)
    {
        var outbound = DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd");
        var inbound = DateTime.Today.AddDays(-10).ToString("yyyy-MM-dd");
        PostTrip(client, new
        {
            departureCountry = "Canada",
            departureCity = "Halifax",
            departureDateTime = outbound + "T08:00:00",
            departureTimezone = "America/Halifax",
            departureIataCode = "YHZ",
            arrivalCountry = "United Kingdom",
            arrivalCity = "London",
            arrivalDateTime = outbound + "T18:00:00",
            arrivalTimezone = "Europe/London",
            arrivalIataCode = "LHR"
        });
        PostTrip(client, new
        {
            departureCountry = "United Kingdom",
            departureCity = "London",
            departureDateTime = inbound + "T10:00:00",
            departureTimezone = "Europe/London",
            departureIataCode = "LHR",
            arrivalCountry = "Canada",
            arrivalCity = "Halifax",
            arrivalDateTime = inbound + "T16:00:00",
            arrivalTimezone = "America/Halifax",
            arrivalIataCode = "YHZ"
        });
    }

    private static void PostTrip(HttpClient client, object trip)
    {
        var created = client.PostAsJsonAsync("/api/v1/trips", trip).GetAwaiter().GetResult();
        if (created.StatusCode != HttpStatusCode.Created)
        {
            throw new InvalidOperationException(created.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Host configuration becomes the entry-point args and overrides appsettings at startup.
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["environment"] = "Production",
                ["Jwt:Authority"] = "",
                ["ConnectionStrings:Default"] = $"Data Source={_databasePath}",
                ["DatabaseBackup:Enabled"] = "false",
                ["Mcp:Enabled"] = McpEnabled ? "true" : "false",
                ["OpenAI:OpenAIApiKey"] = ParserConfigured ? "test-key" : "",
                ["OpenAI:OpenAIEndpoint"] = ParserConfigured ? "https://example.invalid/v1" : "",
                ["OpenAI:OpenAIModel"] = ParserConfigured ? "gpt-test" : ""
            });
        });
        return base.CreateHost(builder);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        foreach (var path in new[] { _databasePath, _databasePath + "-wal", _databasePath + "-shm" }.Where(File.Exists))
        {
            File.Delete(path);
        }
    }
}

public static class McpClientConnector
{
    public static async Task<McpClient> ConnectAsync(HttpClient http, string? bearer = null)
    {
        var headers = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(bearer))
        {
            headers["Authorization"] = bearer.StartsWith("Bearer ", StringComparison.Ordinal) ? bearer : $"Bearer {bearer}";
        }

        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(http.BaseAddress!, "mcp"),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = headers
        }, http, ownsHttpClient: false);
        return await McpClient.CreateAsync(transport);
    }

    public static string Text(CallToolResult result) =>
        string.Join(" ", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
}
