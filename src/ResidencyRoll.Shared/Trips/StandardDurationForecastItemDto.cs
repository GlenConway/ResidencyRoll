namespace ResidencyRoll.Shared.Trips;

public class StandardDurationForecastItemDto
{
    public int DurationDays { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalDaysInCountry { get; set; }
    public bool ExceedsLimit { get; set; }
}
