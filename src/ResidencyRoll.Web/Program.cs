using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using ResidencyRoll.Web.Bff;
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
Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "wwwroot"));

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
    else if (File.Exists(Path.Combine(builder.Environment.ContentRootPath, "ResidencyRoll.Web.csproj")))
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
                if (BffEndpointExtensions.IsApiOrBff(context.Request.Path) || BffEndpointExtensions.IsMcpPassThrough(context.Request.Path))
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

var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "https://localhost:5003";
builder.Services.AddReverseProxy()
    .AddTransforms(transformBuilder =>
    {
        // The cookie token is attached only for the browser's /api calls.
        // /mcp keeps the Authorization header the MCP client sent.
        if (string.Equals(transformBuilder.Route.RouteId, "api", StringComparison.Ordinal))
        {
            transformBuilder.AddRequestTransform(BffProxy.AttachAccessToken);
        }
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
        },
        new RouteConfig
        {
            RouteId = "mcp",
            ClusterId = "api",
            Match = new RouteMatch { Path = "/mcp" }
        },
        new RouteConfig
        {
            RouteId = "mcp-rest",
            ClusterId = "api",
            Match = new RouteMatch { Path = "/mcp/{**rest}" }
        },
        new RouteConfig
        {
            RouteId = "oauth-metadata",
            ClusterId = "api",
            Match = new RouteMatch { Path = "/.well-known/oauth-protected-resource" }
        },
        new RouteConfig
        {
            RouteId = "oauth-metadata-rest",
            ClusterId = "api",
            Match = new RouteMatch { Path = "/.well-known/oauth-protected-resource/{**rest}" }
        }
    ],
    [
        new ClusterConfig
        {
            ClusterId = "api",
            HttpRequest = new ForwarderRequestConfig
            {
                ActivityTimeout = TimeSpan.FromMinutes(5),
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

var app = builder.Build();

// Handle forwarded headers from reverse proxy
app.UseConfiguredForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// IMPORTANT: API endpoints MUST be registered BEFORE MapRazorComponents
// because Razor component routing acts as a catch-all for remaining routes.

// BFF: auth endpoints, CSRF, and the /api reverse proxy. The React build in wwwroot is the UI.
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

app.MapGet("/Error", () => Results.Problem(statusCode: StatusCodes.Status500InternalServerError));

var spa = app.MapFallbackToFile("index.html");
if (oidcEnabled)
{
    spa.RequireAuthorization();
}

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
