using System.Text.Json.Serialization;

namespace ResidencyRoll.Shared.Trips;

/// <summary>
/// Represents a single flight leg parsed from an itinerary text.
/// This DTO is used for the response from the itinerary parsing API.
/// </summary>
public class ItineraryFlightLegDto
{
    /// <summary>
    /// IATA airport code for the departure airport (e.g., "YHZ", "JFK").
    /// </summary>
    [JsonPropertyName("departure_airport")]
    public required string DepartureAirport { get; set; }

    /// <summary>
    /// ISO 8601 local departure date and time (e.g., "2026-01-23T16:40").
    /// Does not include timezone information - represents local time.
    /// </summary>
    [JsonPropertyName("departure_datetime_local")]
    public required string DepartureDatetimeLocal { get; set; }

    /// <summary>
    /// IATA airport code for the arrival airport (e.g., "YUL", "LHR").
    /// </summary>
    [JsonPropertyName("arrival_airport")]
    public required string ArrivalAirport { get; set; }

    /// <summary>
    /// ISO 8601 local arrival date and time (e.g., "2026-01-23T18:40").
    /// Does not include timezone information - represents local time.
    /// </summary>
    [JsonPropertyName("arrival_datetime_local")]
    public string? ArrivalDatetimeLocal { get; set; }
}
