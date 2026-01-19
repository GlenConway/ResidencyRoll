using Microsoft.AspNetCore.Components;
using ResidencyRoll.Shared.Trips;
using ResidencyRoll.Web.Services;

namespace ResidencyRoll.Web.Components;

public partial class ResidencyTimeline
{
    private DateTime? startDate;
    private DateTime? endDate;
    private List<DailyPresenceDto> dailyPresence = new();
    private DailyPresenceDto? selectedDay;
    private bool loading = false;
    private string? homeCountry;
    private string? _previousHomeCountry;
    private List<string> availableCountries = new();

    [Inject] private TripsApiClient ApiClient { get; set; } = default!;
    [Inject] private CountryColorService ColorService { get; set; } = default!;
    [Inject] private LocalStorageService LocalStorage { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        // Default to last 365 days
        endDate = DateTime.Today;
        startDate = DateTime.Today.AddDays(-365);
        
        // Load saved home country from localStorage
        homeCountry = await LocalStorage.GetItemAsync("residencyroll_home_country");
        
        await LoadData();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Handle home country changes after render
        if (homeCountry != _previousHomeCountry)
        {
            _previousHomeCountry = homeCountry;
            ColorService.HomeCountry = homeCountry;
            
            // Save to localStorage
            if (!string.IsNullOrEmpty(homeCountry))
            {
                await LocalStorage.SetItemAsync("residencyroll_home_country", homeCountry);
            }
            
            StateHasChanged();
        }
        await base.OnAfterRenderAsync(firstRender);
    }

    private async Task LoadData()
    {
        loading = true;
        StateHasChanged();

        try
        {
            var start = startDate.HasValue ? DateOnly.FromDateTime(startDate.Value) : (DateOnly?)null;
            var end = endDate.HasValue ? DateOnly.FromDateTime(endDate.Value) : (DateOnly?)null;
            
            dailyPresence = await ApiClient.GetDailyPresenceAsync(start, end);
            
            // Sort dailyPresence by date descending for display
            dailyPresence = dailyPresence.OrderByDescending(d => d.Date).ToList();

            // Build list of available countries from both LocationAtMidnight AND LocationsDuringDay
            var allCountries = new HashSet<string>();
            
            foreach (var presence in dailyPresence)
            {
                // Add from LocationAtMidnight (if not transit)
                if (!string.IsNullOrEmpty(presence.LocationAtMidnight) 
                    && presence.LocationAtMidnight != "IN_TRANSIT")
                {
                    allCountries.Add(presence.LocationAtMidnight);
                }
                
                // Add all countries from LocationsDuringDay (Partial Day Rule)
                if (presence.LocationsDuringDay != null)
                {
                    foreach (var country in presence.LocationsDuringDay)
                    {
                        if (!string.IsNullOrEmpty(country))
                        {
                            allCountries.Add(country);
                        }
                    }
                }
            }
            
            availableCountries = allCountries.OrderBy(c => c).ToList();
            ColorService.RegisterCountries(availableCountries);
            
            // Set home country if not already set or if saved country is not in the list
            if ((string.IsNullOrEmpty(homeCountry) || !availableCountries.Contains(homeCountry)) 
                && availableCountries.Count > 0)
            {
                homeCountry = availableCountries[0];
                ColorService.HomeCountry = homeCountry;
            }
            else if (!string.IsNullOrEmpty(homeCountry))
            {
                ColorService.HomeCountry = homeCountry;
            }
        }
        finally
        {
            loading = false;
            StateHasChanged();
        }
    }

