# Forwarded Headers Configuration for HTTPS Behind Reverse Proxy

This document explains how to configure the ResidencyRoll application to properly recognize HTTPS when behind an NGINX reverse proxy that terminates SSL.

## Problem

When the application is behind a reverse proxy (e.g., NGINX) that:
1. Terminates SSL/TLS on the proxy
2. Forwards requests to the app container over HTTP
3. Sends X-Forwarded-* headers with the original request information

The application may not recognize that the original request was HTTPS, causing:
- OAuth callback URLs to be generated with `http://` instead of `https://`
- OAuth callback URL mismatches
- Insecure cookie settings

## Solution Overview

The application uses ASP.NET Core's `ForwardedHeaders` middleware to process X-Forwarded-* headers from the reverse proxy. This middleware is configured through the `UseConfiguredForwardedHeaders()` extension method that:

1. **Reads configuration** for trusted proxies and networks from `appsettings.json` or environment variables
2. **Enables proper headers** including:
   - `X-Forwarded-Proto` (original request scheme: https/http)
   - `X-Forwarded-Host` (original request host)
   - `X-Forwarded-For` (original client IP)
3. **Logs diagnostics** to help troubleshoot configuration issues

## Configuration

### Option 1: Configure via appsettings.json

Add or update the `ForwardedHeaders` section in `appsettings.json`:

```json
{
  "ForwardedHeaders": {
    "KnownProxies": [
      "172.20.0.1"
    ],
    "KnownNetworks": [
      "172.20.0.0/16"
    ]
  }
}
```

- **KnownProxies**: Array of trusted proxy IP addresses
- **KnownNetworks**: Array of trusted networks in CIDR notation (e.g., `10.0.0.0/8`)

### Option 2: Configure via Environment Variables

If you're using Docker, you can set environment variables to override or supplement the configuration:

```bash
# Docker environment variables
FORWARDED_HEADERS__KNOWNPROXIES__0=172.20.0.1
FORWARDED_HEADERS__KNOWNNETWORKS__0=172.20.0.0/16
```

Or in a docker-compose.yml:

```yaml
services:
  web:
    environment:
      - FORWARDED_HEADERS__KNOWNPROXIES__0=172.20.0.1
      - FORWARDED_HEADERS__KNOWNNETWORKS__0=172.20.0.0/16
```

## For Your Setup

Based on your configuration where:
- Docker gateway IP: `172.20.0.1`
- Docker network: `172.20.0.0/16`
- NGINX runs in the same Docker network

**Recommended configuration:**

```json
{
  "ForwardedHeaders": {
    "KnownProxies": [
      "172.20.0.1"
    ],
    "KnownNetworks": [
      "172.20.0.0/16"
    ]
  }
}
```

## Middleware Order

The `UseConfiguredForwardedHeaders()` middleware must be called early in the pipeline, before:
- `UseAuthentication()`
- `UseAuthorization()`
- `UseHttpsRedirection()` (when behind a proxy)

The application already has this correctly configured in `Program.cs`:

```csharp
var app = builder.Build();

// Handle forwarded headers from reverse proxy (MUST be early)
app.UseConfiguredForwardedHeaders();

// ... rest of middleware pipeline
app.UseHttpsRedirection();
app.UseAuthentication();
```

## Diagnostic Logging

The middleware includes comprehensive logging to help troubleshoot issues. Monitor logs for:

### Configuration Messages

At startup, you should see:

```text
=== Configuring ForwardedHeaders Middleware ===
Enabled forwarded headers: XForwardedFor, XForwardedProto, XForwardedHost
Processing 1 configured trusted proxies
✓ Added trusted proxy IP: 172.20.0.1
Processing 1 configured trusted networks
✓ Added trusted network: 172.20.0.0/16 at index 0.
Configuration summary: 1 proxies, 1 networks configured
```

### Request-Level Messages

For each request with forwarded headers, you'll see:

```text
[ForwardedHeaders] Received forwarded headers: 
  X-Forwarded-Proto=https, 
  X-Forwarded-Host=residencyroll.kinsac.com, 
  X-Forwarded-For=203.0.113.42, 
  RemoteIP=172.20.0.1, 
  Request.Scheme=https, 
  Request.Host=residencyroll.kinsac.com
```

### Warning Messages

If proxies aren't configured, you'll see:

```text
⚠ No trusted proxies or networks configured. 
The default behavior will trust only localhost (127.0.0.1, ::1). 
This may cause forwarded headers to be ignored if your reverse proxy 
is not on localhost. Configure 'ForwardedHeaders:KnownProxies' or 
'ForwardedHeaders:KnownNetworks' in appsettings.json or via 
environment variables.
```

## Troubleshooting

### Problem: OAuth callbacks still showing http://

**Check:**

1. **Verify configuration is loaded:**
   - Look for "Configuration summary" log message
   - Ensure at least 1 proxy or network is configured

2. **Verify NGINX is sending headers:**
   - Check NGINX configuration includes forwarded headers
   - NGINX should have: `proxy_set_header X-Forwarded-Proto $scheme;`
   - NGINX should have: `proxy_set_header X-Forwarded-Host $host;`

3. **Verify request origin matches trusted proxy:**
   - In logs, check the `RemoteIP` field
   - Compare with configured `KnownProxies` and `KnownNetworks`
   - Example: If RemoteIP is `172.20.5.10` but only `172.20.0.0/16` is trusted, it should still work with `/16` CIDR notation

### Problem: Forwarded headers are being ignored

1. Check log messages for configuration errors (marked with ✗)
2. Verify your CIDR notation is correct (e.g., `172.20.0.0/16` not `172.20.0.0/24` if the IP is outside that range)
3. Ensure the middleware is called early enough in `Program.cs`

### Problem: Still getting "Invalid CIDR notation" warnings

Common issues:
- Missing `/` in CIDR notation: use `172.20.0.0/16` not `172.20.0.0`
- Invalid prefix length for IPv4: must be 0-32 (you used a value > 32)
- Invalid prefix length for IPv6: must be 0-128

## Authentication and Cookies

Once ForwardedHeaders is properly configured, the application will:

1. Recognize HTTPS in `Request.Scheme` and `Request.Host`
2. Generate OAuth callback URLs with `https://` (if OIDC is configured)
3. Set secure cookies automatically in non-development environments
4. Properly validate SSL/TLS for authentication metadata

For the Web application, ensure `RequireHttpsMetadata` is set appropriately:

```json
{
  "Authentication": {
    "OpenIdConnect": {
      "RequireHttpsMetadata": true
    }
  }
}
```

In production, this should be `true`. Set to `false` only for local development.

## Security Considerations

⚠️ **Important Security Notes:**

1. **Only trust known proxies:** Only configure IPs/networks of your reverse proxy. Trusting all sources allows header spoofing.

2. **Docker network isolation:** The `172.20.0.0/16` network is isolated within Docker. This is secure for container-to-container communication.

3. **From untrusted sources:** Never enable forwarded headers trust for requests from the public internet.

4. **Environment variables in production:** Be cautious when using environment variables. Consider:
   - Use a configuration management system
   - Don't hardcode sensitive IPs in docker-compose.yml
   - Use Docker secrets or similar mechanisms in production

## References

- [ASP.NET Core Forwarded Headers Middleware Documentation](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer)
- [NGINX Documentation on Proxied Headers](https://nginx.org/en/docs/http/ngx_http_proxy_module.html)
- [RFC 7239 - Forwarded HTTP Extension](https://tools.ietf.org/html/rfc7239)
