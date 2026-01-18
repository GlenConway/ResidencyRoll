namespace ResidencyRoll.Shared.Trips;

/// <summary>
/// Represents a single leg of a trip (one flight/journey segment)
/// </summary>
public class TripLegDto
{
    // Departure information
    public string DepartureCountry { get; set; } = string.Empty;
    public string DepartureCity { get; set; } = string.Empty;
    public DateTime DepartureDateTime { get; set; }
    public string DepartureTimezone { get; set; } = "UTC";
    public string? DepartureIataCode { get; set; }
    
    // Arrival information
    public string ArrivalCountry { get; set; } = string.Empty;
    public string ArrivalCity { get; set; } = string.Empty;
    public DateTime ArrivalDateTime { get; set; }
    public string ArrivalTimezone { get; set; } = "UTC";
    public string? ArrivalIataCode { get; set; }
}
