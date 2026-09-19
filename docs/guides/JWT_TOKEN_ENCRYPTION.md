# JWT Token Encryption (JWE) Support

## Overview

This document explains how the ResidencyRoll API handles encrypted JWT tokens (JWE format) issued by identity providers like Auth0.

## Background

### What are JWE Tokens?

JWE (JSON Web Encryption) tokens are encrypted JWT tokens that provide an additional layer of security by encrypting the token payload. Instead of being a signed JWT (which can be decoded and read by anyone), JWE tokens are encrypted and can only be decrypted by authorized parties.

**Standard JWT (signed):**
- Format: `header.payload.signature`
- Payload is Base64-encoded (readable by anyone)
- Only signed to ensure integrity

**JWE (encrypted):**
- Format: `header.encrypted_key.iv.ciphertext.tag`
- Payload is encrypted (unreadable without decryption key)
- Both encrypted AND signed

### Why Auth0 Issues JWE Tokens

Auth0 can issue encrypted access tokens when:
1. The API is configured with a "Confidential Client" signing algorithm
2. The token is being issued for a specific audience (API identifier)
3. Enhanced security is required for the access token payload

## The Problem

When Auth0 issues JWE tokens, the .NET JWT Bearer authentication middleware needs to:
1. **Decrypt** the token using the appropriate decryption key
2. **Validate** the decrypted token's signature and claims

Without the decryption key configured, the API will fail with:

```text
IDX10609: Decryption failed. No Keys tried: ...
```

This error means the API received an encrypted token but doesn't have the key to decrypt it.

## The Solution

### For Auth0 Users

When using Auth0 with encrypted access tokens, you need to provide the **Client Secret** to the API so it can decrypt incoming JWE tokens.

#### Configuration

**Docker Deployment (`.env` file):**
```bash
# Identity Provider Configuration (for Web App)
OIDC_CLIENT_SECRET=your-auth0-client-secret

# API JWT Configuration
JWT_AUTHORITY=https://your-tenant.auth0.com/
JWT_AUDIENCE=your-api-identifier
JWT_CLIENT_SECRET=${OIDC_CLIENT_SECRET}  # Reuse the same secret
```

**Local Development (User Secrets):**
```bash
cd src/ResidencyRoll.Api
dotnet user-secrets set "Jwt:ClientSecret" "YOUR-CLIENT-SECRET"
```

**appsettings.json:**
```json
{
  "Jwt": {
    "Authority": "https://your-tenant.auth0.com/",
    "Audience": "your-api-identifier",
    "RequireHttpsMetadata": true,
    "ClientSecret": "your-client-secret"
  }
}
```

### How It Works

1. **Web Application** authenticates with Auth0 using the Client ID and Client Secret
2. **Auth0** issues an encrypted access token (JWE) for the API audience
3. **Web Application** forwards the encrypted token to the API in the Authorization header
4. **API** uses the Client Secret to:
   - Decrypt the JWE token
   - Validate the decrypted JWT's signature
   - Extract and validate claims
5. **API** grants access if token is valid

### Code Implementation

The API's `Program.cs` now includes JWE decryption support:

```csharp
// Configure token decryption for JWE (encrypted JWT) tokens
var clientSecret = builder.Configuration["Jwt:ClientSecret"];
if (!string.IsNullOrEmpty(clientSecret))
{
    Log.Information("JWT client secret configured - JWE token decryption enabled");
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        // Use client secret for decrypting JWE tokens
        TokenDecryptionKey = new SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(clientSecret))
    };
}
```

## Environment-Specific Configuration

### Development

In local development, JWE tokens can be decrypted if:
- The client secret is configured in user-secrets OR
- The client secret is in `appsettings.Development.json`

### Docker/Production

In Docker deployment, JWE tokens can be decrypted if:
- The `OIDC_CLIENT_SECRET` environment variable is set (for the Web app)
- The `Jwt__ClientSecret` environment variable is set to the same value (for the API)

**Note:** The docker-compose.yml now automatically passes the client secret to both services:
- Web app: `OIDC_CLIENT_SECRET`
- API: `Jwt__ClientSecret=${OIDC_CLIENT_SECRET}`

## Troubleshooting

### Error: "IDX10609: Decryption failed. No Keys tried"

**Symptoms:**
- API returns 401 Unauthorized
- Logs show "Decryption failed" error
- Token has `"alg":"dir","enc":"A256GCM"` in header (indicating JWE)

**Solutions:**
1. **Verify Client Secret is configured:**
   ```bash
   # In Docker
   docker logs ResidencyRoll-Api | grep "JWT client secret"
   # Should show: "JWT client secret configured - JWE token decryption enabled"
   ```

2. **Check environment variables:**
   ```bash
   # Verify the secret is set (don't print the actual value!)
   docker exec ResidencyRoll-Api env | grep Jwt__ClientSecret
   ```

3. **Ensure the same secret is used:**
   - The Web app's `OIDC_CLIENT_SECRET` must match
   - The API's `Jwt__ClientSecret` must match
   - Both must match the Auth0 application's client secret

### Working in Local Development but Not Docker?

This typically means:
- ✅ Local development has the client secret configured (user-secrets or appsettings)
- ❌ Docker deployment is missing the `Jwt__ClientSecret` environment variable

**Fix:** Ensure `.env` file includes:
```bash
OIDC_CLIENT_SECRET=your-actual-client-secret
```

### Do I Need This for All Identity Providers?

**No.** JWE token decryption is only needed if:
- Your identity provider issues encrypted access tokens
- The token header contains `"enc"` field (e.g., `"enc":"A256GCM"`)

**Auth0:** May issue JWE tokens depending on API configuration
**Azure AD/Entra ID:** Typically issues standard signed JWTs (no encryption)
**Keycloak:** Can be configured to issue either JWE or standard JWT

## Security Considerations

### Client Secret Protection

The client secret is sensitive and should be:
- ✅ Stored in environment variables or secret management systems
- ✅ Never committed to source control
- ✅ Rotated regularly (update in both Auth0 and deployment config)
- ✅ Different between development and production environments

### When to Use JWE Tokens

**Use JWE tokens when:**
- Access tokens contain sensitive user data
- Compliance requires encrypted tokens in transit
- Additional security layer is desired

**Standard JWT is sufficient when:**
- Token only contains non-sensitive claims (user ID, roles)
- HTTPS already encrypts the token in transit
- Performance is critical (JWE adds processing overhead)

## Auth0 Configuration

### To Check if Your API Issues JWE Tokens

1. Go to **Auth0 Dashboard → Applications → APIs**
2. Select your API
3. Go to **Settings** tab
4. Check **Token Encryption** section
5. If encryption is enabled, you'll need the client secret in your API configuration

### To Disable JWE (Use Standard JWT Instead)

If you prefer standard signed JWTs instead of encrypted tokens:

1. In Auth0 Dashboard → Applications → APIs → Your API → Settings
2. Find **Token Encryption** or **Token Settings**
3. Disable token encryption
4. Standard signed JWTs will be issued instead

**Note:** After changing this setting:
- No client secret is needed in the API
- Remove `Jwt__ClientSecret` from configuration
- Tokens will be standard JWT format (header.payload.signature)

## References

- [RFC 7516: JSON Web Encryption (JWE)](https://tools.ietf.org/html/rfc7516)
- [Auth0: Access Token Encryption](https://auth0.com/docs/secure/tokens/access-tokens)
- [Microsoft: JWT Token Decryption](https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.tokens.tokenvalidationparameters.tokendecryptionkey)
