using Microsoft.AspNetCore.Components;
using Radzen;
using ResidencyRoll.Web.Data;

namespace ResidencyRoll.Web.Components;

public partial class AirportSelector
{
    [Parameter] public string Label { get; set; } = "Location";
    [Parameter] public string City { get; set; } = string.Empty;
    [Parameter] public EventCallback<string> CityChanged { get; set; }
    [Parameter] public string Country { get; set; } = string.Empty;
    [Parameter] public EventCallback<string> CountryChanged { get; set; }
    [Parameter] public string SelectedTimezone { get; set; } = "UTC";
    [Parameter] public EventCallback<string> SelectedTimezoneChanged { get; set; }
    [Parameter] public string? IataCode { get; set; }
    [Parameter] public EventCallback<string?> IataCodeChanged { get; set; }
    [Parameter] public bool ShowManualOverride { get; set; } = true;
    [Parameter] public bool ShowTimezoneOffset { get; set; } = true;

    private string searchText = string.Empty;
    private List<AirportData> filteredAirports = new();
    private List<TimezoneData> allTimezones = new();
    private bool manualOverride = false;
    private AirportData? selectedAirport;

    protected override void OnInitialized()
    {
        LoadTimezones();
        // Show first 20 airports initially (includes international airports)
        filteredAirports = AirportDatabase.GetAllAirports().Take(20).ToList();
        
        UpdateSearchTextFromParameters();
    }

    protected override void OnParametersSet()
    {
        UpdateSearchTextFromParameters();
    }

    private void UpdateSearchTextFromParameters()
    {
        // If IataCode is provided, initialize with that airport
        if (!string.IsNullOrEmpty(IataCode))
        {
            selectedAirport = AirportDatabase.FindByIataCode(IataCode);
            if (selectedAirport != null)
            {
                searchText = selectedAirport.DisplayName;
            }
        }
        // Otherwise, if City is provided, try to find matching airport
        else if (!string.IsNullOrEmpty(City))
        {
            var airports = AirportDatabase.SearchAirports(City);
            selectedAirport = airports.FirstOrDefault(a => 
                a.City.Equals(City, StringComparison.OrdinalIgnoreCase));
            if (selectedAirport != null)
            {
                searchText = selectedAirport.DisplayName;
            }
            else
            {
                // If no matching airport found, just show the city
                searchText = string.Empty;
            }
        }
        else
        {
            searchText = string.Empty;
        }
    }

    private void LoadTimezones()
    {
        // Common timezones for manual override
        allTimezones = new List<TimezoneData>
        {
            // North America
            new("Vancouver", "Canada", "America/Vancouver"),
            new("Toronto", "Canada", "America/Toronto"),
            new("Montreal", "Canada", "America/Toronto"),
            new("Calgary", "Canada", "America/Edmonton"),
            new("Halifax", "Canada", "America/Halifax"),
            new("St. John's", "Canada", "America/St_Johns"),
            new("New York", "United States", "America/New_York"),
            new("Los Angeles", "United States", "America/Los_Angeles"),
            new("Chicago", "United States", "America/Chicago"),
            new("Denver", "United States", "America/Denver"),
            new("Phoenix", "United States", "America/Phoenix"),
            new("Honolulu", "United States", "Pacific/Honolulu"),
            new("Anchorage", "United States", "America/Anchorage"),
            
            // Europe
            new("London", "United Kingdom", "Europe/London"),
            new("Paris", "France", "Europe/Paris"),
            new("Berlin", "Germany", "Europe/Berlin"),
            new("Rome", "Italy", "Europe/Rome"),
            new("Madrid", "Spain", "Europe/Madrid"),
            new("Amsterdam", "Netherlands", "Europe/Amsterdam"),
            new("Zurich", "Switzerland", "Europe/Zurich"),
            
            // Asia
            new("Tokyo", "Japan", "Asia/Tokyo"),
            new("Seoul", "South Korea", "Asia/Seoul"),
            new("Hong Kong", "Hong Kong", "Asia/Hong_Kong"),
            new("Singapore", "Singapore", "Asia/Singapore"),
            new("Bangkok", "Thailand", "Asia/Bangkok"),
            new("Dubai", "United Arab Emirates", "Asia/Dubai"),
            new("Delhi", "India", "Asia/Kolkata"),
            new("Shanghai", "China", "Asia/Shanghai"),
            
            // Oceania
            new("Sydney", "Australia", "Australia/Sydney"),
            new("Melbourne", "Australia", "Australia/Melbourne"),
            new("Brisbane", "Australia", "Australia/Brisbane"),
            new("Perth", "Australia", "Australia/Perth"),
            new("Auckland", "New Zealand", "Pacific/Auckland"),
            
            // South America
            new("São Paulo", "Brazil", "America/Sao_Paulo"),
            new("Buenos Aires", "Argentina", "America/Argentina/Buenos_Aires"),
            new("Santiago", "Chile", "America/Santiago"),
        };
    }

