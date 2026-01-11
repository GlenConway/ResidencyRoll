using Microsoft.AspNetCore.Authentication;
using System.Net.Http.Headers;

namespace ResidencyRoll.Web.Services;

/// <summary>
/// Delegating handler that adds the access token to outgoing API requests
/// </summary>
public class ApiAuthenticationHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ApiAuthenticationHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        
        if (httpContext != null)
        {
            // Get the access token from the authenticated user
            var accessToken = await httpContext.GetTokenAsync("access_token");
            
            if (!string.IsNullOrEmpty(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
