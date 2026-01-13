using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Radzen;
using System.Globalization;
using ResidencyRoll.Web.Components;
using ResidencyRoll.Web.Data;
using ResidencyRoll.Web.Services;
using ResidencyRoll.Shared.Trips;
using Serilog;
using Microsoft.AspNetCore.HttpOverrides;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/web-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting ResidencyRoll Web");

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

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
        });
    
    builder.Services.AddAuthorization();
}

// Add Radzen services
builder.Services.AddRadzenComponents();

// Add authentication state provider for Blazor components (uses server-side auth from middleware)
// This enables @attribute [Authorize] and <AuthorizeView> in components
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddServerSideBlazor();

// Configure SQLite database
var dataDirectory = builder.Environment.IsDevelopment() 
    ? Path.Combine(builder.Environment.ContentRootPath, "data")
    : Path.Combine("/app", "data");
Directory.CreateDirectory(dataDirectory);
var connectionString = $"Data Source={Path.Combine(dataDirectory, "residencyroll.db")}";
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

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
var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "https://localhost:5000";
builder.Services.AddScoped<ApiAuthenticationHandler>();
builder.Services.AddHttpClient<TripsApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddHttpMessageHandler<ApiAuthenticationHandler>();

// Add application services (kept for now for import/export endpoints)
// TODO: Move import/export to API and remove TripService completely
builder.Services.AddScoped<TripService>();

// Register AccessTokenProvider for Blazor circuits
builder.Services.AddScoped<AccessTokenProvider>();

var app = builder.Build();

// Handle forwarded headers from reverse proxy
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};

// Clear security restrictions so it trusts headers from your NGINX proxy
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();

app.UseForwardedHeaders(forwardedHeadersOptions);

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Authentication endpoints
app.MapPost("/auth/login", async (HttpContext context) =>
{
    var oidcEnabled = context.RequestServices.GetRequiredService<IConfiguration>()
        .GetValue<bool>("Authentication:OpenIdConnect:Enabled");
    
    if (!oidcEnabled)
    {
        // For development without OIDC, redirect to home
        context.Response.Redirect("/");
        return;
    }
    
    await context.ChallengeAsync(
        Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectDefaults.AuthenticationScheme,
        new Microsoft.AspNetCore.Authentication.AuthenticationProperties
        {
            RedirectUri = "/"
        });
});

app.MapPost("/logout", async (HttpContext context) =>
{
    var oidcEnabled = context.RequestServices.GetRequiredService<IConfiguration>()
        .GetValue<bool>("Authentication:OpenIdConnect:Enabled");
    
    if (!oidcEnabled)
    {
        // For development without OIDC, redirect to home
        context.Response.Redirect("/");
        return;
    }
    
    await context.SignOutAsync(
        Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignOutAsync(
        Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectDefaults.AuthenticationScheme,
        new Microsoft.AspNetCore.Authentication.AuthenticationProperties
        {
            RedirectUri = "/"
        });
});

app.MapGet("/api/trips/export", async (TripsApiClient apiClient) =>
{
    var trips = await apiClient.GetAllTripsAsync();
    var csvLines = new List<string> { "CountryName,StartDate,EndDate" };

    foreach (var trip in trips.OrderBy(t => t.StartDate))
    {
        var line = string.Join(',',
            EscapeCsv(trip.CountryName ?? string.Empty),
            trip.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            trip.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        csvLines.Add(line);
    }

    var bytes = System.Text.Encoding.UTF8.GetBytes(string.Join('\n', csvLines));
    return Results.File(bytes, "text/csv", "trips.csv");
}).DisableAntiforgery();

app.MapPost("/api/trips/import", async (HttpRequest request, TripsApiClient apiClient) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest("Form file is required.");
    }

    var form = await request.ReadFormAsync();
    var file = form.Files.FirstOrDefault();

    if (file == null || file.Length == 0)
    {
        return Results.BadRequest("File is empty.");
    }

    using var reader = new StreamReader(file.OpenReadStream());
    var content = await reader.ReadToEndAsync();
    var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

    var newTrips = new List<ResidencyRoll.Shared.Trips.TripDto>();
    var isFirst = true;

    foreach (var rawLine in lines)
    {
        var line = rawLine.Trim();
        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        if (isFirst && line.StartsWith("CountryName", StringComparison.OrdinalIgnoreCase))
        {
            isFirst = false;
            continue;
        }

        isFirst = false;

        var parts = ParseCsvLine(line);
        if (parts.Length < 3)
        {
            continue;
        }

        if (!DateTime.TryParse(parts[1], CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
        {
            continue;
        }

        if (!DateTime.TryParse(parts[2], CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
        {
            continue;
        }

        newTrips.Add(new ResidencyRoll.Shared.Trips.TripDto
        {
            CountryName = parts[0],
            StartDate = start,
            EndDate = end
        });
    }

    if (newTrips.Count == 0)
    {
        return Results.BadRequest("No valid trips found in file.");
    }

    foreach (var trip in newTrips)
    {
        await apiClient.CreateTripAsync(trip);
    }

    return Results.Ok(new { Imported = newTrips.Count });
}).DisableAntiforgery();

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
    var response = await apiClient.ForecastDaysWithTripAsync(request.CountryName!, request.TripStart, request.TripEnd);
    return Results.Ok(response);
}).DisableAntiforgery();

tripsProxy.MapPost("/forecast/max-end-date", async (MaxTripEndDateRequestDto request, TripsApiClient apiClient) =>
{
    var response = await apiClient.CalculateMaxTripEndDateAsync(request.CountryName!, request.TripStart, request.DayLimit);
    return Results.Ok(response);
}).DisableAntiforgery();

tripsProxy.MapPost("/forecast/standard-durations", async (StandardDurationForecastRequestDto request, TripsApiClient apiClient) =>
{
    var response = await apiClient.CalculateStandardDurationForecastsAsync(request.CountryName!, request.TripStart, request.DayLimit);
    return Results.Ok(response);
}).DisableAntiforgery();

static string EscapeCsv(string value)
{
    if (value.Contains(',') || value.Contains('"'))
    {
        return '"' + value.Replace("\"", "\"\"") + '"';
    }
    return value;
}

static string[] ParseCsvLine(string line)
{
    var values = new List<string>();
    var current = new System.Text.StringBuilder();
    var inQuotes = false;

    for (int i = 0; i < line.Length; i++)
    {
        var c = line[i];

        if (c == '"')
        {
            if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
            {
                current.Append('"');
                i++;
            }
            else
            {
                inQuotes = !inQuotes;
            }
        }
        else if (c == ',' && !inQuotes)
        {
            values.Add(current.ToString());
            current.Clear();
        }
        else
        {
            current.Append(c);
        }
    }

    values.Add(current.ToString());
    return values.ToArray();
}

app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
