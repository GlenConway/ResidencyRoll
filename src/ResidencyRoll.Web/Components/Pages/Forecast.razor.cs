using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Radzen;
using ResidencyRoll.Shared.Trips;
using ResidencyRoll.Web.Services;
using ResidencyRoll.Web.Helpers;
using ResidencyRoll.Web.Data;
using System.Globalization;
using ResidencyRoll.Web.Components.Dialogs;

namespace ResidencyRoll.Web.Components.Pages;

public partial class Forecast
{
    // List of trip legs
    private List<TripLegEditModel> legs = new();
    private int nextLegId = 0;
    private bool hasInitialized = false;
    
    // State
    private bool forecastCalculated = false;
    private Dictionary<string, int> currentDaysPerCountry = new();
    private Dictionary<string, int> forecastDaysPerCountry = new();
    private List<StandardDurationForecastItemDto> standardDurationForecasts = new();
    private List<string> validationIssues = new();
    private string? homeCountry;
    
    // Itinerary parsing state
    private string itineraryParsingError = string.Empty;
    private bool parsingSuccess = false;
    private int parsedLegsCount = 0;
    private bool isItineraryParserAvailable = false;
    private bool isItineraryParserAvailabilityChecked = false;
    
    [Inject] private TripsApiClient ApiClient { get; set; } = default!;
    [Inject] private ILogger<Forecast> Logger { get; set; } = default!;
    [Inject] private DialogService DialogService { get; set; } = default!;
    [Inject] private LocalStorageService LocalStorage { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        homeCountry = await LocalStorage.GetItemAsync("residencyroll_home_country");

        if (!hasInitialized && legs.Count == 0)
        {
            hasInitialized = true;
            var newLeg = new TripLegEditModel
            {
                Id = nextLegId++,
                DepartureCity = string.Empty,
                DepartureCountry = string.Empty,
                DepartureTimezone = "UTC",
                DepartureDate = DateTime.Today.AddMonths(1),
                DepartureTime = new DateTime(1, 1, 1, 12, 0, 0),
                ArrivalDate = DateTime.Today.AddMonths(1),
                ArrivalTime = new DateTime(1, 1, 1, 12, 0, 0)
            };
            legs.Add(newLeg);
        }
        await CheckItineraryParserAvailability();
    }

    private async Task CheckItineraryParserAvailability()
    {
        try
        {
            isItineraryParserAvailable = await ApiClient.IsItineraryParsingAvailableAsync();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to check itinerary parsing availability");
            isItineraryParserAvailable = false;
        }
        finally
        {
            isItineraryParserAvailabilityChecked = true;
        }
    }

    private async Task OpenItineraryParserDialog()
    {
        parsingSuccess = false;
        itineraryParsingError = string.Empty;

        var result = await DialogService.OpenAsync<ItineraryParserDialog>(
            "Quick Itinerary Parser",
            options: new DialogOptions
            {
                Width = "680px",
                Resizable = false,
                Draggable = false,
                CloseDialogOnOverlayClick = true
            });

        if (result is ItineraryParsingResponseDto response)
        {
            ApplyParsedLegs(response);
        }
    }

    private void ApplyParsedLegs(ItineraryParsingResponseDto response)
    {
        try
        {
            if (!string.IsNullOrEmpty(response.Error))
            {
                itineraryParsingError = response.Error;
                Logger.LogWarning("Itinerary parsing error: {Error}", response.Error);
                return;
            }

            if (response.Legs.Count == 0)
            {
                itineraryParsingError = "No flight legs could be extracted from the provided itinerary text. Please check the format and try again.";
                Logger.LogWarning("No legs parsed from itinerary");
                return;
            }

            Logger.LogInformation("Successfully parsed {LegCount} flight legs", response.Legs.Count);

            // Clear existing legs and add parsed ones
            legs.Clear();
            nextLegId = 0;

            foreach (var parsedLeg in response.Legs)
            {
                // Look up airport information by IATA code
                var departureAirport = AirportDatabase.FindByIataCode(parsedLeg.DepartureAirport);
                var arrivalAirport = AirportDatabase.FindByIataCode(parsedLeg.ArrivalAirport);

                var leg = new TripLegEditModel
                {
                    Id = nextLegId++,
                    DepartureCity = departureAirport?.City ?? string.Empty,
                    DepartureCountry = departureAirport?.Country ?? string.Empty,
                    DepartureTimezone = departureAirport?.IanaTimezone ?? "UTC",
                    DepartureIataCode = parsedLeg.DepartureAirport,
                    ArrivalCity = arrivalAirport?.City ?? string.Empty,
                    ArrivalCountry = arrivalAirport?.Country ?? string.Empty,
                    ArrivalTimezone = arrivalAirport?.IanaTimezone ?? "UTC",
                    ArrivalIataCode = parsedLeg.ArrivalAirport
                };

                // Parse the ISO 8601 departure datetime
                DateTime departureDate = DateTime.Today.AddMonths(1);
                DateTime departureTime = new DateTime(1, 1, 1, 12, 0, 0);

                if (DateTime.TryParseExact(parsedLeg.DepartureDatetimeLocal, "yyyy-MM-ddTHH:mm",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var departureDateTime))
                {
                    departureDate = departureDateTime.Date;
                    departureTime = departureDateTime;
                }
                else
                {
                    Logger.LogWarning("Could not parse departure datetime: {DateTime}", parsedLeg.DepartureDatetimeLocal);
                }

                leg.DepartureDate = departureDate;
                leg.DepartureTime = departureTime;

                // Parse the ISO 8601 arrival datetime
                DateTime arrivalDate = departureDate;
                DateTime arrivalTime = new DateTime(1, 1, 1, 14, 0, 0); // Default 2:00 PM

                if (!string.IsNullOrEmpty(parsedLeg.ArrivalDatetimeLocal) &&
                    DateTime.TryParseExact(parsedLeg.ArrivalDatetimeLocal, "yyyy-MM-ddTHH:mm",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var arrivalDateTime))
                {
                    arrivalDate = arrivalDateTime.Date;
                    arrivalTime = arrivalDateTime;
                }
                else if (!string.IsNullOrEmpty(parsedLeg.ArrivalDatetimeLocal))
                {
                    Logger.LogWarning("Could not parse arrival datetime: {DateTime}", parsedLeg.ArrivalDatetimeLocal);
                }

                leg.ArrivalDate = arrivalDate;
                leg.ArrivalTime = arrivalTime;

                legs.Add(leg);
            }

            parsedLegsCount = response.Legs.Count;
            parsingSuccess = true;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            itineraryParsingError = $"Error parsing itinerary: {ex.Message}";
            Logger.LogError(ex, "Exception while parsing itinerary");
        }
    }

