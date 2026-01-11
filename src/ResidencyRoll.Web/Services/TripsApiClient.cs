using System.Net.Http.Json;
using ResidencyRoll.Shared.Trips;

namespace ResidencyRoll.Web.Services;

public class TripsApiClient
{
    private readonly HttpClient _httpClient;
    private const string BaseRoute = "api/v1/trips";

    public TripsApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<TripDto>> GetAllTripsAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<TripDto>>(BaseRoute);
        return result ?? new List<TripDto>();
    }

    public async Task<TripDto?> GetTripByIdAsync(int id)
    {
        return await _httpClient.GetFromJsonAsync<TripDto>($"{BaseRoute}/{id}");
    }

    public async Task<TripDto> CreateTripAsync(TripDto trip)
    {
        var response = await _httpClient.PostAsJsonAsync(BaseRoute, trip);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TripDto>())!;
    }

    public async Task UpdateTripAsync(int id, TripDto trip)
    {
        var response = await _httpClient.PutAsJsonAsync($"{BaseRoute}/{id}", trip);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteTripAsync(int id)
    {
        var response = await _httpClient.DeleteAsync($"{BaseRoute}/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<TripDto>> GetTimelineAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<TripDto>>($"{BaseRoute}/timeline");
        return result ?? new List<TripDto>();
    }

    public async Task<List<CountryDaysDto>> GetTotalDaysPerCountryAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<CountryDaysDto>>($"{BaseRoute}/days-per-country");
        return result ?? new List<CountryDaysDto>();
    }

    public async Task<List<CountryDaysDto>> GetDaysPerCountryInLast365DaysAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<CountryDaysDto>>($"{BaseRoute}/days-per-country/last365");
        return result ?? new List<CountryDaysDto>();
    }

    public async Task<int> GetTotalDaysAwayInLast365DaysAsync()
    {
        return await _httpClient.GetFromJsonAsync<int>($"{BaseRoute}/total-days-away/last365");
    }

    public async Task<int> GetDaysAtHomeInLast365DaysAsync()
    {
        return await _httpClient.GetFromJsonAsync<int>($"{BaseRoute}/days-at-home/last365");
    }

    public async Task<ForecastResponseDto> ForecastDaysWithTripAsync(string countryName, DateTime tripStart, DateTime tripEnd)
    {
        var request = new ForecastRequestDto
        {
            CountryName = countryName,
            TripStart = tripStart,
            TripEnd = tripEnd
        };

        var response = await _httpClient.PostAsJsonAsync($"{BaseRoute}/forecast", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ForecastResponseDto>())!;
    }

    public async Task<MaxTripEndDateResponseDto> CalculateMaxTripEndDateAsync(string countryName, DateTime tripStart, int dayLimit = 183)
    {
        var request = new MaxTripEndDateRequestDto
        {
            CountryName = countryName,
            TripStart = tripStart,
            DayLimit = dayLimit
        };

        var response = await _httpClient.PostAsJsonAsync($"{BaseRoute}/forecast/max-end-date", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MaxTripEndDateResponseDto>())!;
    }

    public async Task<List<StandardDurationForecastItemDto>> CalculateStandardDurationForecastsAsync(
        string countryName, DateTime tripStart, int dayLimit = 183)
    {
        var request = new StandardDurationForecastRequestDto
        {
            CountryName = countryName,
            TripStart = tripStart,
            DayLimit = dayLimit
        };

        var response = await _httpClient.PostAsJsonAsync($"{BaseRoute}/forecast/standard-durations", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<StandardDurationForecastItemDto>>())!;
    }
}
