using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Net.Http.Headers;

namespace ResidencyRoll.Web.Services;

/// <summary>
/// Delegating handler that adds the access token to outgoing API requests
/// </summary>
public class ApiAuthenticationHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ApiAuthenticationHandler> _logger;
    private readonly AccessTokenProvider _tokenProvider;

    public ApiAuthenticationHandler(
        IHttpContextAccessor httpContextAccessor,
        ILogger<ApiAuthenticationHandler> logger,
        AccessTokenProvider tokenProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _tokenProvider = tokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Try to get token from the scoped cache first
        var accessToken = _tokenProvider.GetAccessToken();
        _logger.LogDebug("[ApiAuth] Cache check - Token present: {HasToken}", !string.IsNullOrEmpty(accessToken));
        
        // If not cached, try to get from HttpContext
        if (string.IsNullOrEmpty(accessToken))
        {
            var httpContext = _httpContextAccessor.HttpContext;
            _logger.LogDebug("[ApiAuth] HttpContext available: {HasContext}", httpContext != null);
            
            if (httpContext != null)
            {
                // Try getting token from authentication properties (OIDC)
                accessToken = await httpContext.GetTokenAsync("access_token");
                _logger.LogDebug("[ApiAuth] Retrieved token from GetTokenAsync: {HasToken}", !string.IsNullOrEmpty(accessToken));
                
                // If still not found, try to get from claims (fallback)
                if (string.IsNullOrEmpty(accessToken))
                {
                    var tokenClaim = httpContext.User?.FindFirst("access_token");
                    if (tokenClaim != null)
                    {
                        accessToken = tokenClaim.Value;
                        _logger.LogDebug("[ApiAuth] Retrieved token from claims: {HasToken}", !string.IsNullOrEmpty(accessToken));
                    }
                }
                
                // Log token details for debugging
                if (!string.IsNullOrEmpty(accessToken))
                {
                    var tokenParts = accessToken.Split('.');
                    if (tokenParts.Length >= 1)
                    {
                        _logger.LogInformation("[ApiAuth] Token structure valid (parts: {Parts})", tokenParts.Length);
                    }
                }
                else
                {
                    _logger.LogWarning("[ApiAuth] No token found in HttpContext. User authenticated: {IsAuthenticated}, User: {User}",
                        httpContext.User?.Identity?.IsAuthenticated ?? false,
                        httpContext.User?.Identity?.Name ?? "unknown");
                }
                
                // Cache it for subsequent requests in this circuit
                if (!string.IsNullOrEmpty(accessToken))
                {
                    _tokenProvider.SetAccessToken(accessToken);
                }
            }
            else
            {
                _logger.LogWarning("[ApiAuth] HttpContext is null - cannot retrieve token");
            }
        }
        
        if (!string.IsNullOrEmpty(accessToken))
        {
            _logger.LogDebug("[ApiAuth] Adding Bearer token to request: {Url}", request.RequestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        else
        {
            _logger.LogWarning("[ApiAuth] No access token available for request: {Url}", request.RequestUri);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
