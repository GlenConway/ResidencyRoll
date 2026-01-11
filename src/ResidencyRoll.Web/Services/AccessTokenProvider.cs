namespace ResidencyRoll.Web.Services;

/// <summary>
/// Stores access token in a scoped cache that can be accessed across the request/circuit
/// </summary>
public class AccessTokenProvider
{
    private string? _cachedToken;
    private readonly ILogger<AccessTokenProvider> _logger;

    public AccessTokenProvider(ILogger<AccessTokenProvider> logger)
    {
        _logger = logger;
    }

    public void SetAccessToken(string? token)
    {
        _cachedToken = token;
        _logger.LogDebug("[AccessTokenProvider] Token cached: {HasToken}", !string.IsNullOrEmpty(token));
    }

    public string? GetAccessToken()
    {
        _logger.LogDebug("[AccessTokenProvider] Retrieving cached token: {HasToken}", !string.IsNullOrEmpty(_cachedToken));
        return _cachedToken;
    }
}
