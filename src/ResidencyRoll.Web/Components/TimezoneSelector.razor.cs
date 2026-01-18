using Microsoft.AspNetCore.Components;

namespace ResidencyRoll.Web.Components;

public partial class TimezoneSelector
{
    [Parameter] public string Label { get; set; } = "Location";
    [Parameter] public string City { get; set; } = string.Empty;
    [Parameter] public EventCallback<string> CityChanged { get; set; }
    [Parameter] public string Country { get; set; } = string.Empty;
    [Parameter] public EventCallback<string> CountryChanged { get; set; }
    [Parameter] public string SelectedTimezone { get; set; } = "UTC";
    [Parameter] public EventCallback<string> SelectedTimezoneChanged { get; set; }
    [Parameter] public EventCallback<(string city, string country, string timezone)> OnLocationChanged { get; set; }

    private List<TimezoneData> allTimezones = new();
    private List<TimezoneData> filteredTimezones = new();

    protected override void OnInitialized()
    {
        LoadTimezones();
        FilterTimezones();
    }

    private void LoadTimezones()
    {
        // Common cities with their timezones
        allTimezones = new List<TimezoneData>
        {
            // North America
            new("Vancouver", "Canada", "America/Vancouver"),
            new("Toronto", "Canada", "America/Toronto"),
            new("Montreal", "Canada", "America/Toronto"),
            new("Calgary", "Canada", "America/Edmonton"),
            new("Ottawa", "Canada", "America/Toronto"),
            new("Winnipeg", "Canada", "America/Winnipeg"),
            new("Halifax", "Canada", "America/Halifax"),
            new("St. John's", "Canada", "America/St_Johns"),
            
            new("New York", "United States", "America/New_York"),
            new("Los Angeles", "United States", "America/Los_Angeles"),
            new("Chicago", "United States", "America/Chicago"),
            new("Houston", "United States", "America/Chicago"),
            new("Phoenix", "United States", "America/Phoenix"),
            new("Philadelphia", "United States", "America/New_York"),
            new("San Antonio", "United States", "America/Chicago"),
            new("San Diego", "United States", "America/Los_Angeles"),
            new("Dallas", "United States", "America/Chicago"),
            new("San Jose", "United States", "America/Los_Angeles"),
            new("Austin", "United States", "America/Chicago"),
            new("Jacksonville", "United States", "America/New_York"),
            new("San Francisco", "United States", "America/Los_Angeles"),
            new("Columbus", "United States", "America/New_York"),
            new("Indianapolis", "United States", "America/Indiana/Indianapolis"),
            new("Fort Worth", "United States", "America/Chicago"),
            new("Charlotte", "United States", "America/New_York"),
            new("Seattle", "United States", "America/Los_Angeles"),
            new("Denver", "United States", "America/Denver"),
            new("Washington DC", "United States", "America/New_York"),
            new("Boston", "United States", "America/New_York"),
            new("Nashville", "United States", "America/Chicago"),
            new("Detroit", "United States", "America/Detroit"),
            new("Portland", "United States", "America/Los_Angeles"),
            new("Las Vegas", "United States", "America/Los_Angeles"),
            new("Miami", "United States", "America/New_York"),
            new("Atlanta", "United States", "America/New_York"),
            new("Honolulu", "United States", "Pacific/Honolulu"),
            new("Anchorage", "United States", "America/Anchorage"),
            
            new("Mexico City", "Mexico", "America/Mexico_City"),
            new("Cancun", "Mexico", "America/Cancun"),
            new("Guadalajara", "Mexico", "America/Mexico_City"),
            
            // Europe
            new("London", "United Kingdom", "Europe/London"),
            new("Manchester", "United Kingdom", "Europe/London"),
            new("Birmingham", "United Kingdom", "Europe/London"),
            new("Edinburgh", "United Kingdom", "Europe/London"),
            new("Glasgow", "United Kingdom", "Europe/London"),
            
            new("Paris", "France", "Europe/Paris"),
            new("Berlin", "Germany", "Europe/Berlin"),
            new("Munich", "Germany", "Europe/Berlin"),
            new("Frankfurt", "Germany", "Europe/Berlin"),
            new("Rome", "Italy", "Europe/Rome"),
            new("Milan", "Italy", "Europe/Rome"),
            new("Madrid", "Spain", "Europe/Madrid"),
            new("Barcelona", "Spain", "Europe/Madrid"),
            new("Amsterdam", "Netherlands", "Europe/Amsterdam"),
            new("Brussels", "Belgium", "Europe/Brussels"),
            new("Vienna", "Austria", "Europe/Vienna"),
            new("Zurich", "Switzerland", "Europe/Zurich"),
            new("Geneva", "Switzerland", "Europe/Zurich"),
            new("Prague", "Czech Republic", "Europe/Prague"),
            new("Copenhagen", "Denmark", "Europe/Copenhagen"),
            new("Stockholm", "Sweden", "Europe/Stockholm"),
            new("Oslo", "Norway", "Europe/Oslo"),
            new("Helsinki", "Finland", "Europe/Helsinki"),
            new("Warsaw", "Poland", "Europe/Warsaw"),
            new("Budapest", "Hungary", "Europe/Budapest"),
            new("Athens", "Greece", "Europe/Athens"),
            new("Lisbon", "Portugal", "Europe/Lisbon"),
            new("Dublin", "Ireland", "Europe/Dublin"),
            
            // Asia
            new("Tokyo", "Japan", "Asia/Tokyo"),
            new("Osaka", "Japan", "Asia/Tokyo"),
            new("Seoul", "South Korea", "Asia/Seoul"),
            new("Beijing", "China", "Asia/Shanghai"),
            new("Shanghai", "China", "Asia/Shanghai"),
            new("Hong Kong", "Hong Kong", "Asia/Hong_Kong"),
            new("Singapore", "Singapore", "Asia/Singapore"),
            new("Bangkok", "Thailand", "Asia/Bangkok"),
            new("Kuala Lumpur", "Malaysia", "Asia/Kuala_Lumpur"),
            new("Manila", "Philippines", "Asia/Manila"),
            new("Jakarta", "Indonesia", "Asia/Jakarta"),
            new("Mumbai", "India", "Asia/Kolkata"),
            new("Delhi", "India", "Asia/Kolkata"),
            new("Bangalore", "India", "Asia/Kolkata"),
            new("Dubai", "United Arab Emirates", "Asia/Dubai"),
            new("Tel Aviv", "Israel", "Asia/Tel_Aviv"),
            
            // Oceania
            new("Sydney", "Australia", "Australia/Sydney"),
            new("Melbourne", "Australia", "Australia/Melbourne"),
            new("Brisbane", "Australia", "Australia/Brisbane"),
            new("Perth", "Australia", "Australia/Perth"),
            new("Adelaide", "Australia", "Australia/Adelaide"),
            new("Auckland", "New Zealand", "Pacific/Auckland"),
            new("Wellington", "New Zealand", "Pacific/Auckland"),
            new("Christchurch", "New Zealand", "Pacific/Auckland"),
            
            // South America
            new("São Paulo", "Brazil", "America/Sao_Paulo"),
            new("Rio de Janeiro", "Brazil", "America/Sao_Paulo"),
            new("Buenos Aires", "Argentina", "America/Argentina/Buenos_Aires"),
            new("Santiago", "Chile", "America/Santiago"),
            new("Lima", "Peru", "America/Lima"),
            new("Bogotá", "Colombia", "America/Bogota"),
            
            // Africa
            new("Cairo", "Egypt", "Africa/Cairo"),
            new("Johannesburg", "South Africa", "Africa/Johannesburg"),
            new("Cape Town", "South Africa", "Africa/Johannesburg"),
            new("Lagos", "Nigeria", "Africa/Lagos"),
            new("Nairobi", "Kenya", "Africa/Nairobi"),
        };

        filteredTimezones = allTimezones;
    }

