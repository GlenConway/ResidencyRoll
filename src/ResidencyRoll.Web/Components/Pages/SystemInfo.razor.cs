using Microsoft.AspNetCore.Components;
using ResidencyRoll.Shared.Trips;
using ResidencyRoll.Web.Services;

namespace ResidencyRoll.Web.Components.Pages;

public partial class SystemInfo
{
    [Inject] private SystemApiClient ApiClient { get; set; } = default!;
    [Inject] private ILogger<SystemInfo> Logger { get; set; } = default!;

    private SystemInfoDto? info;
    private bool loading = true;
    private string? errorMessage;
    private bool backingUp;
    private bool backupFailed;
    private string? backupMessage;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            info = await ApiClient.GetSystemInfoAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load system info");
            errorMessage = "Unable to load system information.";
        }
        finally
        {
            loading = false;
        }
    }

    private async Task BackupNowAsync()
    {
        backingUp = true;
        backupMessage = null;
        try
        {
            var backup = await ApiClient.BackupNowAsync();
            backupFailed = false;
            backupMessage = $"Backup created: {backup?.FileName}";
            info = await ApiClient.GetSystemInfoAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Manual backup failed");
            backupFailed = true;
            backupMessage = "Backup failed. Check the API logs.";
        }
        finally
        {
            backingUp = false;
        }
    }

    private static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes} B" : $"{size:0.##} {units[unit]} ({bytes:N0} bytes)";
    }
}
