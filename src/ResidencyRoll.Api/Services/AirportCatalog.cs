using System.Reflection;
using System.Text.Json;
using ResidencyRoll.Shared.Trips;

namespace ResidencyRoll.Api.Services;

/// <summary>
/// IATA catalogue shared with the React app. The JSON file lives in ResidencyRoll.Shared.
/// </summary>
public sealed class AirportCatalog
{
    public const string ResourceName = "ResidencyRoll.Shared.Airports.airports.json";

    private readonly IReadOnlyList<Airport> _airports;
    private readonly Dictionary<string, Airport> _byCode;

    public AirportCatalog()
    {
        _airports = Load();
        _byCode = _airports.ToDictionary(airport => airport.IataCode, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<Airport> All => _airports;

    public Airport? FindByIataCode(string? iataCode)
    {
        if (string.IsNullOrWhiteSpace(iataCode))
        {
            return null;
        }

        return _byCode.TryGetValue(iataCode.Trim(), out var airport) ? airport : null;
    }

    public IReadOnlyList<Airport> Search(string? query, int limit = 50)
    {
        if (limit <= 0)
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return _airports.Take(limit).ToArray();
        }

        var term = query.Trim();
        return _airports
            .Where(airport => airport.SearchText.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(airport => Rank(airport, term))
            .ThenBy(airport => airport.City, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
    }

    private static int Rank(Airport airport, string term)
    {
        if (airport.IataCode.Equals(term, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (airport.City.StartsWith(term, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }

    private static IReadOnlyList<Airport> Load()
    {
        var assembly = typeof(TripDto).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            var names = string.Join(", ", assembly.GetManifestResourceNames());
            throw new InvalidOperationException($"Airport catalogue resource '{ResourceName}' was not found. Embedded resources: {names}");
        }

        var records = JsonSerializer.Deserialize<List<AirportRecord>>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Airport catalogue JSON was empty.");

        return records.Select(record => new Airport
        {
            IataCode = record.IataCode,
            AirportName = record.AirportName,
            City = record.City,
            Country = record.Country,
            IanaTimezone = record.IanaTimezone,
            SearchText = $"{record.IataCode} {record.City} {record.AirportName} {record.Country}"
        }).ToArray();
    }

    private sealed class AirportRecord
    {
        public string IataCode { get; set; } = string.Empty;
        public string AirportName { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string IanaTimezone { get; set; } = string.Empty;
    }
}

public sealed class Airport
{
    public required string IataCode { get; init; }
    public required string AirportName { get; init; }
    public required string City { get; init; }
    public required string Country { get; init; }
    public required string IanaTimezone { get; init; }
    public required string SearchText { get; init; }
}
