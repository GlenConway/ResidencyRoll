using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;

namespace ResidencyRoll.Shared.Extensions;

public static class ForwardedHeadersExtensions
{
    /// <summary>
    /// Configures forwarded headers from a reverse proxy.
    /// Enables processing of X-Forwarded-For, X-Forwarded-Proto, and X-Forwarded-Host headers.
    /// Supports configuration of trusted proxies and networks via appsettings or environment variables.
    /// Includes comprehensive diagnostic logging to debug forwarded header processing.
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static IApplicationBuilder UseConfiguredForwardedHeaders(this IApplicationBuilder app)
    {
        var configuration = app.ApplicationServices.GetRequiredService<IConfiguration>();
        var logger = app.ApplicationServices.GetRequiredService<ILogger<ForwardedHeadersOptions>>();
        
        logger.LogInformation("=== Configuring ForwardedHeaders Middleware ===");
        
        var forwardedHeadersOptions = new ForwardedHeadersOptions
        {
            // Include all necessary forwarded headers for HTTPS behind reverse proxy
            ForwardedHeaders = ForwardedHeaders.XForwardedFor 
                             | ForwardedHeaders.XForwardedProto 
                             | ForwardedHeaders.XForwardedHost
        };

        logger.LogInformation("Enabled forwarded headers: XForwardedFor, XForwardedProto, XForwardedHost");

        // Configure trusted proxies from configuration
        var trustedProxies = configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>();
        var proxiesConfigured = false;
        
        if (trustedProxies != null && trustedProxies.Length > 0)
        {
            logger.LogInformation("Processing {ProxyCount} configured trusted proxies", trustedProxies.Length);
            
            foreach (var proxy in trustedProxies)
            {
                // Skip empty or whitespace-only strings
                if (string.IsNullOrWhiteSpace(proxy))
                {
                    logger.LogWarning("Skipping empty proxy configuration entry");
                    continue;
                }

                if (IPAddress.TryParse(proxy, out var ipAddress))
                {
                    forwardedHeadersOptions.KnownProxies.Add(ipAddress);
                    logger.LogInformation("✓ Added trusted proxy IP: {ProxyIP}", ipAddress);
                    proxiesConfigured = true;
                }
                else
                {
                    logger.LogWarning("✗ Invalid proxy IP address in configuration: {InvalidProxy}", proxy);
                }
            }
        }
        else
        {
            logger.LogInformation("No trusted proxies configured in ForwardedHeaders:KnownProxies");
        }

        // Configure trusted networks from configuration
        var trustedNetworks = configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>();
        var networksConfigured = false;
        
        if (trustedNetworks != null && trustedNetworks.Length > 0)
        {
            logger.LogInformation("Processing {NetworkCount} configured trusted networks", trustedNetworks.Length);
            
            for (var index = 0; index < trustedNetworks.Length; index++)
            {
                var network = trustedNetworks[index];

                // Skip empty or whitespace-only strings
                if (string.IsNullOrWhiteSpace(network))
                {
                    logger.LogWarning("Skipping empty network configuration entry at index {Index}", index);
                    continue;
                }

                // Parse CIDR notation (e.g., "10.0.0.0/8")
                var parts = network.Split('/');
                if (parts.Length == 2 &&
                    IPAddress.TryParse(parts[0], out var ipAddress) &&
                    int.TryParse(parts[1], out var prefixLength))
                {
                    var addressFamily = ipAddress.AddressFamily;
                    var isValidPrefix = false;

                    if (addressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        // IPv4: prefix length must be between 0 and 32
                        isValidPrefix = prefixLength >= 0 && prefixLength <= 32;
                    }
                    else if (addressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                    {
                        // IPv6: prefix length must be between 0 and 128
                        isValidPrefix = prefixLength >= 0 && prefixLength <= 128;
                    }

                    if (isValidPrefix)
                    {
                        forwardedHeadersOptions.KnownIPNetworks.Add(new System.Net.IPNetwork(ipAddress, prefixLength));
                        logger.LogInformation("✓ Added trusted network: {Network} at index {Index}", network, index);
                        networksConfigured = true;
                    }
                    else
                    {
                        logger.LogWarning(
                            "✗ Invalid CIDR prefix length {PrefixLength} for '{Network}' at index {Index}. " +
                            "Valid ranges: IPv4 (0-32), IPv6 (0-128)",
                            prefixLength, network, index);
                    }
                }
                else
                {
                    logger.LogWarning(
                        "✗ Invalid CIDR notation '{Network}' at index {Index}. " +
                        "Expected format: 192.168.0.0/16 or 2001:db8::/32",
                        network, index);
                }
            }
        }
        else
        {
            logger.LogInformation("No trusted networks configured in ForwardedHeaders:KnownNetworks");
        }

        // Log summary of trusted configuration
        if (!proxiesConfigured && !networksConfigured)
        {
            logger.LogWarning(
                "⚠ No trusted proxies or networks configured. The default behavior will trust only localhost (127.0.0.1, ::1). " +
                "This may cause forwarded headers to be ignored if your reverse proxy is not on localhost. " +
                "Configure 'ForwardedHeaders:KnownProxies' or 'ForwardedHeaders:KnownNetworks' in appsettings.json or via environment variables.");
        }

        logger.LogInformation("Configuration summary: {ProxiesCount} proxies, {NetworksCount} networks configured", 
            forwardedHeadersOptions.KnownProxies.Count, 
            forwardedHeadersOptions.KnownIPNetworks.Count);

        // Add a middleware to log incoming forwarded headers for diagnostic purposes
        app.Use(async (context, next) =>
        {
            LogReceivedHeaders(context, logger);
            await next();
        });

        // Register the forwarded headers middleware
        app.UseForwardedHeaders(forwardedHeadersOptions);

        return app;
    }

    /// <summary>
    /// Logs all forwarded headers received in the request for diagnostic purposes.
    /// </summary>
    private static void LogReceivedHeaders(HttpContext context, ILogger logger)
    {
        var xForwardedProto = context.Request.Headers["X-Forwarded-Proto"].ToString();
        var xForwardedHost = context.Request.Headers["X-Forwarded-Host"].ToString();
        var xForwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var requestScheme = context.Request.Scheme;
        var requestHost = context.Request.Host.ToString();

        if (!string.IsNullOrEmpty(xForwardedProto) || !string.IsNullOrEmpty(xForwardedHost) || !string.IsNullOrEmpty(xForwardedFor))
        {
            logger.LogInformation(
                "[ForwardedHeaders] Received forwarded headers: " +
                "X-Forwarded-Proto={ProtoHeader}, X-Forwarded-Host={HostHeader}, X-Forwarded-For={ForHeader}, " +
                "RemoteIP={RemoteIP}, Request.Scheme={Scheme}, Request.Host={Host}",
                string.IsNullOrEmpty(xForwardedProto) ? "(not set)" : xForwardedProto,
                string.IsNullOrEmpty(xForwardedHost) ? "(not set)" : xForwardedHost,
                string.IsNullOrEmpty(xForwardedFor) ? "(not set)" : xForwardedFor,
                remoteIp,
                requestScheme,
                requestHost);
        }
    }
}
