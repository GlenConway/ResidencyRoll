# Authentication Setup Guide

## Overview

ResidencyRoll now supports JWT/OAuth/OpenID Connect authentication. The API requires valid JWT tokens, and the Blazor frontend can authenticate users via any OIDC-compliant identity provider.

## Architecture

- **API (ResidencyRoll.Api)**: Validates JWT Bearer tokens from any OIDC provider
- **Web (ResidencyRoll.Web)**: Uses OpenID Connect for user authentication and forwards tokens to API
- **Token Flow**: Web app authenticates user → stores tokens in encrypted cookies → adds Bearer token to API requests

## Quick Start (Development without Authentication)

For local development without setting up an identity provider:

1. Both projects have authentication **disabled by default** in development
2. The API controller's `[Authorize]` attribute is enabled but the Web app runs without OIDC
3. To test without auth, temporarily remove `[Authorize]` from TripsController

## Production Setup with Identity Provider

### Supported Identity Providers

- Azure AD / Microsoft Entra ID
- Auth0
- Keycloak
- Okta
- Duende IdentityServer
- Any OIDC-compliant provider

### Configuration Steps

#### 1. Register Applications with Your Identity Provider

**API Application (Backend):**
- Application Name: `ResidencyRoll API`
- Application Type: API / Resource Server
- Audience: `residencyroll-api` (or your custom value)
- Note the Authority URL (issuer URL)

**Web Application (Frontend):**
- Application Name: `ResidencyRoll Web`
- Application Type: Web Application / Confidential Client
- Redirect URIs: 
  - `https://your-domain.com/signin-oidc`
  - `http://localhost:5001/signin-oidc` (for local testing)
- Post-logout redirect URIs:
  - `https://your-domain.com/`
  - `http://localhost:5001/`
- Grant Types: Authorization Code + PKCE
- Scopes: `openid`, `profile`, `email`, `residencyroll-api`
- Note the ClientId and ClientSecret

#### 2. Configure API (ResidencyRoll.Api)

Update `appsettings.json` or use environment variables:

```json
{
  "Jwt": {
    "Authority": "https://your-identity-provider.com",
    "Audience": "residencyroll-api",
    "RequireHttpsMetadata": true
  },
  "Cors": {
    "AllowedOrigins": [
      "https://your-web-domain.com",
      "http://localhost:8081"
    ]
  }
}
```

Environment variables (for Docker):
```bash
JWT_AUTHORITY=https://your-identity-provider.com
JWT_AUDIENCE=residencyroll-api
JWT_REQUIRE_HTTPS=true
```

#### 3. Configure Web (ResidencyRoll.Web)

Update `appsettings.json` or use environment variables:

```json
{
  "Api": {
    "BaseUrl": "https://api.your-domain.com"
  },
  "Authentication": {
    "OpenIdConnect": {
      "Enabled": true,
      "Authority": "https://your-identity-provider.com",
      "ClientId": "residencyroll-web",
      "ClientSecret": "your-client-secret",
      "RequireHttpsMetadata": true,
      "ApiScope": "residencyroll-api"
    }
  }
}
```

Environment variables (for Docker):
```bash
OIDC_ENABLED=true
OIDC_AUTHORITY=https://your-identity-provider.com
OIDC_CLIENT_ID=residencyroll-web
OIDC_CLIENT_SECRET=your-secret-here
OIDC_REQUIRE_HTTPS=true
OIDC_API_SCOPE=residencyroll-api
```

#### 4. Docker Compose Setup

Create a `.env` file in the project root (see `.env.example`):

```bash
# Identity Provider Configuration
OIDC_ENABLED=true
OIDC_AUTHORITY=https://your-identity-provider.com
OIDC_CLIENT_ID=residencyroll-web
OIDC_CLIENT_SECRET=your-secret-here
OIDC_REQUIRE_HTTPS=true
OIDC_API_SCOPE=residencyroll-api

# API JWT Configuration
JWT_AUTHORITY=https://your-identity-provider.com
JWT_AUDIENCE=residencyroll-api
JWT_REQUIRE_HTTPS=true
```

Then run:
```bash
docker-compose up -d
```

## Provider-Specific Examples

### Azure AD / Microsoft Entra ID

