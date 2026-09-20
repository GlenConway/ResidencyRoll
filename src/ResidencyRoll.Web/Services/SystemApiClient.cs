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
}
