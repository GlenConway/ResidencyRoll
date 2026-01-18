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
            
            // Fill in IDL skipped days (westbound crossings)
            FillIdlSkippedDays();

            // Build list of available countries and register them with the color service
            availableCountries = dailyPresence
                .Where(d => !string.IsNullOrEmpty(d.LocationAtMidnight) 
                    && d.LocationAtMidnight != "IDL_SKIP" 
                    && d.LocationAtMidnight != "IN_TRANSIT")
                .Select(d => d.LocationAtMidnight)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

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

    private void FillIdlSkippedDays()
    {
        // Identify date gaps that represent IDL westbound crossings
        var allDates = dailyPresence.OrderBy(d => d.Date).ToList();
        for (int i = 0; i < allDates.Count - 1; i++)
        {
            var current = allDates[i];
            var next = allDates[i + 1];
            var daysBetween = (next.Date.DayNumber - current.Date.DayNumber);
            
            if (daysBetween > 1)
            {
                // Fill the gap with "IDL skip" markers
                for (int j = 1; j < daysBetween; j++)
                {
                    var skippedDate = current.Date.AddDays(j);
                    dailyPresence.Add(new DailyPresenceDto
                    {
                        Date = skippedDate,
                        LocationAtMidnight = "IDL_SKIP",
                        IsInTransitAtMidnight = false
                    });
                }
            }
        }
        
        dailyPresence = dailyPresence.OrderByDescending(d => d.Date).ToList();
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
        if (presence.LocationAtMidnight == "IDL_SKIP")
            return "idl-skip";
        if (presence.IsInTransitAtMidnight)
            return "transit";
        return "location";
    }

    private string GetDayStyle(DailyPresenceDto presence)
    {
        if (presence.LocationAtMidnight == "IDL_SKIP" 
            || presence.LocationAtMidnight == "IN_TRANSIT" 
            || presence.IsInTransitAtMidnight)
            return string.Empty;

        var isHome = presence.LocationAtMidnight == homeCountry;
        
        // Home country gets a clear/light background to fade into the background
        if (isHome)
        {
            return "background-color: #f5f5f5; border: 1px solid #ddd; color: #999;";
        }

        // Other countries get their assigned colors to stand out
        var color = ColorService.GetCountryColor(presence.LocationAtMidnight);
        return $"background-color: {color}; border: 1px solid {ColorService.GetCountryColorDark(presence.LocationAtMidnight)}; color: white;";
    }

    private string GetDayTooltip(DailyPresenceDto presence)
    {
        if (presence.LocationAtMidnight == "IDL_SKIP")
            return $"{presence.Date:MMM dd, yyyy} - Skipped day (IDL westbound crossing)";
        if (presence.IsInTransitAtMidnight || presence.LocationAtMidnight == "IN_TRANSIT")
            return $"{presence.Date:MMM dd, yyyy} - In Transit at Midnight";
        return $"{presence.Date:MMM dd, yyyy} - {presence.LocationAtMidnight}";
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
        if (presence.LocationAtMidnight == "IDL_SKIP")
            return;
            
        selectedDay = presence;
    }
}
