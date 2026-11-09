using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Radzen;
using System.Globalization;
using ResidencyRoll.Web.Bff;
using ResidencyRoll.Web.Components;
using ResidencyRoll.Web.Services;
using ResidencyRoll.Shared.Trips;
using ResidencyRoll.Shared.Extensions;
using Serilog;
using System.Reflection;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .Build())
    .CreateLogger();

var hostAborted = false;
try
{
    var version = System.Reflection.Assembly.GetExecutingAssembly()
        .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.
        InformationalVersion ?? "unknown";
    
    Log.Information("Starting ResidencyRoll Web - Version: {Version}", version);

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (string.IsNullOrWhiteSpace(keysPath))
{
    var inContainer = string.Equals(
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
        "true",
        StringComparison.OrdinalIgnoreCase);
    if (inContainer)
    {
        keysPath = "/app/data/keys";
    }
    else if (File.Exists(Path.Combine(builder.Environment.ContentRootPath, "Data", "AirportData.cs")))
    {
        // Content root is the project folder. "data" and "Data" are the same directory
        // on a case-insensitive volume, so keep the key ring in the repo data folder.
        keysPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "data", "keys"));
    }
    else
    {
        keysPath = Path.Combine(builder.Environment.ContentRootPath, "data", "keys");
    }
}

Directory.CreateDirectory(keysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("ResidencyRoll.Web");

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add HTTP context accessor for authentication handler
builder.Services.AddHttpContextAccessor();

// Configure authentication
var oidcEnabled = builder.Configuration.GetValue<bool>("Authentication:OpenIdConnect:Enabled");
if (oidcEnabled)
{
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "ResidencyRoll.Auth";
        options.Cookie.SameSite = SameSiteMode.Lax;
        // Only require secure cookies in production
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() 
            ? CookieSecurePolicy.SameAsRequest 
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.SlidingExpiration = true;
        options.Events = new CookieAuthenticationEvents();
        BffEndpointExtensions.ReturnUnauthorizedForApiPaths(options.Events);
    })
    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        options.Authority = builder.Configuration["Authentication:OpenIdConnect:Authority"];
        options.ClientId = builder.Configuration["Authentication:OpenIdConnect:ClientId"];
        options.ClientSecret = builder.Configuration["Authentication:OpenIdConnect:ClientSecret"];
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.RequireHttpsMetadata = builder.Configuration.GetValue<bool?>("Authentication:OpenIdConnect:RequireHttpsMetadata") ?? true;
        
        // Request additional scopes for API access
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        
        // No custom API scopes needed - Auth0 will issue access token based on audience alone
        // If you need custom scopes in the future, add them here
        
        // Request an access token for the API by setting the Auth0 audience
        var apiAudience = builder.Configuration["Authentication:OpenIdConnect:ApiAudience"];
        options.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = context =>
            {
                if (BffEndpointExtensions.IsApiOrBff(context.Request.Path))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.HandleResponse();
                    return Task.CompletedTask;
                }

                Log.Information("[OIDC] Redirecting to identity provider. Audience: {Audience}", apiAudience);
                if (!string.IsNullOrEmpty(apiAudience))
                {
                    context.ProtocolMessage.SetParameter("audience", apiAudience);
                    Log.Information("[OIDC] Added audience parameter: {Audience}", apiAudience);
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Log.Information("[OIDC] Token validated for user: {User}", context.Principal?.Identity?.Name);
                
                // Store the access token as a claim so it's available in the authentication ticket
                if (context.Properties?.Items != null && context.TokenEndpointResponse != null)
                {
                    var accessToken = context.TokenEndpointResponse.AccessToken;
                    if (!string.IsNullOrEmpty(accessToken) && context.Principal != null)
                    {
                        var identity = context.Principal.Identity as System.Security.Claims.ClaimsIdentity;
                        identity?.AddClaim(new System.Security.Claims.Claim("access_token", accessToken));
                        Log.Information("[OIDC] Stored access token as claim");
                    }
                }
                
                return Task.CompletedTask;
            },
            OnTokenResponseReceived = context =>
            {
                Log.Information("[OIDC] Token response received.");
                Log.Information("[OIDC] - access_token present: {HasAccessToken}", !string.IsNullOrEmpty(context.TokenEndpointResponse.AccessToken));
                Log.Information("[OIDC] - id_token present: {HasIdToken}", !string.IsNullOrEmpty(context.TokenEndpointResponse.IdToken));
                Log.Information("[OIDC] - refresh_token present: {HasRefreshToken}", !string.IsNullOrEmpty(context.TokenEndpointResponse.RefreshToken));
                if (!string.IsNullOrEmpty(context.TokenEndpointResponse.AccessToken))
                {
                    var tokenPrefix = context.TokenEndpointResponse.AccessToken.Substring(0, Math.Min(30, context.TokenEndpointResponse.AccessToken.Length));
                    Log.Information("[OIDC] - access_token (first 30 chars): {TokenPrefix}...", tokenPrefix);
                }
                return Task.CompletedTask;
            }
        };
        
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = "role";
    });
    
    builder.Services.AddAuthorization();
}
else
{
    // For development without OIDC, use minimal cookie-based auth
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.Cookie.Name = "ResidencyRoll.Auth";
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.LoginPath = "/login";
            options.Events = new CookieAuthenticationEvents();
            BffEndpointExtensions.ReturnUnauthorizedForApiPaths(options.Events);
        });
    
    builder.Services.AddAuthorization();
}