```json
{
  "Jwt": {
    "Authority": "https://login.microsoftonline.com/{tenant-id}/v2.0",
    "Audience": "api://residencyroll-api"
  }
}
```

Web configuration:
```json
{
  "Authentication": {
    "OpenIdConnect": {
      "Enabled": true,
      "Authority": "https://login.microsoftonline.com/{tenant-id}/v2.0",
      "ClientId": "{web-app-client-id}",
      "ClientSecret": "{web-app-client-secret}",
      "ApiScope": "api://residencyroll-api/.default"
    }
  }
}
```

### Auth0

API configuration:
```json
{
  "Jwt": {
    "Authority": "https://{your-tenant}.auth0.com/",
    "Audience": "https://api.residencyroll.com"
  }
}
```

Web configuration:
```json
{
  "Authentication": {
    "OpenIdConnect": {
      "Enabled": true,
      "Authority": "https://{your-tenant}.auth0.com/",
      "ClientId": "{your-client-id}",
      "ClientSecret": "{your-client-secret}",
      "ApiScope": "read:trips write:trips"
    }
  }
}
```

### Keycloak

API configuration:
```json
{
  "Jwt": {
    "Authority": "https://keycloak.example.com/realms/{realm-name}",
    "Audience": "residencyroll-api"
  }
}
```

Web configuration:
```json
{
  "Authentication": {
    "OpenIdConnect": {
      "Enabled": true,
      "Authority": "https://keycloak.example.com/realms/{realm-name}",
      "ClientId": "residencyroll-web",
      "ClientSecret": "{your-client-secret}",
      "ApiScope": "residencyroll-api"
    }
  }
}
```

## Testing Authentication

### 1. Test API Token Validation

```bash
# Get a token from your identity provider
TOKEN="your-jwt-token-here"

# Test API endpoint
curl -X GET "http://localhost:8080/api/v1/trips" \
  -H "Authorization: Bearer $TOKEN"
```

### 2. Test Web Application Flow

1. Navigate to `http://localhost:8081`
2. Click "Login" button
3. Authenticate with your identity provider
4. You should be redirected back and see your name in the top-right
5. The app will automatically include your token in API requests

### 3. Test Logout

1. Click "Logout" button
2. You should be signed out from both the app and identity provider

## Security Considerations

1. **HTTPS in Production**: Always use HTTPS in production (`RequireHttpsMetadata: true`)
2. **Client Secret**: Store secrets securely (Azure Key Vault, AWS Secrets Manager, etc.)
3. **Token Expiration**: Configure appropriate token lifetimes in your identity provider
4. **Refresh Tokens**: The current implementation uses sliding sessions; consider refresh tokens for longer sessions
5. **CORS**: Only allow specific origins in production
6. **Cookie Security**: Cookies are marked Secure and SameSite=Lax

## Troubleshooting

### "Unauthorized" errors from API

- Verify JWT Authority and Audience match between API and identity provider
- Check token is being sent in `Authorization: Bearer {token}` header
- Validate token at jwt.io or jwt.ms

### Cannot login / redirect loop

- Verify redirect URIs are registered in identity provider
- Check Authority URL is correct and accessible
- Ensure ClientId and ClientSecret are correct
- Check `RequireHttpsMetadata` setting matches your environment

### Token not being forwarded to API

- Verify `ApiAuthenticationHandler` is registered
- Check `SaveTokens: true` in OIDC options
- Inspect network requests to see if Authorization header is present

## Future Mobile Client Support

The API is ready for mobile clients:

1. Mobile app authenticates directly with identity provider
2. Receives access token
3. Includes token in API requests: `Authorization: Bearer {token}`
4. No changes needed to API

Example mobile flow (React Native, Flutter, Swift, etc.):
```javascript
// Mobile app code (pseudocode)
const token = await authProvider.login();
const response = await fetch('https://api.your-domain.com/api/v1/trips', {
  headers: {
    'Authorization': `Bearer ${token}`,
    'Accept': 'application/json'
  }
});
```

## Additional Resources

- [Microsoft Identity Platform](https://learn.microsoft.com/en-us/entra/identity-platform/)
- [Auth0 Documentation](https://auth0.com/docs)
- [Keycloak Documentation](https://www.keycloak.org/documentation)
- [OpenID Connect Specification](https://openid.net/connect/)
