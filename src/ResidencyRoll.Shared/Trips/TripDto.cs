namespace ResidencyRoll.Shared.Trips;

public class TripDto
{
    public int Id { get; set; }
    
    // Timezone-aware fields (primary)
    public string DepartureCountry { get; set; } = string.Empty;
    public string DepartureCity { get; set; } = string.Empty;
    public DateTime DepartureDateTime { get; set; }
    public string DepartureTimezone { get; set; } = "UTC";
    public string? DepartureIataCode { get; set; }
    
    public string ArrivalCountry { get; set; } = string.Empty;
    public string ArrivalCity { get; set; } = string.Empty;
    public DateTime ArrivalDateTime { get; set; }
    public string ArrivalTimezone { get; set; } = "UTC";
    public string? ArrivalIataCode { get; set; }
    
    // Legacy fields (computed for backward compatibility)
    public string CountryName 
    { 
        get => string.IsNullOrEmpty(ArrivalCountry) ? string.Empty : ArrivalCountry;
        set => ArrivalCountry = value;
    }
    
    public DateTime StartDate 
    { 
        get => ArrivalDateTime == default ? DateTime.Today : ArrivalDateTime;
        set => ArrivalDateTime = value;
    }
    
    public DateTime EndDate 
    { 
        get => DepartureDateTime == default ? DateTime.Today : DepartureDateTime;
        set => DepartureDateTime = value;
    }
    
    public int DurationDays => Math.Max(0, (DepartureDateTime - ArrivalDateTime).Days);
}

