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
        var httpContext = _httpContextAccessor.HttpContext;
        
        if (httpContext != null)
        {
            // Try multiple ways to get the access token
            string? accessToken = null;
            
            // Method 1: Try getting from the default authentication scheme
            accessToken = await httpContext.GetTokenAsync("access_token");
            _logger.LogDebug("[ApiAuth] Method 1 (default scheme): {Result}", accessToken != null ? "Found" : "Not found");
            
            // Method 2: Try getting explicitly from Cookie authentication scheme
            if (string.IsNullOrEmpty(accessToken))
            {
                accessToken = await httpContext.GetTokenAsync(CookieAuthenticationDefaults.AuthenticationScheme, "access_token");
                _logger.LogDebug("[ApiAuth] Method 2 (Cookie scheme): {Result}", accessToken != null ? "Found" : "Not found");
            }
            
            // Method 3: Try getting from the authentication result
            if (string.IsNullOrEmpty(accessToken))
            {
                var authenticateResult = await httpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                _logger.LogDebug("[ApiAuth] Method 3 - AuthenticateResult.Succeeded: {Succeeded}", authenticateResult.Succeeded);
                
                if (authenticateResult.Succeeded)
                {
                    _logger.LogDebug("[ApiAuth] Method 3 - User: {User}, IsAuthenticated: {IsAuthenticated}",
                        authenticateResult.Principal?.Identity?.Name,
                        authenticateResult.Principal?.Identity?.IsAuthenticated);
                    
                    if (authenticateResult.Properties?.Items != null)
                    {
                        _logger.LogDebug("[ApiAuth] Method 3 - Properties.Items count: {Count}", authenticateResult.Properties.Items.Count);
                        
                        // Log all property keys for debugging
                        var allKeys = authenticateResult.Properties.Items.Keys.ToList();
                        _logger.LogDebug("[ApiAuth] Method 3 - All property keys: {Keys}", string.Join(", ", allKeys));
                        
                        authenticateResult.Properties.Items.TryGetValue(".Token.access_token", out accessToken);
                        _logger.LogDebug("[ApiAuth] Method 3 (Properties): {Result}", accessToken != null ? "Found" : "Not found");
                    }
                    else
                    {
                        _logger.LogWarning("[ApiAuth] Method 3 - Properties or Items is null");
                    }
                }
                else
                {
                    _logger.LogWarning("[ApiAuth] Method 3 - Authentication failed: {Failure}", authenticateResult.Failure?.Message);
                }
            }
            
            // Method 4: Try getting from AccessTokenProvider (for Blazor Server circuits)
            if (string.IsNullOrEmpty(accessToken))
            {
                accessToken = await _tokenProvider.GetAccessTokenAsync();
                _logger.LogDebug("[ApiAuth] Method 4 (AccessTokenProvider): {Result}", accessToken != null ? "Found" : "Not found");
            }
            
            if (!string.IsNullOrEmpty(accessToken))
            {
                _logger.LogInformation("[ApiAuth] Adding Bearer token to request: {Url}", request.RequestUri);
                _logger.LogDebug("[ApiAuth] Token (first 20 chars): {TokenPrefix}...", accessToken.Substring(0, Math.Min(20, accessToken.Length)));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
            else
            {
                _logger.LogWarning("[ApiAuth] No access token found for request: {Url}", request.RequestUri);
            }
        }
        else
        {
            _logger.LogWarning("[ApiAuth] HttpContext is null for request: {Url}", request.RequestUri);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
