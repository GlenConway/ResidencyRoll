using Microsoft.AspNetCore.Components.Authorization;

namespace ResidencyRoll.Web.Services;

/// <summary>
/// Provides access tokens for API calls from Blazor Server circuits
/// </summary>
public class AccessTokenProvider
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public AccessTokenProvider(AuthenticationStateProvider authenticationStateProvider)
    {
        _authenticationStateProvider = authenticationStateProvider;
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        // In Blazor Server, we need to get the token from the user's claims
        // The token should have been stored as a claim during OIDC authentication
        var accessTokenClaim = user.FindFirst("access_token");
        return accessTokenClaim?.Value;
    }
}