    private Dictionary<string, List<DailyPresenceDto>> GetGroupedByMonth()
    {
        return dailyPresence
            .GroupBy(d => d.Date.ToString("MMMM yyyy"))
            .OrderByDescending(g => dailyPresence.First(d => d.Date.ToString("MMMM yyyy") == g.Key).Date)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.Date).ToList());
    }

    private string GetDayClass(DailyPresenceDto presence)
    {
        if (presence.IsInTransitAtMidnight)
            return "transit";
        return "location";
    }

    private string GetDayStyle(DailyPresenceDto presence)
    {
        if (presence.LocationAtMidnight == "IN_TRANSIT" 
            || presence.IsInTransitAtMidnight)
            return string.Empty;

        // Handle multiple countries in one day (Partial Day Rule)
        if (presence.LocationsDuringDay?.Count > 1)
        {
            // Create a gradient with multiple country colors
            var colors = presence.LocationsDuringDay
                .Select(country => ColorService.GetCountryColor(country))
                .ToList();
            
            if (colors.Count == 2)
            {
                // Split the cell diagonally or vertically
                return $"background: linear-gradient(135deg, {colors[0]} 0%, {colors[0]} 50%, {colors[1]} 50%, {colors[1]} 100%); color: white;";
            }
            else
            {
                // For 3+ countries, use striped pattern
                var gradientStops = new List<string>();
                var step = 100.0 / colors.Count;
                for (int i = 0; i < colors.Count; i++)
                {
                    gradientStops.Add($"{colors[i]} {i * step}%, {colors[i]} {(i + 1) * step}%");
                }
                return $"background: linear-gradient(90deg, {string.Join(", ", gradientStops)}); color: white;";
            }
        }

        // Use LocationsDuringDay if available (even if single country - more accurate for Partial Day Rule)
        var locationToUse = presence.LocationsDuringDay?.FirstOrDefault() ?? presence.LocationAtMidnight;
        
        if (string.IsNullOrEmpty(locationToUse))
            return string.Empty;
            
        var isHome = locationToUse == homeCountry;
        
        // Home country gets a clear/light background to fade into the background
        if (isHome)
        {
            return "background-color: #f5f5f5; border: 1px solid #ddd; color: #999;";
        }

        // Other countries get their assigned colors to stand out
        var color = ColorService.GetCountryColor(locationToUse);
        return $"background-color: {color}; border: 1px solid {ColorService.GetCountryColorDark(locationToUse)}; color: white;";
    }

    private string GetDayTooltip(DailyPresenceDto presence)
    {
        if (presence.IsInTransitAtMidnight || presence.LocationAtMidnight == "IN_TRANSIT")
            return $"{presence.Date:MMM dd, yyyy} - In Transit at Midnight";
        
        // Show multiple countries if present (Partial Day Rule)
        if (presence.LocationsDuringDay?.Count > 1)
        {
            var countries = string.Join(" & ", presence.LocationsDuringDay.OrderBy(c => c));
            return $"{presence.Date:MMM dd, yyyy} - {countries} (both count under Partial Day Rule)";
        }
        
        // Use LocationsDuringDay if available, otherwise fall back to LocationAtMidnight
        var location = presence.LocationsDuringDay?.FirstOrDefault() ?? presence.LocationAtMidnight;
        return $"{presence.Date:MMM dd, yyyy} - {location}";
    }

    private string GetCountryCode(string countryName)
    {
        // Return 2-3 letter country codes for display
        var codes = new Dictionary<string, string>
        {
            {"Canada", "CA"},
            {"United States", "US"},
            {"USA", "US"},
            {"United Kingdom", "UK"},
            {"Australia", "AU"},
            {"New Zealand", "NZ"},
            {"France", "FR"},
            {"Germany", "DE"},
            {"Japan", "JP"},
            {"China", "CN"},
            {"India", "IN"},
            {"Brazil", "BR"},
            {"Mexico", "MX"},
            {"Spain", "ES"},
            {"Italy", "IT"},
        };

        return codes.TryGetValue(countryName, out var code) ? code : countryName.Substring(0, Math.Min(3, countryName.Length)).ToUpper();
    }

    private void ShowDayDetails(DailyPresenceDto presence)
    {
        selectedDay = presence;
    }
}
