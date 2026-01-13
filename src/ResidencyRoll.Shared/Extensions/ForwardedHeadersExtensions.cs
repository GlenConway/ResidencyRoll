using Microsoft.AspNetCore.Builder;
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
    /// Enables processing of X-Forwarded-For and X-Forwarded-Proto headers.
    /// Supports configuration of trusted proxies and networks via appsettings or environment variables.
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static IApplicationBuilder UseConfiguredForwardedHeaders(this IApplicationBuilder app)
    {
        var configuration = app.ApplicationServices.GetRequiredService<IConfiguration>();
        var logger = app.ApplicationServices.GetRequiredService<ILogger<ForwardedHeadersOptions>>();
        
        var forwardedHeadersOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        };

        // Configure trusted proxies from configuration
        var trustedProxies = configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>();
        if (trustedProxies != null && trustedProxies.Length > 0)
        {
            foreach (var proxy in trustedProxies)
            {
                if (IPAddress.TryParse(proxy, out var ipAddress))
                {
                    forwardedHeadersOptions.KnownProxies.Add(ipAddress);
                    logger.LogInformation("Added trusted proxy: {ProxyIP}", ipAddress);
                }
                else
                {
                    logger.LogWarning("Invalid proxy IP address in configuration for ForwardedHeaders:KnownProxies.");
                }
            }
        }

        // Configure trusted networks from configuration
        var trustedNetworks = configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>();
        if (trustedNetworks != null && trustedNetworks.Length > 0)
        {
            for (var index = 0; index < trustedNetworks.Length; index++)
            {
                var network = trustedNetworks[index];
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
                        logger.LogInformation("Added trusted network at index {Index}.", index);
                    }
                    else
                    {
                        logger.LogWarning("Invalid network CIDR prefix length in configuration section 'ForwardedHeaders:KnownNetworks' at index {Index}.", index);
                    }
                }
                else
                {
                    logger.LogWarning("Invalid network CIDR notation in configuration section 'ForwardedHeaders:KnownNetworks' at index {Index}.", index);
                }
            }
        }

        // If no proxies or networks are configured, the default behavior is to trust localhost/loopback only
        // This is secure by default for most deployments

        app.UseForwardedHeaders(forwardedHeadersOptions);

        return app;
    }
}
