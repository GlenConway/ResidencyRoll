namespace ResidencyRoll.Shared.Trips;

public class ForecastResponseDto
{
    public List<CountryDaysDto> Current { get; set; } = new();
    public List<CountryDaysDto> Forecast { get; set; } = new();
}