// Add Radzen services
builder.Services.AddRadzenComponents();

// Add authentication state provider for Blazor components (uses server-side auth from middleware)
// This enables @attribute [Authorize] and <AuthorizeView> in components
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddServerSideBlazor();

// Provide HttpClient with the app base address so relative API calls (e.g., import) work.
// This HttpClient is used by Blazor components to call local proxy endpoints
builder.Services.AddHttpClient("LocalProxy", client =>
{
    // Don't resolve NavigationManager here - it's scoped
    // The base address will be set at runtime when the client is used
});

// Register a scoped HttpClient for components that uses the LocalProxy configuration
builder.Services.AddScoped<HttpClient>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var client = factory.CreateClient("LocalProxy");
    
    // Set base address using NavigationManager (now in scoped context)
    var navigation = sp.GetRequiredService<NavigationManager>();
    client.BaseAddress = new Uri(navigation.BaseUri);
    
    return client;
});

// Add typed HTTP client for API with authentication
var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "https://localhost:5003";
builder.Services.AddReverseProxy()
    .AddTransforms(transformBuilder =>
    {
        transformBuilder.AddRequestTransform(BffProxy.AttachAccessToken);
    });

foreach (var descriptor in builder.Services.Where(d => d.ServiceType == typeof(IProxyConfigProvider)).ToList())
{
    builder.Services.Remove(descriptor);
}

// Resolve the destination when the proxy starts so later configuration (tests, env vars) wins.
builder.Services.AddSingleton<IProxyConfigProvider>(sp =>
{
    var configuredBaseUrl = sp.GetRequiredService<IConfiguration>()["Api:BaseUrl"] ?? apiBaseUrl;
    var apiProxyAddress = configuredBaseUrl.EndsWith('/') ? configuredBaseUrl : configuredBaseUrl + "/";
    return new InMemoryConfigProvider(
    [
        new RouteConfig
        {
            RouteId = "api",
            ClusterId = "api",
            Match = new RouteMatch { Path = "/api/{**catch-all}" }
        }
    ],
    [
        new ClusterConfig
        {
            ClusterId = "api",
            HttpRequest = new ForwarderRequestConfig
            {
                Version = new Version(1, 1),
                VersionPolicy = HttpVersionPolicy.RequestVersionExact
            },
            Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
            {
                ["api"] = new() { Address = apiProxyAddress }
            }
        }
    ]);
});

builder.Services.AddScoped<ApiAuthenticationHandler>();
builder.Services.AddHttpClient<TripsApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddHttpMessageHandler<ApiAuthenticationHandler>();

builder.Services.AddHttpClient<SystemApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddHttpMessageHandler<ApiAuthenticationHandler>();

// Register utility services
builder.Services.AddScoped<CountryColorService>();
builder.Services.AddScoped<LocalStorageService>();

// Register AccessTokenProvider for Blazor circuits
builder.Services.AddScoped<AccessTokenProvider>();

var app = builder.Build();

