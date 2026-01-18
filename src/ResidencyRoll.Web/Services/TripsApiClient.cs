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

    public async Task<ForecastResponseDto> ForecastDaysWithTripAsync(
        string departureCountry, string departureCity, DateTime departureDateTime, 
        string departureTimezone, string? departureIataCode,
        string arrivalCountry, string arrivalCity, DateTime arrivalDateTime,
        string arrivalTimezone, string? arrivalIataCode)
    {
        var request = new ForecastRequestDto
        {
            DepartureCountry = departureCountry,
            DepartureCity = departureCity,
            DepartureDateTime = departureDateTime,
            DepartureTimezone = departureTimezone,
            DepartureIataCode = departureIataCode,
            ArrivalCountry = arrivalCountry,
            ArrivalCity = arrivalCity,
            ArrivalDateTime = arrivalDateTime,
            ArrivalTimezone = arrivalTimezone,
            ArrivalIataCode = arrivalIataCode
        };

        var response = await _httpClient.PostAsJsonAsync($"{BaseRoute}/forecast", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ForecastResponseDto>())!;
    }

    public async Task<MaxTripEndDateResponseDto> CalculateMaxTripEndDateAsync(
        string departureCountry, string departureCity, string departureTimezone, string? departureIataCode,
        string arrivalCountry, string arrivalCity, DateTime tripStart,
        string arrivalTimezone, string? arrivalIataCode, int dayLimit = 183)
    {
        var request = new MaxTripEndDateRequestDto
        {
            DepartureCountry = departureCountry,
            DepartureCity = departureCity,
            DepartureTimezone = departureTimezone,
            DepartureIataCode = departureIataCode,
            ArrivalCountry = arrivalCountry,
            ArrivalCity = arrivalCity,
            TripStart = tripStart,
            ArrivalTimezone = arrivalTimezone,
            ArrivalIataCode = arrivalIataCode,
            DayLimit = dayLimit
        };

        var response = await _httpClient.PostAsJsonAsync($"{BaseRoute}/forecast/max-end-date", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MaxTripEndDateResponseDto>())!;
    }

    public async Task<List<StandardDurationForecastItemDto>> CalculateStandardDurationForecastsAsync(
        string departureCountry, string departureCity, string departureTimezone, string? departureIataCode,
        string arrivalCountry, string arrivalCity, DateTime tripStart,
        string arrivalTimezone, string? arrivalIataCode, int dayLimit = 183)
    {
        var request = new StandardDurationForecastRequestDto
        {
            DepartureCountry = departureCountry,
            DepartureCity = departureCity,
            DepartureTimezone = departureTimezone,
            DepartureIataCode = departureIataCode,
            ArrivalCountry = arrivalCountry,
            ArrivalCity = arrivalCity,
            TripStart = tripStart,
            ArrivalTimezone = arrivalTimezone,
            ArrivalIataCode = arrivalIataCode,
            DayLimit = dayLimit
        };

        var response = await _httpClient.PostAsJsonAsync($"{BaseRoute}/forecast/standard-durations", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<StandardDurationForecastItemDto>>())!;
    }

    public async Task<List<DailyPresenceDto>> GetDailyPresenceAsync(DateOnly? startDate = null, DateOnly? endDate = null)
    {
        var query = string.Empty;
        if (startDate.HasValue || endDate.HasValue)
        {
            var queryParams = new List<string>();
            if (startDate.HasValue)
                queryParams.Add($"startDate={startDate.Value:yyyy-MM-dd}");
            if (endDate.HasValue)
                queryParams.Add($"endDate={endDate.Value:yyyy-MM-dd}");
            query = "?" + string.Join("&", queryParams);
        }
        
        var result = await _httpClient.GetFromJsonAsync<List<DailyPresenceDto>>($"{BaseRoute}/daily-presence{query}");
        return result ?? new List<DailyPresenceDto>();
    }

    public async Task<List<ResidencySummaryDto>> GetResidencySummaryAsync(DateOnly? startDate = null, DateOnly? endDate = null)
    {
        var query = string.Empty;
        if (startDate.HasValue || endDate.HasValue)
        {
            var queryParams = new List<string>();
            if (startDate.HasValue)
                queryParams.Add($"startDate={startDate.Value:yyyy-MM-dd}");
            if (endDate.HasValue)
                queryParams.Add($"endDate={endDate.Value:yyyy-MM-dd}");
            query = "?" + string.Join("&", queryParams);
        }
        
        var result = await _httpClient.GetFromJsonAsync<List<ResidencySummaryDto>>($"{BaseRoute}/residency-summary{query}");
        return result ?? new List<ResidencySummaryDto>();
    }

    public async Task<(byte[], string)> ExportTripsAsync()
    {
        var response = await _httpClient.GetAsync($"{BaseRoute}/export");
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync();
        
        // Extract filename from Content-Disposition header
        var filename = "trips.csv";
        if (response.Content.Headers.ContentDisposition?.FileName is not null)
        {
            filename = response.Content.Headers.ContentDisposition.FileName.Trim('"');
        }
        
        return (bytes, filename);
    }

    public async Task<(int Imported, string Message, int Errors)> ImportTripsAsync(Stream fileStream, string fileName)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(streamContent, "file", fileName);

        var response = await _httpClient.PostAsync($"{BaseRoute}/import", content);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Import failed: {errorBody}");
        }

        var result = await response.Content.ReadFromJsonAsync<ImportResultDto>();
        return (result?.Imported ?? 0, result?.Message ?? string.Empty, result?.Errors ?? 0);
    }

    private class ImportResultDto
    {
        public int Imported { get; set; }
        public string Message { get; set; } = string.Empty;
        public int Errors { get; set; }
    }
}
