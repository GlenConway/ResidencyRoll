namespace ResidencyRoll.Shared.Trips;

public class ForecastRequestDto
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
    
    // Legacy support for backward compatibility (optional)
    public string CountryName
    {
        get => string.IsNullOrEmpty(ArrivalCountry) ? string.Empty : ArrivalCountry;
        set => ArrivalCountry = value;
    }
    
    public DateTime TripStart
    {
        get => ArrivalDateTime == default ? DateTime.Today : ArrivalDateTime;
        set => ArrivalDateTime = value;
    }
    
    public DateTime TripEnd
    {
        get => DepartureDateTime == default ? DateTime.Today : DepartureDateTime;
        set => DepartureDateTime = value;
    }
}
