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
    
    public int DurationDays
    {
        get
        {
            var depUtc = ConvertToUtc(DepartureDateTime, DepartureTimezone);
            var arrUtc = ConvertToUtc(ArrivalDateTime, ArrivalTimezone);
            return Math.Max(0, (int)(arrUtc - depUtc).TotalDays);
        }
    }

    public int DurationHours
    {
        get
        {
            var depUtc = ConvertToUtc(DepartureDateTime, DepartureTimezone);
            var arrUtc = ConvertToUtc(ArrivalDateTime, ArrivalTimezone);
            return Math.Max(0, (int)(arrUtc - depUtc).TotalHours);
        }
    }

    private DateTime ConvertToUtc(DateTime localTime, string timezoneId)
    {
        try
        {
            if (string.IsNullOrEmpty(timezoneId) || timezoneId == "UTC")
                return localTime;

            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            return TimeZoneInfo.ConvertTimeToUtc(localTime, tz);
        }
        catch
        {
            // If timezone conversion fails, return as-is
            return localTime;
        }
    }
}

