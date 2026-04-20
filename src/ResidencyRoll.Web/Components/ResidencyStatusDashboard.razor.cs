using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using ResidencyRoll.Shared.Trips;
using ResidencyRoll.Web.Services;

namespace ResidencyRoll.Web.Components;

public partial class ResidencyStatusDashboard
{
    private List<ResidencySummaryDto> summaries = new();
    private bool loading = false;
    private string? homeCountry;
    private DateTime startDate;
    private DateTime endDate;

    [Inject] private TripsApiClient ApiClient { get; set; } = default!;
    [Inject] private LocalStorageService LocalStorage { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        endDate = DateTime.Today;
        startDate = endDate.AddDays(-365);
        homeCountry = await LocalStorage.GetItemAsync("residencyroll_home_country");
        await LoadData();
    }

    private async Task LoadData()
    {
        loading = true;
        StateHasChanged();

        try
        {
            summaries = await ApiClient.GetResidencySummaryAsync(
                DateOnly.FromDateTime(startDate),
                DateOnly.FromDateTime(endDate));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading residency summary: {ex.Message}");
            summaries = new();
        }
        finally
        {
            loading = false;
            StateHasChanged();
        }
    }

    private string GetCardStyle(ResidencySummaryDto summary)
    {
        if (summary.TotalDays >= summary.ThresholdDays && !IsHomeCountry(summary.CountryName))
        {
            return "border-left: 4px solid #f44336;"; // Red for over threshold
        }
        else if (IsHomeCountry(summary.CountryName))
        {
            return "border-left: 4px solid #4caf50;"; // Home country over-threshold is expected
        }
        else if (summary.IsApproachingThreshold)
        {
            return "border-left: 4px solid #ff9800;"; // Orange for approaching
        }
        else
        {
            return "border-left: 4px solid #4caf50;"; // Green for safe
        }
    }

    private BadgeStyle GetBadgeStyle(string ruleType)
    {
        return ruleType == "Midnight" ? BadgeStyle.Info : BadgeStyle.Secondary;
    }

    private double GetProgressPercentage(ResidencySummaryDto summary)
    {
        return Math.Min(100, (double)summary.TotalDays / summary.ThresholdDays * 100);
    }

    private ProgressBarStyle GetProgressBarStyle(ResidencySummaryDto summary)
    {
        if (summary.TotalDays >= summary.ThresholdDays && !IsHomeCountry(summary.CountryName))
            return ProgressBarStyle.Danger;
        if (summary.IsApproachingThreshold)
            return ProgressBarStyle.Warning;
        return ProgressBarStyle.Success;
    }

    private bool IsHomeCountry(string countryName)
    {
        return !string.IsNullOrWhiteSpace(homeCountry)
            && string.Equals(countryName, homeCountry, StringComparison.OrdinalIgnoreCase);
    }
}
