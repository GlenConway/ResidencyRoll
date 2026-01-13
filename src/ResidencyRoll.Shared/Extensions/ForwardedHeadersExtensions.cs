using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace ResidencyRoll.Shared.Extensions;

public static class ForwardedHeadersExtensions
{
    /// <summary>
    /// Configures forwarded headers from a reverse proxy.
    /// Enables processing of X-Forwarded-For and X-Forwarded-Proto headers.
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static IApplicationBuilder UseConfiguredForwardedHeaders(this IApplicationBuilder app)
    {
        var forwardedHeadersOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        };

        // NOTE: Rely on default KnownProxies/KnownNetworks or configure specific trusted
        // proxies or networks (via KnownProxies/KnownNetworks) if required by deployment.

        app.UseForwardedHeaders(forwardedHeadersOptions);

        return app;
    }
}
