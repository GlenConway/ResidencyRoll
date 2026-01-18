namespace ResidencyRoll.Shared.Trips;

public class ForecastRequestDto
{
    // Trip legs (one or more flight segments)
    public List<TripLegDto> Legs { get; set; } = new();
    
    // Legacy single-leg support - these properties map to a single leg
    // Departure information
    public string DepartureCountry
    {
        get => Legs.FirstOrDefault()?.DepartureCountry ?? string.Empty;
        set
        {
            if (Legs.Count == 0) Legs.Add(new TripLegDto());
            Legs[0].DepartureCountry = value;
        }
    }
    
    public string DepartureCity
    {
        get => Legs.FirstOrDefault()?.DepartureCity ?? string.Empty;
        set
        {
            if (Legs.Count == 0) Legs.Add(new TripLegDto());
            Legs[0].DepartureCity = value;
        }
    }
    
    public DateTime DepartureDateTime
    {
        get => Legs.FirstOrDefault()?.DepartureDateTime ?? DateTime.Today;
        set
        {
            if (Legs.Count == 0) Legs.Add(new TripLegDto());
            Legs[0].DepartureDateTime = value;
        }
    }
    
    public string DepartureTimezone
    {
        get => Legs.FirstOrDefault()?.DepartureTimezone ?? "UTC";
        set
        {
            if (Legs.Count == 0) Legs.Add(new TripLegDto());
            Legs[0].DepartureTimezone = value;
        }
    }
    
    public string? DepartureIataCode
    {
        get => Legs.FirstOrDefault()?.DepartureIataCode;
        set
        {
            if (Legs.Count == 0) Legs.Add(new TripLegDto());
            Legs[0].DepartureIataCode = value;
        }
    }
    
    // Arrival information (last leg)
    public string ArrivalCountry
    {
        get => Legs.LastOrDefault()?.ArrivalCountry ?? string.Empty;
        set
        {
            if (Legs.Count == 0) Legs.Add(new TripLegDto());
            Legs[^1].ArrivalCountry = value;
        }
    }
    
    public string ArrivalCity
    {
        get => Legs.LastOrDefault()?.ArrivalCity ?? string.Empty;
        set
        {
            if (Legs.Count == 0) Legs.Add(new TripLegDto());
            Legs[^1].ArrivalCity = value;
        }
    }
    
    public DateTime ArrivalDateTime
    {
        get => Legs.LastOrDefault()?.ArrivalDateTime ?? DateTime.Today;
        set
        {
            if (Legs.Count == 0) Legs.Add(new TripLegDto());
            Legs[^1].ArrivalDateTime = value;
        }
    }
    
    public string ArrivalTimezone
    {
        get => Legs.LastOrDefault()?.ArrivalTimezone ?? "UTC";
        set
        {
            if (Legs.Count == 0) Legs.Add(new TripLegDto());
            Legs[^1].ArrivalTimezone = value;
        }
    }
    
    public string? ArrivalIataCode
    {
        get => Legs.LastOrDefault()?.ArrivalIataCode;
        set
        {
            if (Legs.Count == 0) Legs.Add(new TripLegDto());
            Legs[^1].ArrivalIataCode = value;
        }
    }
    
    // Legacy support for backward compatibility
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