// Handle forwarded headers from reverse proxy
app.UseConfiguredForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseWhen(
    context => !BffEndpointExtensions.IsApiOrBff(context.Request.Path)
        && !context.Request.Path.StartsWithSegments("/auth"),
    branch => branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();

// IMPORTANT: API endpoints MUST be registered BEFORE MapRazorComponents
// because Razor component routing acts as a catch-all for remaining routes.

// BFF: auth endpoints, CSRF, and the /api reverse proxy. Blazor still serves the UI.
app.MapPost("/auth/login", async (HttpContext context) =>
{
    var oidcEnabled = context.RequestServices.GetRequiredService<IConfiguration>()
        .GetValue<bool>("Authentication:OpenIdConnect:Enabled");

    if (!oidcEnabled)
    {
        context.Response.Redirect("/");
        return;
    }

    await context.ChallengeAsync(
        OpenIdConnectDefaults.AuthenticationScheme,
        new AuthenticationProperties
        {
            RedirectUri = "/"
        });
});

app.MapBff();

// Streams a database backup from the API to the browser as a file download
app.MapGet("/app/system/backups/{fileName}", async (string fileName, HttpContext httpContext, SystemApiClient apiClient) =>
{
    var response = await apiClient.GetBackupAsync(fileName);
    if (response == null)
    {
        return Results.NotFound();
    }

    httpContext.Response.RegisterForDispose(response);
    var stream = await response.Content.ReadAsStreamAsync();
    return Results.File(stream, "application/vnd.sqlite3", fileName);
}).RequireAuthorization();

// NOW register Razor components
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Proxy endpoints to call the external API from within HTTP context (so tokens attach)
var tripsProxy = app.MapGroup("/app/trips").WithTags("Trips Proxy");

tripsProxy.MapGet("/", async (TripsApiClient apiClient) =>
{
    var trips = await apiClient.GetAllTripsAsync();
    return Results.Ok(trips);
}).DisableAntiforgery();

tripsProxy.MapGet("/timeline", async (HttpContext httpContext, TripsApiClient apiClient) =>
{
    Log.Information("[Proxy] /timeline called. User authenticated: {IsAuthenticated}, User: {User}",
        httpContext.User?.Identity?.IsAuthenticated,
        httpContext.User?.Identity?.Name);
    
    // Check for authentication cookie
    var hasCookie = httpContext.Request.Cookies.ContainsKey("ResidencyRoll.Auth");
    Log.Information("[Proxy] ResidencyRoll.Auth cookie present: {HasCookie}", hasCookie);
    
    if (hasCookie)
    {
        var cookieValue = httpContext.Request.Cookies["ResidencyRoll.Auth"];
        Log.Debug("[Proxy] Cookie value length: {Length}", cookieValue?.Length ?? 0);
    }
    
    var result = await apiClient.GetTimelineAsync();
    return Results.Ok(result);
}).DisableAntiforgery();

tripsProxy.MapGet("/days-per-country/last365", async (TripsApiClient apiClient) =>
{
    var result = await apiClient.GetDaysPerCountryInLast365DaysAsync();
    return Results.Ok(result);
}).DisableAntiforgery();

tripsProxy.MapGet("/total-days-away/last365", async (TripsApiClient apiClient) =>
{
    var result = await apiClient.GetTotalDaysAwayInLast365DaysAsync();
    return Results.Ok(result);
}).DisableAntiforgery();

tripsProxy.MapGet("/days-at-home/last365", async (TripsApiClient apiClient) =>
{
    var result = await apiClient.GetDaysAtHomeInLast365DaysAsync();
    return Results.Ok(result);
}).DisableAntiforgery();

tripsProxy.MapGet("/{id:int}", async (int id, TripsApiClient apiClient) =>
{
    var trip = await apiClient.GetTripByIdAsync(id);
    return trip is null ? Results.NotFound() : Results.Ok(trip);
}).DisableAntiforgery();

tripsProxy.MapPost("/", async (TripDto trip, TripsApiClient apiClient) =>
{
    var created = await apiClient.CreateTripAsync(trip);
    return Results.Ok(created);
}).DisableAntiforgery();

tripsProxy.MapPut("/{id:int}", async (int id, TripDto trip, TripsApiClient apiClient) =>
{
    await apiClient.UpdateTripAsync(id, trip);
    return Results.NoContent();
}).DisableAntiforgery();

tripsProxy.MapDelete("/{id:int}", async (int id, TripsApiClient apiClient) =>
{
    await apiClient.DeleteTripAsync(id);
    return Results.NoContent();
}).DisableAntiforgery();

tripsProxy.MapPost("/forecast", async (ForecastRequestDto request, TripsApiClient apiClient) =>
{
    var response = await apiClient.ForecastDaysWithTripsAsync(request.Legs);
    return Results.Ok(response);
}).DisableAntiforgery();

tripsProxy.MapPost("/forecast/max-end-date", async (MaxTripEndDateRequestDto request, TripsApiClient apiClient) =>
{
    var response = await apiClient.CalculateMaxTripEndDateAsync(
        request.DepartureCountry,
        request.DepartureCity,
        request.DepartureTimezone,
        request.DepartureIataCode,
        request.ArrivalCountry,
        request.ArrivalCity,
        request.TripStart,
        request.ArrivalTimezone,
        request.ArrivalIataCode,
        request.DayLimit);
    return Results.Ok(response);
}).DisableAntiforgery();

tripsProxy.MapPost("/forecast/standard-durations", async (StandardDurationForecastRequestDto request, TripsApiClient apiClient) =>
{
    var response = await apiClient.CalculateStandardDurationForecastsAsync(
        request.DepartureCountry,
        request.DepartureCity,
        request.DepartureTimezone,
        request.DepartureIataCode,
        request.ArrivalCountry,
        request.ArrivalCity,
        request.TripStart,
        request.ArrivalTimezone,
        request.ArrivalIataCode,
        request.DayLimit);
    return Results.Ok(response);
}).DisableAntiforgery();

app.Run();
}
catch (HostAbortedException)
{
    hostAborted = true;
    throw;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    if (!hostAborted)
    {
        Log.CloseAndFlush();
    }
}

public partial class Program { }
