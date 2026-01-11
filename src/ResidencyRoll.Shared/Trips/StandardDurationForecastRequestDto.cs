namespace ResidencyRoll.Shared.Trips;

public class StandardDurationForecastRequestDto
{
    public string CountryName { get; set; } = string.Empty;
    public DateTime TripStart { get; set; }
    public int DayLimit { get; set; } = 183;
    public int[]? Durations { get; set; }
}
