using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using ResidencyRoll.Api;
using Xunit;

namespace ResidencyRoll.Tests;

public class LocalApiAuthenticationTests : IClassFixture<LocalApiFactory>
{
    private readonly LocalApiFactory _factory;

    public LocalApiAuthenticationTests(LocalApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateTrip_WithoutAToken_ShouldStoreTheTripForTheLocalUser()
    {
        var client = _factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/v1/trips", new
        {
            departureCountry = "Canada",
            departureCity = "Halifax",
            departureDateTime = "2026-11-09T12:00:00",
            departureTimezone = "America/Halifax",
            departureIataCode = "YHZ",
            arrivalCountry = "Canada",
            arrivalCity = "Montreal",
            arrivalDateTime = "2026-11-09T14:00:00",
            arrivalTimezone = "America/Toronto",
            arrivalIataCode = "YUL",
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var list = await client.GetAsync("/api/v1/trips");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var document = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var trip = document.RootElement.EnumerateArray().Single();
        Assert.Equal("YHZ", trip.GetProperty("departureIataCode").GetString());
        Assert.Equal("Halifax", trip.GetProperty("departureCity").GetString());
    }
}

public class LocalApiFactory : WebApplicationFactory<ApiEntryPoint>
{
    private readonly string _databasePath = Path.Join(Path.GetTempPath(), $"residencyroll-local-api-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Authority"] = "",
                ["ConnectionStrings:Default"] = $"Data Source={_databasePath}",
                ["DatabaseBackup:Enabled"] = "false",
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
