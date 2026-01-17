namespace ResidencyRoll.Shared.Trips;

/// <summary>
/// Represents a location with its IANA timezone identifier
/// </summary>
public class TimezoneLocation
{
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string TimezoneId { get; set; } = string.Empty;
    public string DisplayName => $"{City}, {Country}";
}
