using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using ResidencyRoll.Api.Configuration;
using ResidencyRoll.Api.Data;
using ResidencyRoll.Api.Services;
using ResidencyRoll.Shared.Extensions;
using Serilog;
using System.Reflection;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/api-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

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
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Only require JWT authentication if Authority is configured
var jwtAuthority = builder.Configuration["Jwt:Authority"];
var jwtEnabled = !string.IsNullOrEmpty(jwtAuthority) && jwtAuthority != "https://your-identity-provider.com";

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
                    return Task.CompletedTask;
                },
                OnMessageReceived = context =>
                {
                    // Log when Authorization header is missing or present
                    var hasAuth = context.Request.Headers.ContainsKey("Authorization");
                    Log.Information("[JWT] Authorization header present: {HasAuth}", hasAuth);
                    if (hasAuth)
                    {
                        var authHeader = context.Request.Headers["Authorization"].ToString();
                        Log.Debug("[JWT] Authorization header value: {AuthHeader}", authHeader.Length > 30 ? authHeader.Substring(0, 30) + "..." : authHeader);
                    }
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var claims = string.Join(", ", context.Principal?.Claims.Select(c => $"{c.Type}:{c.Value}") ?? Array.Empty<string>());
                    Log.Information("[JWT] Token validated. Claims: {Claims}", claims);
                    return Task.CompletedTask;
                }
            };
        });
}
else
{
    Log.Warning("JWT authentication is DISABLED. API endpoints are accessible without authentication.");
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

app.MapControllers();

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
