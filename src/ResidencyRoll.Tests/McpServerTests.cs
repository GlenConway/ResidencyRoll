using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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

    private static async Task<int> TripCount(HttpClient http)
    {
        using var document = JsonDocument.Parse(await http.GetStringAsync("/api/v1/trips"));
        return document.RootElement.GetArrayLength();
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

    private int _seeded;

    public new HttpClient CreateClient()
    {
        var client = base.CreateClient();
        if (Interlocked.Exchange(ref _seeded, 1) == 0 && McpEnabled)
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
