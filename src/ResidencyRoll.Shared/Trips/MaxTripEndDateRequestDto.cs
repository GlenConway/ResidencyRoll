namespace ResidencyRoll.Shared.Trips;

public class MaxTripEndDateRequestDto
{
    // Departure information
    public string DepartureCountry { get; set; } = string.Empty;
    public string DepartureCity { get; set; } = string.Empty;
    public string DepartureTimezone { get; set; } = "UTC";
    public string? DepartureIataCode { get; set; }
    
    // Arrival information
    public string ArrivalCountry { get; set; } = string.Empty;
    public string ArrivalCity { get; set; } = string.Empty;
    public DateTime TripStart { get; set; }
    public string ArrivalTimezone { get; set; } = "UTC";
    public string? ArrivalIataCode { get; set; }
    
    public int DayLimit { get; set; } = 183;
    
    // Legacy support
    public string CountryName
    {
        get => string.IsNullOrEmpty(ArrivalCountry) ? string.Empty : ArrivalCountry;
        set => ArrivalCountry = value;
    }
}
