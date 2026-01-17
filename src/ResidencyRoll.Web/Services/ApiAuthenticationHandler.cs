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
        
        // If not cached, try to get from HttpContext (if available)
        if (string.IsNullOrEmpty(accessToken))
        {
            var httpContext = _httpContextAccessor.HttpContext;
            
            if (httpContext != null)
            {
                // Try getting from the authentication token
                accessToken = await httpContext.GetTokenAsync("access_token");
                _logger.LogDebug("[ApiAuth] Retrieved token from HttpContext: {HasToken}", !string.IsNullOrEmpty(accessToken));
                
                // Log token header for debugging
                if (!string.IsNullOrEmpty(accessToken))
                {
                    var tokenParts = accessToken.Split('.');
                    if (tokenParts.Length >= 1)
                    {
                        var header = tokenParts[0];
                        _logger.LogInformation("[ApiAuth] Token header (base64): {Header}", header);
                        try
                        {
                            var headerJson = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(header.PadRight(header.Length + (4 - header.Length % 4) % 4, '=')));
                            _logger.LogInformation("[ApiAuth] Token header (decoded): {HeaderJson}", headerJson);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("[ApiAuth] Could not decode token header: {Error}", ex.Message);
                        }
                    }
                }
                
                // Cache it for subsequent requests in this circuit
                if (!string.IsNullOrEmpty(accessToken))
                {
                    _tokenProvider.SetAccessToken(accessToken);
                }
            }
            else
            {
                _logger.LogDebug("[ApiAuth] HttpContext is null for request: {Url}", request.RequestUri);
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
