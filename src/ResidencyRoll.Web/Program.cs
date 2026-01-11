using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Radzen;
using System.Globalization;
using ResidencyRoll.Web.Components;
using ResidencyRoll.Web.Data;
using ResidencyRoll.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add Radzen services
builder.Services.AddRadzenComponents();

// Configure SQLite database
var dataDirectory = builder.Environment.IsDevelopment() 
    ? Path.Combine(builder.Environment.ContentRootPath, "data")
    : Path.Combine("/app", "data");
Directory.CreateDirectory(dataDirectory);
var connectionString = $"Data Source={Path.Combine(dataDirectory, "residencyroll.db")}";
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// Provide HttpClient with the app base address so relative API calls (e.g., import) work.
builder.Services.AddScoped(sp =>
{
    var navigation = sp.GetRequiredService<NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(navigation.BaseUri) };
});

// Add typed HTTP client for API
var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "https://localhost:5000";
builder.Services.AddHttpClient<TripsApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Add application services (kept for now for import/export endpoints)
// TODO: Move import/export to API and remove TripService completely
builder.Services.AddScoped<TripService>();

var app = builder.Build();

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

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/api/trips/export", async (ApplicationDbContext db) =>
{
    var trips = await db.Trips.OrderBy(t => t.StartDate).ToListAsync();
    var csvLines = new List<string> { "CountryName,StartDate,EndDate" };

    foreach (var trip in trips)
    {
        var line = string.Join(',',
            EscapeCsv(trip.CountryName),
            trip.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            trip.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        csvLines.Add(line);
    }

    var bytes = System.Text.Encoding.UTF8.GetBytes(string.Join('\n', csvLines));
    return Results.File(bytes, "text/csv", "trips.csv");
});

app.MapPost("/api/trips/import", async (HttpRequest request, ApplicationDbContext db) =>
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

    var newTrips = new List<ResidencyRoll.Web.Models.Trip>();
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

        newTrips.Add(new ResidencyRoll.Web.Models.Trip
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

    await db.Trips.AddRangeAsync(newTrips);
    await db.SaveChangesAsync();

    return Results.Ok(new { Imported = newTrips.Count });
});

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
