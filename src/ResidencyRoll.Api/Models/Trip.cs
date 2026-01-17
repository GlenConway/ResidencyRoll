namespace ResidencyRoll.Api.Models;

public class Trip
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    
    // Departure information
    public string DepartureCountry { get; set; } = string.Empty;
    public string DepartureCity { get; set; } = string.Empty;
    public DateTime DepartureDateTime { get; set; }
    public string DepartureTimezone { get; set; } = string.Empty; // IANA timezone (e.g., "America/Toronto")
    public string? DepartureIataCode { get; set; } // IATA airport code (e.g., "YYZ")
    
    // Arrival information
    public string ArrivalCountry { get; set; } = string.Empty;
    public string ArrivalCity { get; set; } = string.Empty;
    public DateTime ArrivalDateTime { get; set; }
    public string ArrivalTimezone { get; set; } = string.Empty; // IANA timezone (e.g., "Australia/Sydney")
    public string? ArrivalIataCode { get; set; } // IATA airport code (e.g., "SYD")
    
    // Legacy fields for backward compatibility - map to arrival
    public string CountryName
    {
        get => ArrivalCountry;
        set => ArrivalCountry = value;
    }
    
    public DateTime StartDate
    {
        get => ArrivalDateTime;
        set => ArrivalDateTime = value;
    }
    
    public DateTime EndDate
    {
        get => DepartureDateTime;
        set => DepartureDateTime = value;
    }

    // Legacy duration calculation - kept for backward compatibility but deprecated
    [Obsolete("Use ResidencyCalculationService for accurate residency calculations")]
    public int DurationDays => Math.Max(0, (DepartureDateTime - ArrivalDateTime).Days);
}
