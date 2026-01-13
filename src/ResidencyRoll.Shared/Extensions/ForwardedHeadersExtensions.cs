using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
                }
            }
        }

        // Configure trusted networks from configuration
        var trustedNetworks = configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>();
        if (trustedNetworks != null && trustedNetworks.Length > 0)
        {
            foreach (var network in trustedNetworks)
            {
                // Parse CIDR notation (e.g., "10.0.0.0/8")
                var parts = network.Split('/');
                if (parts.Length == 2 && 
                    IPAddress.TryParse(parts[0], out var ipAddress) && 
                    int.TryParse(parts[1], out var prefixLength))
                {
                    forwardedHeadersOptions.KnownIPNetworks.Add(new System.Net.IPNetwork(ipAddress, prefixLength));
                }
            }
        }

        // If no proxies or networks are configured, the default behavior is to trust localhost/loopback only
        // This is secure by default for most deployments

        app.UseForwardedHeaders(forwardedHeadersOptions);

        return app;
    }
}