    private void OnLoadData(LoadDataArgs args)
    {
        var searchTerm = args.Filter;
        
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            // Show popular airports when no search term
            filteredAirports = AirportDatabase.GetAllAirports().Take(20).ToList();
        }
        else
        {
            // Search and return up to 50 results
            filteredAirports = AirportDatabase.SearchAirports(searchTerm).Take(50).ToList();
        }
        
        // Force UI update
        InvokeAsync(StateHasChanged);
    }

    private async Task OnAirportSelected(object value)
    {
        if (manualOverride)
            return;

        var selectedText = value?.ToString() ?? string.Empty;
        
        // Try to find the airport by matching the display name
        selectedAirport = filteredAirports.FirstOrDefault(a => a.DisplayName == selectedText);
        
        // If not found, try searching by the text (might be IATA code)
        if (selectedAirport == null && selectedText.Length >= 3)
        {
            var searchResults = AirportDatabase.SearchAirports(selectedText);
            selectedAirport = searchResults.FirstOrDefault();
        }

        if (selectedAirport != null)
        {
            // Populate all fields from airport data
            City = selectedAirport.City;
            Country = selectedAirport.Country;
            SelectedTimezone = selectedAirport.IanaTimezone;
            IataCode = selectedAirport.IataCode;
            searchText = selectedAirport.DisplayName;

            // Notify parent components - await all to ensure they complete
            await CityChanged.InvokeAsync(City);
            await CountryChanged.InvokeAsync(Country);
            await SelectedTimezoneChanged.InvokeAsync(SelectedTimezone);
            await IataCodeChanged.InvokeAsync(IataCode);

            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task OnManualTimezoneChanged()
    {
        var selected = allTimezones.FirstOrDefault(tz => tz.TimezoneId == SelectedTimezone);
        if (selected != null)
        {
            City = selected.City;
            Country = selected.Country;
            IataCode = null; // Clear IATA code when manually selecting
            
            await CityChanged.InvokeAsync(City);
            await CountryChanged.InvokeAsync(Country);
            await SelectedTimezoneChanged.InvokeAsync(SelectedTimezone);
            await IataCodeChanged.InvokeAsync(IataCode);
        }
    }

    private string GetTimezoneOffset()
    {
        if (string.IsNullOrEmpty(SelectedTimezone))
            return string.Empty;

        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(SelectedTimezone);
            var offset = tz.GetUtcOffset(DateTime.UtcNow);
            var sign = offset < TimeSpan.Zero ? "-" : "+";
            return $"UTC{sign}{Math.Abs(offset.Hours):D2}:{Math.Abs(offset.Minutes):D2}";
        }
        catch
        {
            return SelectedTimezone;
        }
    }

    private class TimezoneData
    {
        public string City { get; set; }
        public string Country { get; set; }
        public string TimezoneId { get; set; }
        public string DisplayName => $"{City}, {Country} ({TimezoneId})";

        public TimezoneData(string city, string country, string timezoneId)
        {
            City = city;
            Country = country;
            TimezoneId = timezoneId;
        }
    }
}
