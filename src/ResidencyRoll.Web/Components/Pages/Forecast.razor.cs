using Microsoft.AspNetCore.Components;
using ResidencyRoll.Shared.Trips;
using ResidencyRoll.Web.Services;
using ResidencyRoll.Web.Helpers;

namespace ResidencyRoll.Web.Components.Pages;

public partial class Forecast
{
    // List of trip legs
    private List<TripLegEditModel> legs = new();
    private int nextLegId = 1;
    
    // State
    private bool forecastCalculated = false;
    private Dictionary<string, int> currentDaysPerCountry = new();
    private Dictionary<string, int> forecastDaysPerCountry = new();
    private List<StandardDurationForecastItemDto> standardDurationForecasts = new();
    
    [Inject] private TripsApiClient ApiClient { get; set; } = default!;

    protected override void OnInitialized()
    {
        // Start with one outbound leg
        AddLeg();
    }

    private void AddLeg()
    {
        var lastLeg = legs.LastOrDefault();
        
        var newLeg = new TripLegEditModel
        {
            Id = nextLegId++,
            DepartureCity = lastLeg?.ArrivalCity ?? string.Empty,
            DepartureCountry = lastLeg?.ArrivalCountry ?? string.Empty,
            DepartureTimezone = lastLeg?.ArrivalTimezone ?? "UTC",
            DepartureIataCode = lastLeg?.ArrivalIataCode,
            DepartureDate = lastLeg?.ArrivalDate ?? DateTime.Today.AddMonths(1),
            DepartureTime = lastLeg?.ArrivalTime ?? new DateTime(1, 1, 1, 12, 0, 0),
            ArrivalDate = (lastLeg?.ArrivalDate ?? DateTime.Today.AddMonths(1)).AddDays(1),
            ArrivalTime = new DateTime(1, 1, 1, 12, 0, 0)
        };
        
        legs.Add(newLeg);
    }

    private void RemoveLeg(int legId)
    {
        if (legs.Count > 1)
        {
            legs.RemoveAll(l => l.Id == legId);
        }
    }

    private async Task CalculateForecast()
    {
        // Validate all legs have required information
        if (legs.Any(leg => string.IsNullOrWhiteSpace(leg.ArrivalCountry) || 
                           !leg.ArrivalDate.HasValue || !leg.ArrivalTime.HasValue ||
                           !leg.DepartureDate.HasValue || !leg.DepartureTime.HasValue))
        {
            return;
        }

        // Convert legs to DTOs
        var legDtos = legs.Select(leg => new TripLegDto
        {
            DepartureCountry = leg.DepartureCountry,
            DepartureCity = leg.DepartureCity,
            DepartureDateTime = CombineDateAndTime(leg.DepartureDate!.Value, leg.DepartureTime!.Value),
            DepartureTimezone = leg.DepartureTimezone,
            DepartureIataCode = leg.DepartureIataCode,
            ArrivalCountry = leg.ArrivalCountry,
            ArrivalCity = leg.ArrivalCity,
            ArrivalDateTime = CombineDateAndTime(leg.ArrivalDate!.Value, leg.ArrivalTime!.Value),
            ArrivalTimezone = leg.ArrivalTimezone,
            ArrivalIataCode = leg.ArrivalIataCode
        }).ToList();

        var forecastResponse = await ApiClient.ForecastDaysWithTripsAsync(legDtos);

        currentDaysPerCountry = forecastResponse.Current.ToDictionary(c => c.CountryName, c => c.Days);
        forecastDaysPerCountry = forecastResponse.Forecast.ToDictionary(c => c.CountryName, c => c.Days);
        forecastCalculated = true;

        // Find all countries visited in forecast
        var countriesVisited = legDtos.Select(l => l.ArrivalCountry).Distinct().ToList();
        
        // Calculate 183-day limit planning if any country exceeds 183 days
        foreach (var country in countriesVisited)
        {
            var countryDays = forecastDaysPerCountry.ContainsKey(country) ? forecastDaysPerCountry[country] : 0;
            if (countryDays > 183)
            {
                // For multi-leg trips, user should manually adjust dates
                standardDurationForecasts = new();
                break;
            }
        }
    }

