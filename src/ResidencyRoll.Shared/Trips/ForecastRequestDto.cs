namespace ResidencyRoll.Shared.Trips;

public class ForecastRequestDto
{
    public string CountryName { get; set; } = string.Empty;
    public DateTime TripStart { get; set; }
    public DateTime TripEnd { get; set; }
}
