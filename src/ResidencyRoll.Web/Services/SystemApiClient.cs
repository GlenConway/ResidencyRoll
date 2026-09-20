using System.Net.Http.Json;
using ResidencyRoll.Shared.Trips;

namespace ResidencyRoll.Web.Services;

public class SystemApiClient
{
    private readonly HttpClient _httpClient;

    public SystemApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<SystemInfoDto?> GetSystemInfoAsync()
    {
        return await _httpClient.GetFromJsonAsync<SystemInfoDto>("api/v1/system/info");
    }

    public async Task<BackupInfoDto?> BackupNowAsync()
    {
        var response = await _httpClient.PostAsync("api/v1/system/backup", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BackupInfoDto>();
    }

    /// <summary>
    /// Opens the backup as a stream; the caller owns the returned response and must dispose it. Null when not found.
    /// </summary>
    public async Task<HttpResponseMessage?> GetBackupAsync(string fileName)
    {
        var response = await _httpClient.GetAsync(
            $"api/v1/system/backups/{Uri.EscapeDataString(fileName)}", HttpCompletionOption.ResponseHeadersRead);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            response.Dispose();
            return null;
        }
        response.EnsureSuccessStatusCode();
        return response;
    }
}
