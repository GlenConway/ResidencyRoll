# Authentication Quick Reference

## For Developers

### Running Locally WITHOUT Authentication

Authentication is **disabled by default** in development for easier testing.

1. Start both services:
   ```bash
   # Terminal 1 - API
   cd src/ResidencyRoll.Api
   dotnet watch run
   
   # Terminal 2 - Web
   cd src/ResidencyRoll.Web
   dotnet watch run
   ```

2. The API has `[Authorize]` enabled, but you can test it by:
   - **Option A**: Use the Web app (which will make authenticated requests)
   - **Option B**: Temporarily remove `[Authorize]` from TripsController for API testing

### Running Locally WITH Authentication

1. Set up a test identity provider (recommended: Auth0 free tier or Azure AD)

2. Update `appsettings.Development.json` in both projects:
   
   **API:**
   ```json
   {
     "Jwt": {
       "Authority": "https://your-dev-tenant.auth0.com/",
       "Audience": "residencyroll-api-dev",
       "RequireHttpsMetadata": false
     }
   }
   ```
   
   **Web:**
   ```json
   {
     "Authentication": {
       "OpenIdConnect": {
         "Enabled": true,
         "Authority": "https://your-dev-tenant.auth0.com/",
         "ClientId": "your-dev-client-id",
         "ClientSecret": "your-dev-secret",
         "RequireHttpsMetadata": false,
         "ApiScope": "residencyroll-api-dev"
       }
     }
   }
   ```

3. Start both services and test login flow

## For DevOps/Deployment

### Production Deployment Checklist

- [ ] Register applications with identity provider
- [ ] Set `RequireHttpsMetadata: true`
- [ ] Use HTTPS for all endpoints
- [ ] Store secrets in secure vault (Azure Key Vault, AWS Secrets, etc.)
- [ ] Configure proper CORS origins (no wildcards)
- [ ] Set appropriate token expiration times
- [ ] Enable security headers (HSTS, CSP, etc.)
- [ ] Monitor authentication failures
- [ ] Set up alerts for unusual authentication patterns

### Docker Deployment

1. Create `.env` file from `.env.example`
2. Fill in your identity provider details
3. Deploy:
   ```bash
   docker-compose up -d
   ```

### Kubernetes Deployment

Use secrets for sensitive values:

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: residencyroll-auth
type: Opaque
stringData:
  oidc-client-secret: "your-secret-here"
  jwt-authority: "https://your-provider.com"
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: residencyroll-web
spec:
  template:
    spec:
      containers:
      - name: web
        env:
        - name: Authentication__OpenIdConnect__Enabled
          value: "true"
        - name: Authentication__OpenIdConnect__ClientSecret
          valueFrom:
            secretKeyRef:
              name: residencyroll-auth
              key: oidc-client-secret
```

## Testing Authentication

### Manual API Testing

```bash
# 1. Get token from identity provider (example with Auth0)
curl -X POST https://your-tenant.auth0.com/oauth/token \
  -H "Content-Type: application/json" \
  -d '{
    "client_id": "your-client-id",
    "client_secret": "your-client-secret",
    "audience": "residencyroll-api",
    "grant_type": "client_credentials"
  }'

# 2. Use token to call API
TOKEN="eyJhbGc..."
curl -X GET http://localhost:8080/api/v1/trips \
  -H "Authorization: Bearer $TOKEN"
```

### Automated Testing

Consider using tools like:
- **Postman** with environment variables for tokens
- **K6** or **Artillery** for load testing with auth
- **Playwright** or **Selenium** for E2E testing with login flows

## Common Issues

| Issue | Solution |
|-------|----------|
| 401 Unauthorized from API | Check JWT Authority and Audience match |
| Login redirect loop | Verify redirect URIs in identity provider |
| Token not forwarded | Check ApiAuthenticationHandler is registered |
| CORS errors | Add Web URL to API CORS AllowedOrigins |
| Certificate errors (dev) | Set RequireHttpsMetadata: false in dev |

## Identity Provider Setup Examples

### Auth0 (Easiest for Testing)

1. Create free account at auth0.com
2. Create API: Applications → APIs → Create
   - Name: ResidencyRoll API
   - Identifier: `residencyroll-api`
3. Create Application: Applications → Create
   - Type: Regular Web Application
   - Add callback: `http://localhost:5001/signin-oidc`
   - Add logout URL: `http://localhost:5001/`
4. Copy values to appsettings

### Azure AD

1. Register API app in Azure Portal
2. Register Web app
3. Configure API permissions
4. Copy tenant ID, client IDs to appsettings

### Keycloak (Self-hosted)

1. Run Keycloak: `docker run -p 8080:8080 quay.io/keycloak/keycloak`
2. Create realm
3. Create client for API
4. Create client for Web
5. Configure redirect URIs

## Security Best Practices

1. **Rotate secrets regularly** (client secrets, signing keys)
2. **Use short token expiration** (1 hour access tokens, refresh as needed)
3. **Implement rate limiting** on auth endpoints
4. **Log authentication events** for security monitoring
5. **Use strong client secrets** (generated, not chosen)
6. **Enable MFA** for users when possible
7. **Validate tokens thoroughly** (issuer, audience, expiration, signature)
8. **Use PKCE** for mobile/SPA clients
9. **Implement token revocation** checking if supported
10. **Monitor for anomalies** (impossible travel, brute force attempts)
