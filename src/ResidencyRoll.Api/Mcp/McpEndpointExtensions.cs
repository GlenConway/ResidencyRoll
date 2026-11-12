using System.Reflection;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ResidencyRoll.Api.Services;
using ResidencyRoll.Shared.Mcp;
using Serilog;

namespace ResidencyRoll.Api.Mcp;

public static class McpEndpointExtensions
{
    public const int MaxBodyBytes = 1024 * 1024;

    public static WebApplicationBuilder AddResidencyMcp(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<AirportCatalog>();
        builder.Services.AddScoped<ResidencyQueryService>();
        builder.Services.AddSingleton<ItineraryParseLimiter>();
        builder.Services.AddHttpContextAccessor();

        if (!McpConfiguration.IsEnabled(builder.Configuration))
        {
            Log.Information("MCP endpoint is disabled.");
            return builder;
        }

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("mcp", httpContext =>
            {
                var key = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? httpContext.User.FindFirstValue("sub")
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous";
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 120,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
            });
        });

        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        var mcp = builder.Services.AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = "ResidencyRoll",
                    Title = "ResidencyRoll",
                    Version = version
                };
                options.ServerInstructions =
                    "Answer residency questions from the signed-in user's trips. Trips belong to travellers on the signed-in account. Omit traveller for the signed-in person. When the user names someone, or says \"we\" or \"my wife\", call list_travellers and pass the match. Confirm the traveller by name before any write. locationAtMidnight for a date is the country at 00:00 at the start of that date, and a later flight the same day does not change it. Each country uses either the midnight rule or the partial-day rule; tool results include ruleType. Each country is counted over its own windowType (rolling365, taxYear, or calendarYear). The home country is saved on the traveller. Call get_home_country, and set_home_country when it is missing, before answering days left, days over, or days away. Use get_current_location for the current country and last arrival airport. forecast_trips reports each country's window and a peak that can exceed the count at the end of a rolling window. Pass excludeTripIds when a plan replaces saved legs: those legs stay saved and are left out of the forecast. Reaching the threshold counts as residency (thresholdStatus reached). Saved legs that look wrong are returned as warnings and by find_suspect_trips; a summary window past the first or last saved date is incomplete. unaccountedDays on a summary row is the number of days in that window that no country counts, including a country gap. When Canada is home, Nova Scotia MSI is a minimum of 183 days in Canada in the calendar year. forecast_trips reports that count in homeCalendarYears for each calendar year the plan touches. Leg times are local wall-clock times with no offset. Write tools change saved trips, so confirm with the user first. create_trips, replace_trips, and import_trips_csv check every leg before saving and return every error together. Their default result is ids, a short summary, and warnings. Call get_server_info to see the commit and version of this build. parse_itinerary returns a draft and saves nothing.";
            })
            .WithHttpTransport(options => options.Stateless = true)
            .WithTools<TripReadTools>()
            .WithTools<ResidencyTools>()
            .WithTools<ForecastTools>()
            .WithTools<TripWriteTools>()
            .WithTools<TripBatchTools>()
            .WithTools<ServerTools>()
            .WithTools<TravellerTools>()
            .WithTools<ProfileTools>()
            .WithPrompts<ResidencyPrompts>()
            .WithResources<CountryRulesResource>();

        if (ParserConfigured(builder.Configuration))
        {
            mcp.WithTools<ItineraryTools>();
        }

        return builder;
    }

    public static WebApplication UseResidencyMcpGuards(this WebApplication app)
    {
        if (!McpConfiguration.IsEnabled(app.Configuration))
        {
            return app;
        }

        var allowed = new HashSet<string>(McpUrls.AllowedOrigins(app.Configuration), StringComparer.OrdinalIgnoreCase);
        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/mcp"))
            {
                await next();
                return;
            }

            var origin = context.Request.Headers.Origin.ToString();
            if (!string.IsNullOrEmpty(origin) && !allowed.Contains(origin))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Origin is not allowed.");
                return;
            }

            if (context.Request.ContentLength > MaxBodyBytes)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                return;
            }

            var bodyLimit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (bodyLimit is { IsReadOnly: false })
            {
                bodyLimit.MaxRequestBodySize = MaxBodyBytes;
            }

            await next();
        });

        app.UseRateLimiter();
        return app;
    }

    public static WebApplication MapResidencyMcp(this WebApplication app)
    {
        if (!McpConfiguration.IsEnabled(app.Configuration))
        {
            return app;
        }

        app.MapMcp("/mcp").RequireAuthorization().RequireRateLimiting("mcp");

        if (!IsJwtEnabled(app.Configuration))
        {
            Log.Warning("/mcp is open without authentication and runs as the local user. Use this mode on trusted networks only.");
            return app;
        }

        app.MapGet("/.well-known/oauth-protected-resource", Metadata).AllowAnonymous();
        app.MapGet("/.well-known/oauth-protected-resource/{**rest}", Metadata).AllowAnonymous();
        return app;
    }

    public static bool IsJwtEnabled(IConfiguration configuration)
    {
        var authority = configuration["Jwt:Authority"];
        return !string.IsNullOrWhiteSpace(authority)
            && !string.Equals(authority.Trim(), "https://your-identity-provider.com", StringComparison.OrdinalIgnoreCase);
    }

    public static void ApplyAudiences(IConfiguration configuration, Microsoft.IdentityModel.Tokens.TokenValidationParameters parameters)
    {
        var audiences = new List<string>();
        var apiAudience = McpConfiguration.EmptyToNull(configuration["Jwt:Audience"]);
        if (apiAudience is not null)
        {
            audiences.Add(apiAudience);
        }

        var mcpAudience = McpUrls.Audience(configuration);
        if (mcpAudience is not null && !audiences.Contains(mcpAudience, StringComparer.Ordinal))
        {
            audiences.Add(mcpAudience);
        }

        if (audiences.Count > 0)
        {
            parameters.ValidAudiences = audiences;
        }
    }

    private static IResult Metadata(HttpContext context)
    {
        var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
        var authority = configuration["Jwt:Authority"]?.Trim();
        var document = new ProtectedResourceMetadataDocument
        {
            Resource = McpUrls.Resource(context.Request, configuration),
            AuthorizationServers = string.IsNullOrWhiteSpace(authority) ? [] : [authority],
            ScopesSupported = [McpScopes.Read, McpScopes.Write],
            BearerMethodsSupported = ["header"],
            ResourceName = "ResidencyRoll"
        };
        return Results.Json(document);
    }

    private static bool ParserConfigured(IConfiguration configuration)
    {
        var section = configuration.GetSection("OpenAI");
        return !string.IsNullOrWhiteSpace(section["OpenAIModel"])
            && !string.IsNullOrWhiteSpace(section["OpenAIEndpoint"])
            && !string.IsNullOrWhiteSpace(section["OpenAIApiKey"]);
    }
}

public sealed class ProtectedResourceMetadataDocument
{
    [JsonPropertyName("resource")]
    public string Resource { get; set; } = string.Empty;

    [JsonPropertyName("authorization_servers")]
    public string[] AuthorizationServers { get; set; } = [];

    [JsonPropertyName("scopes_supported")]
    public string[] ScopesSupported { get; set; } = [];

    [JsonPropertyName("bearer_methods_supported")]
    public string[] BearerMethodsSupported { get; set; } = [];

    [JsonPropertyName("resource_name")]
    public string ResourceName { get; set; } = "ResidencyRoll";
}
