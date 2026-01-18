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

    [Inject] private TripsApiClient ApiClient { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        loading = true;
        StateHasChanged();

        try
        {
            // Get last 365 days
            var endDate = DateOnly.FromDateTime(DateTime.Today);
            var startDate = endDate.AddDays(-365);
            
            summaries = await ApiClient.GetResidencySummaryAsync(startDate, endDate);
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
        if (summary.TotalDays >= summary.ThresholdDays)
        {
            return "border-left: 4px solid #f44336;"; // Red for over threshold
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
        if (summary.TotalDays >= summary.ThresholdDays)
            return ProgressBarStyle.Danger;
        if (summary.IsApproachingThreshold)
            return ProgressBarStyle.Warning;
        return ProgressBarStyle.Success;
    }
}
