using Microsoft.AspNetCore.Components;
using ResidencyRoll.Shared.Trips;
using ResidencyRoll.Web.Services;

namespace ResidencyRoll.Web.Components.Pages;

public partial class Forecast
{
    // Departure fields
    private string departureCity = string.Empty;
    private string departureCountry = string.Empty;
    private string departureTimezone = "UTC";
    private string? departureIataCode;
    private DateTime? departureDate = DateTime.Today.AddMonths(1).AddDays(30);
    private DateTime? departureTime = new DateTime(1, 1, 1, 12, 0, 0);
    
    // Arrival fields
    private string arrivalCity = string.Empty;
    private string arrivalCountry = string.Empty;
    private string arrivalTimezone = "UTC";
    private string? arrivalIataCode;
    private DateTime? arrivalDate = DateTime.Today.AddMonths(1);
    private DateTime? arrivalTime = new DateTime(1, 1, 1, 12, 0, 0);
    
    // State
    private bool forecastCalculated = false;
    private Dictionary<string, int> currentDaysPerCountry = new();
    private Dictionary<string, int> forecastDaysPerCountry = new();
    private DateTime? maxEndDateForLimit;
    private List<StandardDurationForecastItemDto> standardDurationForecasts = new();
    
    [Inject] private TripsApiClient ApiClient { get; set; } = default!;

    private async Task CalculateForecast()
    {
        if (string.IsNullOrWhiteSpace(arrivalCountry) || 
            !arrivalDate.HasValue || !arrivalTime.HasValue ||
            !departureDate.HasValue || !departureTime.HasValue)
        {
            return;
        }

        var arrivalDateTime = CombineDateAndTime(arrivalDate.Value, arrivalTime.Value);
        var departureDateTime = CombineDateAndTime(departureDate.Value, departureTime.Value);

        var forecastResponse = await ApiClient.ForecastDaysWithTripAsync(
            departureCountry,
            departureCity,
            departureDateTime,
            departureTimezone,
            departureIataCode,
            arrivalCountry,
            arrivalCity,
            arrivalDateTime,
            arrivalTimezone,
            arrivalIataCode
        );

        currentDaysPerCountry = forecastResponse.Current.ToDictionary(c => c.CountryName, c => c.Days);
        forecastDaysPerCountry = forecastResponse.Forecast.ToDictionary(c => c.CountryName, c => c.Days);
        forecastCalculated = true;

        // Calculate 183-day limit planning if forecast exceeds 183 days
        var countryDays = forecastDaysPerCountry.ContainsKey(arrivalCountry) ? forecastDaysPerCountry[arrivalCountry] : 0;
        if (countryDays > 183)
        {
            var maxEnd = await ApiClient.CalculateMaxTripEndDateAsync(
                departureCountry,
                departureCity,
                departureTimezone,
                departureIataCode,
                arrivalCountry,
                arrivalCity,
                arrivalDateTime,
                arrivalTimezone,
                arrivalIataCode,
                183
            );
            maxEndDateForLimit = maxEnd?.MaxEndDate;
        }
        else
        {
            maxEndDateForLimit = null;
        }

        // Calculate standard duration forecasts
        standardDurationForecasts = await ApiClient.CalculateStandardDurationForecastsAsync(
            departureCountry,
            departureCity,
            departureTimezone,
            departureIataCode,
            arrivalCountry,
            arrivalCity,
            arrivalDateTime,
            arrivalTimezone,
            arrivalIataCode,
            183
        );
    }

    private DateTime CombineDateAndTime(DateTime date, DateTime time)
    {
        return new DateTime(date.Year, date.Month, date.Day, time.Hour, time.Minute, time.Second);
    }

    private int GetTripDurationDays()
    {
        if (!arrivalDate.HasValue || !arrivalTime.HasValue || 
            !departureDate.HasValue || !departureTime.HasValue)
        {
            return 0;
        }

        var arrivalDateTime = CombineDateAndTime(arrivalDate.Value, arrivalTime.Value);
        var departureDateTime = CombineDateAndTime(departureDate.Value, departureTime.Value);
        
        // Convert to UTC for accurate calculation
        var arrivalUtc = ConvertToUtc(arrivalDateTime, arrivalTimezone);
        var departureUtc = ConvertToUtc(departureDateTime, departureTimezone);
        
        return Math.Max(0, (int)(departureUtc - arrivalUtc).TotalDays);
    }

    private DateTime GetArrivalDateTime()
    {
        if (!arrivalDate.HasValue || !arrivalTime.HasValue)
        {
            return DateTime.Today;
        }

        return CombineDateAndTime(arrivalDate.Value, arrivalTime.Value);
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
            return localTime;
        }
    }
}
