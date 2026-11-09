using Asp.Versioning;
using Microsoft.AspNetCore.Authentication;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using ResidencyRoll.Api.Authentication;
using ResidencyRoll.Api.Configuration;
using ResidencyRoll.Api.Data;
using ResidencyRoll.Api.Mcp;
using ResidencyRoll.Api.Services;
using ResidencyRoll.Shared.Extensions;
using Serilog;
using System.Reflection;

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
    
    Log.Information("Starting ResidencyRoll API - Version: {Version}", version);

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

const string FrontendCorsPolicy = "Frontend";

builder.Services.AddControllers();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=residencyroll.db"));

builder.Services.AddScoped<TripService>();
builder.Services.AddScoped<ItineraryParsingService>();
builder.Services.AddSingleton<ResidencyCalculationService>();
builder.AddResidencyMcp();

// Weekly SQLite backup (Mondays 04:00 UTC)
builder.Services.Configure<DatabaseBackupOptions>(builder.Configuration.GetSection("DatabaseBackup"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DatabaseBackupService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DatabaseBackupService>());

// Configure OpenAI options for Semantic Kernel
builder.Services.Configure<OpenAIOptions>(builder.Configuration.GetSection("OpenAI"));

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        var origins = McpUrls.AllowedOrigins(builder.Configuration);
        policy.WithOrigins(origins.ToArray())
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("WWW-Authenticate", "Mcp-Session-Id", "MCP-Protocol-Version");
    });
});

// Only require JWT authentication if Authority is configured
var jwtAuthority = builder.Configuration["Jwt:Authority"];
var jwtEnabled = McpEndpointExtensions.IsJwtEnabled(builder.Configuration);

if (jwtEnabled)
{
    Log.Information("JWT authentication is ENABLED. Authority: {Authority}", jwtAuthority);
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = jwtAuthority;
            options.Audience = builder.Configuration["Jwt:Audience"];
            options.RequireHttpsMetadata = builder.Configuration.GetValue<bool?>("Jwt:RequireHttpsMetadata") ?? true;
            
            // Configure token decryption for JWE (encrypted JWT) tokens
            // Auth0 can issue encrypted access tokens when using a confidential client
            var clientSecret = builder.Configuration["Jwt:ClientSecret"];
            if (!string.IsNullOrEmpty(clientSecret))
            {
                Log.Information("JWT client secret configured - JWE token decryption enabled");
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    // Use client secret for decrypting JWE tokens
                    TokenDecryptionKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(clientSecret))
                };
                McpEndpointExtensions.ApplyAudiences(builder.Configuration, options.TokenValidationParameters);
            }
            else
            {
                Log.Information("JWT client secret NOT configured - only JWT (non-encrypted) tokens supported");
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true
                };
                McpEndpointExtensions.ApplyAudiences(builder.Configuration, options.TokenValidationParameters);
            }

            // Add diagnostic logging to understand 401 causes during development
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    Log.Error("[JWT] Authentication failed: {Message}", context.Exception.Message);
                    if (context.Exception.InnerException != null)
                    {
                        Log.Error("[JWT] Inner: {Message}", context.Exception.InnerException.Message);
                    }
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    Log.Warning("[JWT] Challenge issued. Error: {Error}, Description: {Description}", context.Error, context.ErrorDescription);
                    if (context.Request.Path.StartsWithSegments("/mcp"))
                    {
                        var metadata = McpUrls.Metadata(context.Request, context.HttpContext.RequestServices.GetRequiredService<IConfiguration>());
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.Headers.WWWAuthenticate =
                            $"Bearer resource_metadata=\"{metadata}\", scope=\"read:trips\"";
                    }

                    return Task.CompletedTask;
                },
                OnMessageReceived = context =>
                {
                    Log.Debug("[JWT] Authorization header present: {HasAuth}", context.Request.Headers.ContainsKey("Authorization"));
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var claimNames = string.Join(", ", context.Principal?.Claims.Select(c => c.Type).Distinct() ?? Array.Empty<string>());
                    Log.Debug("[JWT] Token validated. Claim types: {ClaimTypes}", claimNames);
                    return Task.CompletedTask;
                }
            };
        });
}
else
{
    Log.Warning("JWT authentication is DISABLED. Requests run as the local user.");
    builder.Services.AddAuthentication(LocalUserAuthenticationHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, LocalUserAuthenticationHandler>(LocalUserAuthenticationHandler.SchemeName, _ => { });
}

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();

var app = builder.Build();

// Handle forwarded headers from reverse proxy
app.UseConfiguredForwardedHeaders();

// Ensure database is created and migrations are applied
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", $"ResidencyRoll API {description.GroupName.ToUpperInvariant()}");
        }
    });
    
    // Redirect root to Swagger UI in development
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

app.UseHttpsRedirection();

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();
app.UseResidencyMcpGuards();

app.MapResidencyMcp();
app.MapControllers();

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

/// <summary>Marker so tests can boot this host without colliding with the web project's Program class.</summary>
public sealed class ApiEntryPoint;
