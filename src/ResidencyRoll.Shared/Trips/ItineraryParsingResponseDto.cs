namespace ResidencyRoll.Shared.Trips;

/// <summary>
/// Response DTO for the itinerary parsing API.
/// Contains the extracted flight legs from the parsed itinerary text.
/// </summary>
public class ItineraryParsingResponseDto
{
    /// <summary>
    /// List of flight legs extracted from the itinerary text.
    /// Each leg contains departure airport, departure local time, and arrival airport.
    /// Legs with missing required fields are omitted.
    /// </summary>
    public List<ItineraryFlightLegDto> Legs { get; set; } = new();

    /// <summary>
    /// Optional error message if parsing failed.
    /// When this is set, Legs may be empty or partially populated.
    /// </summary>
    public string? Error { get; set; }
}
