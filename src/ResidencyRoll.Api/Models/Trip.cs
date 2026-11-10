namespace ResidencyRoll.Api.Models;

public class Trip
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int TravellerId { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? UpdatedByUserId { get; set; }
    public Traveller? Traveller { get; set; }
    
    // Departure information (departure leg)
    public string DepartureCountry { get; set; } = string.Empty;
    public string DepartureCity { get; set; } = string.Empty;
    public DateTime DepartureDateTime { get; set; }
    public string DepartureTimezone { get; set; } = string.Empty; // IANA timezone (e.g., "America/Toronto")
    public string? DepartureIataCode { get; set; } // IATA airport code (e.g., "YYZ")
    
    // Arrival information (arrival leg)
    public string ArrivalCountry { get; set; } = string.Empty;
    public string ArrivalCity { get; set; } = string.Empty;
    public DateTime ArrivalDateTime { get; set; }
    public string ArrivalTimezone { get; set; } = string.Empty; // IANA timezone (e.g., "Australia/Sydney")
    public string? ArrivalIataCode { get; set; } // IATA airport code (e.g., "SYD")

    public const int MaxSourceLength = 100;
    public const int MaxBookingRefLength = 100;
    public const int MaxNotesLength = 2000;

    public string? Source { get; set; }
    public string? BookingRef { get; set; }
    public string? Notes { get; set; }

    // Duration in hours (timezone-aware)
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