    private void AddLeg()
    {
        try
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
                ArrivalDate = lastLeg?.ArrivalDate ?? DateTime.Today.AddMonths(1),
                ArrivalTime = new DateTime(1, 1, 1, 12, 0, 0)
            };
            
            legs.Add(newLeg);
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error adding leg to forecast");
            throw;
        }
    }

    private void RemoveLeg(int legId)
    {
        if (legs.Count > 1)
        {
            legs.RemoveAll(l => l.Id == legId);
            StateHasChanged();
        }
    }

    private async Task CalculateForecast()
    {
        try
        {
            // Validate all legs have required information
            var invalidLegs = legs.Where(leg => string.IsNullOrWhiteSpace(leg.ArrivalCountry) || 
                               !leg.ArrivalDate.HasValue || !leg.ArrivalTime.HasValue ||
                               !leg.DepartureDate.HasValue || !leg.DepartureTime.HasValue).ToList();
            
            if (invalidLegs.Any())
            {
                validationIssues = BuildValidationIssues(invalidLegs);
                forecastCalculated = false;
                Logger.LogWarning("Validation failed. Invalid legs: {Count}. Missing data: {Issues}", 
                    invalidLegs.Count,
                    string.Join("; ", validationIssues));
                return;
            }

            validationIssues.Clear();

            Logger.LogInformation("Calculating forecast with {LegCount} legs", legs.Count);

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

            Logger.LogInformation("Forecast response received. Current: {CurrentCount} countries, Forecast: {ForecastCount} countries",
                forecastResponse.Current.Count, forecastResponse.Forecast.Count);

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
        catch (HttpRequestException ex)
        {
            forecastCalculated = false;
            validationIssues = new List<string>
            {
                "Unable to reach the API. Make sure the API project is running and accessible at https://localhost:5003."
            };
            Logger.LogError(ex, "API connection error calculating forecast with legs: {LegCount}", legs.Count);
        }
        catch (Exception ex)
        {
            forecastCalculated = false;
            validationIssues = new List<string>
            {
                "An unexpected error occurred while calculating the forecast. Check the logs for details."
            };
            Logger.LogError(ex, "Error calculating forecast with legs: {LegCount}", legs.Count);
        }
    }

    private List<string> BuildValidationIssues(List<TripLegEditModel> invalidLegs)
    {
        var issues = new List<string>();
        foreach (var item in invalidLegs.Select((leg, idx) => (leg, idx)))
        {
            var missingFields = new List<string>();
            if (string.IsNullOrWhiteSpace(item.leg.ArrivalCountry)) missingFields.Add("arrival country");
            if (!item.leg.ArrivalDate.HasValue) missingFields.Add("arrival date");
            if (!item.leg.ArrivalTime.HasValue) missingFields.Add("arrival time");
            if (!item.leg.DepartureDate.HasValue) missingFields.Add("departure date");
            if (!item.leg.DepartureTime.HasValue) missingFields.Add("departure time");

            if (missingFields.Count > 0)
            {
                issues.Add($"Leg {item.idx + 1}: missing {string.Join(", ", missingFields)}");
            }
        }

        return issues;
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

    private void OnDepartureDateChanged(TripLegEditModel leg, DateTime? newDate)
    {
        Logger.LogInformation("OnDepartureDateChanged called. Leg ID: {LegId}, New Departure: {NewDate}, Current Arrival: {ArrivalDate}", 
            leg.Id, newDate, leg.ArrivalDate);
        
        // Keep arrival date in sync with departure date (same date for flight legs)
        leg.ArrivalDate = newDate;
        Logger.LogInformation("Updated arrival date to: {NewArrivalDate}", leg.ArrivalDate);
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

    private bool IsHomeCountry(string countryName)
    {
        return !string.IsNullOrWhiteSpace(homeCountry)
            && string.Equals(countryName, homeCountry, StringComparison.OrdinalIgnoreCase);
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