    private DateTime CombineDateAndTime(DateTime date, DateTime time)
    {
        return new DateTime(date.Year, date.Month, date.Day, time.Hour, time.Minute, time.Second);
    }

    private int GetTripDurationDays()
    {
        if (legs.Count == 0) return 0;
        
        var firstLeg = legs.First();
        var lastLeg = legs.Last();
        
        if (!firstLeg.ArrivalDate.HasValue || !firstLeg.ArrivalTime.HasValue || 
            !lastLeg.DepartureDate.HasValue || !lastLeg.DepartureTime.HasValue)
        {
            return 0;
        }

        var arrivalDateTime = CombineDateAndTime(firstLeg.ArrivalDate.Value, firstLeg.ArrivalTime.Value);
        var departureDateTime = CombineDateAndTime(lastLeg.DepartureDate.Value, lastLeg.DepartureTime.Value);
        
        // Convert to UTC for accurate calculation
        var arrivalUtc = ConvertToUtc(arrivalDateTime, firstLeg.ArrivalTimezone);
        var departureUtc = ConvertToUtc(departureDateTime, lastLeg.DepartureTimezone);
        
        return Math.Max(0, (int)(departureUtc - arrivalUtc).TotalDays);
    }

    private DateTime GetFirstArrivalDateTime()
    {
        if (legs.Count == 0) return DateTime.Today;
        
        var firstLeg = legs.First();
        if (!firstLeg.ArrivalDate.HasValue || !firstLeg.ArrivalTime.HasValue)
        {
            return DateTime.Today;
        }

        return CombineDateAndTime(firstLeg.ArrivalDate.Value, firstLeg.ArrivalTime.Value);
    }

    private string GetPrimaryDestinationCountry()
    {
        if (legs.Count == 0) return string.Empty;
        
        // Find the leg with the longest stay (biggest gap between arrival and next departure)
        TripLegEditModel? longestStay = null;
        TimeSpan maxStay = TimeSpan.Zero;
        
        for (int i = 0; i < legs.Count; i++)
        {
            var leg = legs[i];
            if (!leg.ArrivalDate.HasValue || !leg.ArrivalTime.HasValue) continue;
            
            DateTime stayEnd;
            if (i < legs.Count - 1)
            {
                var nextLeg = legs[i + 1];
                if (!nextLeg.DepartureDate.HasValue || !nextLeg.DepartureTime.HasValue) continue;
                stayEnd = CombineDateAndTime(nextLeg.DepartureDate.Value, nextLeg.DepartureTime.Value);
            }
            else
            {
                if (!leg.DepartureDate.HasValue || !leg.DepartureTime.HasValue) continue;
                stayEnd = CombineDateAndTime(leg.DepartureDate.Value, leg.DepartureTime.Value);
            }
            
            var stayStart = CombineDateAndTime(leg.ArrivalDate.Value, leg.ArrivalTime.Value);
            var stayDuration = stayEnd - stayStart;
            
            if (stayDuration > maxStay)
            {
                maxStay = stayDuration;
                longestStay = leg;
            }
        }
        
        return longestStay?.ArrivalCountry ?? legs.First().ArrivalCountry;
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
    
    private class TripLegEditModel
    {
        public int Id { get; set; }
        public string DepartureCity { get; set; } = string.Empty;
        public string DepartureCountry { get; set; } = string.Empty;
        public string DepartureTimezone { get; set; } = "UTC";
        public string? DepartureIataCode { get; set; }
        public DateTime? DepartureDate { get; set; }
        public DateTime? DepartureTime { get; set; }
        
        public string ArrivalCity { get; set; } = string.Empty;
        public string ArrivalCountry { get; set; } = string.Empty;
        public string ArrivalTimezone { get; set; } = "UTC";
        public string? ArrivalIataCode { get; set; }
        public DateTime? ArrivalDate { get; set; }
        public DateTime? ArrivalTime { get; set; }
    }
}
