using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using ResidencyRoll.Api;
using Xunit;

namespace ResidencyRoll.Tests;

public class McpOAuthTests
{
    private const string Issuer = "https://issuer.test/";
    private const string ApiAudience = "https://api.example/trips";
    private const string McpAudience = "https://mcp.example/mcp";
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef");

    [Fact]
    public async Task NoToken_ShouldChallengeWithResourceMetadata()
    {
        await using var factory = new JwtApiFactory();
        var response = await factory.CreateClient().PostAsync("/mcp", JsonRpc("initialize"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var header = response.Headers.WwwAuthenticate.ToString();
        Assert.Contains("resource_metadata=", header, StringComparison.Ordinal);
        Assert.Contains("/.well-known/oauth-protected-resource/mcp", header, StringComparison.Ordinal);
        Assert.Contains("scope=\"read:trips\"", header, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RestChallenge_ShouldKeepItsExistingShape()
    {
        await using var factory = new JwtApiFactory();
        var response = await factory.CreateClient().GetAsync("/api/v1/trips");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("resource_metadata", response.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WrongAudience_ShouldBeRejected()
    {
        await using var factory = new JwtApiFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token("user-a", "https://evil.example", "read:trips"));

        var response = await client.PostAsync("/mcp", JsonRpc("initialize"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task McpAudience_ShouldListTrips_AndApiAudience_ShouldCallRest()
    {
        await using var factory = new JwtApiFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token("user-a", ApiAudience, "read:trips write:trips"));
        var created = await client.PostAsJsonAsync("/api/v1/trips", SampleTrip());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var rest = await client.GetAsync("/api/v1/trips");
        Assert.Equal(HttpStatusCode.OK, rest.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token("user-a", McpAudience, "read:trips write:trips"));
        await using var mcp = await McpClientConnector.ConnectAsync(client, Token("user-a", McpAudience, "read:trips write:trips"));
        var listed = await mcp.CallToolAsync("list_trips", new Dictionary<string, object?>());
        Assert.NotEqual(true, listed.IsError);
        Assert.Equal("YHZ", listed.StructuredContent!.Value[0].GetProperty("departureIataCode").GetString());
    }

    [Fact]
    public async Task ReadScope_ShouldRejectWriteTools()
    {
        await using var factory = new JwtApiFactory();
        var client = factory.CreateClient();
        await using var mcp = await McpClientConnector.ConnectAsync(client, Token("user-a", McpAudience, "read:trips"));
        var result = await mcp.CallToolAsync("delete_trip", new Dictionary<string, object?> { ["id"] = 1 });

        Assert.Equal(true, result.IsError);
        Assert.Contains("write:trips", McpClientConnector.Text(result));
    }

    [Fact]
    public async Task OtherUser_ShouldNotUpdateOrDelete()
    {
        await using var factory = new JwtApiFactory();
        var owner = factory.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token("user-a", ApiAudience, "read:trips write:trips"));
        var created = await owner.PostAsJsonAsync("/api/v1/trips", SampleTrip());
        using var createdDocument = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = createdDocument.RootElement.GetProperty("id").GetInt32();

        var other = factory.CreateClient();
        await using var mcp = await McpClientConnector.ConnectAsync(other, Token("user-b", McpAudience, "read:trips write:trips"));
        var updated = await mcp.CallToolAsync("update_trip", new Dictionary<string, object?>
        {
            ["id"] = id,
            ["changes"] = new Dictionary<string, object?>
            {
                ["departureIataCode"] = "YUL",
                ["departureDateTime"] = "2026-04-01T08:00",
                ["arrivalIataCode"] = "LHR",
                ["arrivalDateTime"] = "2026-04-01T18:00"
            }
        });
        var deleted = await mcp.CallToolAsync("delete_trip", new Dictionary<string, object?> { ["id"] = id });

        Assert.Equal(true, updated.IsError);
        Assert.Contains("not found", McpClientConnector.Text(updated), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(true, deleted.IsError);
        Assert.Contains("not found", McpClientConnector.Text(deleted), StringComparison.OrdinalIgnoreCase);

        var stillThere = await owner.GetAsync($"/api/v1/trips/{id}");
        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
    }

    [Fact]
    public async Task MetadataResource_ShouldFollowResourceUrl()
    {
        await using var factory = new JwtApiFactory { ResourceUrl = "https://residencyroll.kinsac.com/mcp" };
        var response = await factory.CreateClient().GetAsync("/.well-known/oauth-protected-resource/mcp");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("https://residencyroll.kinsac.com/mcp", document.RootElement.GetProperty("resource").GetString());
        Assert.Equal("https://issuer.test/", document.RootElement.GetProperty("authorization_servers")[0].GetString());
        Assert.Equal("ResidencyRoll", document.RootElement.GetProperty("resource_name").GetString());
    }

    [Fact]
    public async Task MetadataResource_ShouldFollowForwardedHeaders()
    {
        await using var factory = new JwtApiFactory { TrustForwardedHeaders = true };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/oauth-protected-resource/mcp");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Host", "roll.example");

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("https://roll.example/mcp", document.RootElement.GetProperty("resource").GetString());
    }

    private static StringContent JsonRpc(string method) =>
        new(
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"" + method + "\",\"params\":{\"protocolVersion\":\"2025-11-25\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1\"}}}",
            Encoding.UTF8,
            "application/json");

    private static object SampleTrip() => new
    {
        departureCountry = "Canada",
        departureCity = "Halifax",
        departureDateTime = "2026-04-01T08:00:00",
        departureTimezone = "America/Halifax",
        departureIataCode = "YHZ",
        arrivalCountry = "United Kingdom",
        arrivalCity = "London",
        arrivalDateTime = "2026-04-01T18:00:00",
        arrivalTimezone = "Europe/London",
        arrivalIataCode = "LHR"
    };

    private static string Token(string subject, string audience, string scope)
    {
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: audience,
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, subject),
                new Claim("sub", subject),
                new Claim("scope", scope)
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-5),
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Key), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class JwtApiFactory : WebApplicationFactory<ApiEntryPoint>
    {
        private readonly string _databasePath = Path.Join(Path.GetTempPath(), $"residencyroll-jwt-{Guid.NewGuid():N}.db");

        public string? ResourceUrl { get; init; }
        public bool TrustForwardedHeaders { get; init; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, TestJwtPostConfigure>();
            });
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            // WebApplicationFactory turns host configuration into the entry-point args, which
            // override appsettings. App configuration added later is not visible while Program starts.
            builder.ConfigureHostConfiguration(config =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["environment"] = "Production",
                    ["Jwt:Authority"] = Issuer,
                    ["Jwt:Audience"] = ApiAudience,
                    ["Jwt:RequireHttpsMetadata"] = "false",
                    ["Mcp:Audience"] = McpAudience,
                    ["Mcp:ResourceUrl"] = ResourceUrl ?? "",
                    ["ConnectionStrings:Default"] = $"Data Source={_databasePath}",
                    ["DatabaseBackup:Enabled"] = "false"
                };
                if (TrustForwardedHeaders)
                {
                    settings["ForwardedHeaders:KnownNetworks:0"] = "0.0.0.0/0";
                    settings["ForwardedHeaders:KnownNetworks:1"] = "::/0";
                }

                config.AddInMemoryCollection(settings);
            });
            return base.CreateHost(builder);
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

    private sealed class TestJwtPostConfigure : IPostConfigureOptions<JwtBearerOptions>
    {
        public void PostConfigure(string? name, JwtBearerOptions options)
        {
            var config = new OpenIdConnectConfiguration { Issuer = Issuer };
            config.SigningKeys.Add(new SymmetricSecurityKey(Key));
            options.RequireHttpsMetadata = false;
            options.Configuration = config;
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(config);
            options.TokenValidationParameters.ValidateIssuer = true;
            options.TokenValidationParameters.ValidIssuer = Issuer;
            options.TokenValidationParameters.ValidateIssuerSigningKey = true;
            options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Key);
        }
    }
}