    private void OnCityChanged()
    {
        FilterTimezones();
    }

    private void FilterTimezones()
    {
        if (string.IsNullOrWhiteSpace(City))
        {
            filteredTimezones = allTimezones;
        }
        else
        {
            filteredTimezones = allTimezones
                .Where(tz => tz.City.Contains(City, StringComparison.OrdinalIgnoreCase) ||
                            tz.Country.Contains(City, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    private async Task OnTimezoneChanged()
    {
        var selected = allTimezones.FirstOrDefault(tz => tz.TimezoneId == SelectedTimezone);
        if (selected != null)
        {
            City = selected.City;
            Country = selected.Country;
            await CityChanged.InvokeAsync(City);
            await CountryChanged.InvokeAsync(Country);
            await SelectedTimezoneChanged.InvokeAsync(SelectedTimezone);
            await OnLocationChanged.InvokeAsync((City, Country, SelectedTimezone));
        }
    }

    private string GetTimezoneDisplay()
    {
        var selected = allTimezones.FirstOrDefault(tz => tz.TimezoneId == SelectedTimezone);
        if (selected != null)
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(SelectedTimezone);
                var offset = tz.GetUtcOffset(DateTime.UtcNow);
                return $"{selected.City}, {selected.Country} (UTC{offset:hh\\:mm})";
            }
            catch
            {
                return $"{selected.City}, {selected.Country}";
            }
        }
        return SelectedTimezone;
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
