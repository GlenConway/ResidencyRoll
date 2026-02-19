namespace ResidencyRoll.Shared.Trips;

/// <summary>
/// Request DTO for parsing a free-form flight itinerary text.
/// The itinerary text can be in any format (copy-pasted from email, booking confirmation, etc.).
/// </summary>
public class ItineraryParsingRequestDto
{
    /// <summary>
    /// The raw itinerary text to be parsed.
    /// Can contain flight information in various formats including seat numbers,
    /// passenger names, cabin class, and other metadata that will be ignored.
    /// </summary>
    public required string ItineraryText { get; set; }
}
