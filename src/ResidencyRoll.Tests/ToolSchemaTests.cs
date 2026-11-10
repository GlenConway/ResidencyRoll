using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using ResidencyRoll.Api.Mcp;
using ResidencyRoll.Api.Services;
using Xunit;

namespace ResidencyRoll.Tests;

public class ToolSchemaTests
{
    [Theory]
    [InlineData(typeof(ResidencyTools), nameof(ResidencyTools.GetResidencySummary))]
    [InlineData(typeof(ForecastTools), nameof(ForecastTools.ForecastTrips))]
    [InlineData(typeof(ResidencyTools), nameof(ResidencyTools.GetLocationOn))]
    [InlineData(typeof(ForecastTools), nameof(ForecastTools.MaxTripEndDate))]
    public void AdvertisedSchema_ShouldNameTraveller_AndSaveTheHomeCountryOnTheTraveller(Type type, string method)
    {
        var info = type.GetMethod(method, BindingFlags.Public | BindingFlags.Static)!;
        var services = new ServiceCollection();
        services.AddSingleton<ResidencyCalculationService>();
        services.AddSingleton<AirportCatalog>();
        services.AddSingleton<ResidencyQueryService>();
        services.AddSingleton<TravellerService>();
        services.AddSingleton<ClaimsPrincipal>();
        var tool = McpServerTool.Create(info, target: null, new McpServerToolCreateOptions
        {
            Services = services.BuildServiceProvider()
        });

        Assert.Contains("\"traveller\"", tool.ProtocolTool.InputSchema.GetRawText());
        Assert.Contains("saved on", tool.ProtocolTool.Description);
        Assert.DoesNotContain("browser", tool.ProtocolTool.Description, StringComparison.OrdinalIgnoreCase);
    }
}
