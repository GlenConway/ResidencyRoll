using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using ResidencyRoll.Shared.Mcp;
using Serilog;
using Yarp.ReverseProxy.Forwarder;

namespace ResidencyRoll.Web.Bff;

/// <summary>
/// Marker type so tests can boot the web host without colliding with the API project's Program class.
/// </summary>
public sealed class WebEntryPoint;

public static class BffEndpointExtensions
{
    public const string CsrfHeaderName = "X-CSRF";
    public const string CsrfHeaderValue = "1";

    public static WebApplication MapBff(this WebApplication app)
    {
        var oidcEnabled = app.Configuration.GetValue<bool>("Authentication:OpenIdConnect:Enabled");

        app.Use(async (context, next) =>
        {
            var path = context.Request.Path;
            if (IsMcpPassThrough(path))
            {
                await next();
                return;
            }

            var isApi = path.StartsWithSegments("/api");
            var isBff = path.StartsWithSegments("/bff");
            var isAuthLogout = path.StartsWithSegments("/auth/logout");

            if (RequiresCsrf(context.Request.Method) && (isApi || isAuthLogout) && !await HasCsrfAsync(context.Request))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("X-CSRF header required.");
                return;
            }

            if (oidcEnabled && (isApi || isBff) && context.User.Identity?.IsAuthenticated != true)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await next();
        });

        app.MapGet("/auth/login", async (HttpContext context, string? returnUrl) =>
        {
            var enabled = context.RequestServices.GetRequiredService<IConfiguration>()
                .GetValue<bool>("Authentication:OpenIdConnect:Enabled");

            if (string.IsNullOrEmpty(returnUrl))
            {
                if (!enabled)
                {
                    context.Response.Redirect("/");
                    return;
                }

                await context.ChallengeAsync(
                    OpenIdConnectDefaults.AuthenticationScheme,
                    new AuthenticationProperties { RedirectUri = "/" });
                return;
            }

            if (!IsLocalReturnUrl(returnUrl))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("returnUrl must be a local path.");
                return;
            }

            // Only a relative URL reaches the redirect. An absolute URL is rejected.
            var url = new Uri(returnUrl, UriKind.RelativeOrAbsolute);
            if (!url.IsAbsoluteUri)
            {
                if (!enabled)
                {
                    context.Response.Redirect(url.ToString());
                    return;
                }

                await context.ChallengeAsync(
                    OpenIdConnectDefaults.AuthenticationScheme,
                    new AuthenticationProperties { RedirectUri = url.ToString() });
                return;
            }

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("returnUrl must be a local path.");
        });

        app.MapPost("/auth/logout", SignOutAsync);
        app.MapPost("/logout", SignOutAsync);

        app.MapGet("/bff/user", (HttpContext context, IConfiguration configuration) =>
        {
            var enabled = configuration.GetValue<bool>("Authentication:OpenIdConnect:Enabled");
            if (!enabled)
            {
                return Results.Ok(new { name = "Local User", email = "local@localhost", isAuthenticated = true });
            }

            if (context.User.Identity?.IsAuthenticated != true)
            {
                return Results.Unauthorized();
            }

            var name = context.User.Identity?.Name
                ?? context.User.FindFirst("name")?.Value
                ?? string.Empty;
            var email = context.User.FindFirst("email")?.Value
                ?? context.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value
                ?? string.Empty;

            return Results.Ok(new { name, email, isAuthenticated = true });
        });

        app.MapGet("/bff/mcp-info", (HttpContext context, IConfiguration configuration) =>
        {
            var oauthRequired = configuration.GetValue<bool>("Authentication:OpenIdConnect:Enabled");
            return Results.Ok(new
            {
                enabled = McpConfiguration.IsEnabled(configuration),
                oauthRequired,
                url = $"{context.Request.Scheme}://{context.Request.Host}/mcp"
            });
        });

        app.MapReverseProxy(pipeline =>
        {
            pipeline.Use(async (context, next) =>
            {
                await next();
                var error = context.Features.Get<IForwarderErrorFeature>();
                if (error is not null && error.Error != ForwarderError.None)
                {
                    Log.Warning(error.Exception, "[BFF] API proxy failed with {Error}", error.Error);
                }
            });
            pipeline.UseLoadBalancing();
        });
        return app;
    }

    public static void ReturnUnauthorizedForApiPaths(CookieAuthenticationEvents events)
    {
        events.OnRedirectToLogin = context =>
        {
            if (IsApiOrBff(context.Request.Path) || IsMcpPassThrough(context.Request.Path))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    }

    public static bool IsApiOrBff(PathString path) =>
        path.StartsWithSegments("/api") || path.StartsWithSegments("/bff");

    public static bool IsMcpPassThrough(PathString path) =>
        path.StartsWithSegments("/mcp")
        || path.StartsWithSegments("/.well-known/oauth-protected-resource");

    public static bool IsLocalReturnUrl(string returnUrl)
    {
        if (returnUrl.Length == 0 || returnUrl[0] != '/')
        {
            return false;
        }

        if (returnUrl.StartsWith("//", StringComparison.Ordinal) || returnUrl.StartsWith("/\\", StringComparison.Ordinal))
        {
            return false;
        }

        if (returnUrl.Contains('\\', StringComparison.Ordinal) || returnUrl.Contains("://", StringComparison.Ordinal))
        {
            return false;
        }

        return Uri.TryCreate(returnUrl, UriKind.Relative, out _);
    }

    private static bool RequiresCsrf(string method) =>
        !HttpMethods.IsGet(method)
        && !HttpMethods.IsHead(method)
        && !HttpMethods.IsOptions(method)
        && !HttpMethods.IsTrace(method);

    private static async Task<bool> HasCsrfAsync(HttpRequest request)
    {
        if (string.Equals(request.Headers[CsrfHeaderName], CsrfHeaderValue, StringComparison.Ordinal))
        {
            return true;
        }

        // A browser form cannot set headers. Logout accepts the same token as a form field
        // so the navigation can follow the Auth0 end-session redirect.
        if (request.Path.StartsWithSegments("/auth/logout") && request.HasFormContentType)
        {
            var form = await request.ReadFormAsync();
            return string.Equals(form[CsrfHeaderName], CsrfHeaderValue, StringComparison.Ordinal);
        }

        return false;
    }

    private static async Task SignOutAsync(HttpContext context)
    {
        var oidcEnabled = context.RequestServices.GetRequiredService<IConfiguration>()
            .GetValue<bool>("Authentication:OpenIdConnect:Enabled");

        if (!oidcEnabled)
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.Response.Redirect("/");
            return;
        }

        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignOutAsync(
            OpenIdConnectDefaults.AuthenticationScheme,
            new AuthenticationProperties { RedirectUri = "/" });
    }
}

public static class BffProxy
{
    public static async ValueTask AttachAccessToken(Yarp.ReverseProxy.Transforms.RequestTransformContext transformContext)
    {
        var httpContext = transformContext.HttpContext;
        var token = await httpContext.GetTokenAsync("access_token");
        if (string.IsNullOrEmpty(token))
        {
            token = httpContext.User.FindFirst("access_token")?.Value;
        }

        if (!string.IsNullOrEmpty(token))
        {
            transformContext.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}
