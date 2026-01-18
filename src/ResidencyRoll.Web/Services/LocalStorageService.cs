using Microsoft.JSInterop;

namespace ResidencyRoll.Web.Services;

/// <summary>
/// Service for interacting with browser's localStorage
/// </summary>
public class LocalStorageService
{
    private readonly IJSRuntime _jsRuntime;

    public LocalStorageService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Gets a value from localStorage
    /// </summary>
    public async Task<string?> GetItemAsync(string key)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Sets a value in localStorage
    /// </summary>
    public async Task SetItemAsync(string key, string value)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value);
        }
        catch
        {
            // Silently fail if localStorage is not available
        }
    }

    /// <summary>
    /// Removes a value from localStorage
    /// </summary>
    public async Task RemoveItemAsync(string key)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
        }
        catch
        {
            // Silently fail if localStorage is not available
        }
    }
}
