using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using ResidencyRoll.Web.Bff;
using Xunit;

namespace ResidencyRoll.Tests;

public class DownstreamCapture
{
    public string Method { get; set; } = string.Empty;
    public string PathAndQuery { get; set; } = string.Empty;
    public string Authorization { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    public void Reset()
    {
        Method = string.Empty;
        PathAndQuery = string.Empty;
        Authorization = string.Empty;
        Body = string.Empty;
    }
}

public class BffWebApplicationFactory : WebApplicationFactory<WebEntryPoint>
{
    public bool OidcEnabled { get; set; } = true;
    public DownstreamCapture Capture { get; } = new();

    private WebApplication? _downstream;

    public BffWebApplicationFactory()
    {
        var downstreamBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            ContentRootPath = Path.GetTempPath(),
            ApplicationName = "ResidencyRoll.BffDownstream",
            EnvironmentName = "Development"
        });
        downstreamBuilder.WebHost.UseUrls("http://127.0.0.1:0");
        _downstream = downstreamBuilder.Build();
        _downstream.Run(async context =>
        {
            Capture.Method = context.Request.Method;
            Capture.PathAndQuery = context.Request.Path + context.Request.QueryString;
            Capture.Authorization = context.Request.Headers.Authorization.ToString();
            using var reader = new StreamReader(context.Request.Body);
            Capture.Body = await reader.ReadToEndAsync();
            if (context.Request.Path.StartsWithSegments("/mcp"))
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.ContentType = "text/event-stream";
                await context.Response.WriteAsync("data: {\"ok\":true}\n\n");
                await context.Response.Body.FlushAsync();
                return;
            }

            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"proxied\":true}");
        });
        _downstream.StartAsync().GetAwaiter().GetResult();
    }

    public string DownstreamUrl => _downstream!.Urls.Single();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Api:BaseUrl"] = DownstreamUrl,
                ["Authentication:OpenIdConnect:Enabled"] = OidcEnabled ? "true" : "false",
                ["Authentication:OpenIdConnect:Authority"] = "https://example.invalid",
                ["Authentication:OpenIdConnect:ClientId"] = "test-client",
                ["Authentication:OpenIdConnect:ClientSecret"] = "test-secret",
                ["Authentication:OpenIdConnect:RequireHttpsMetadata"] = "false",
                ["Authentication:OpenIdConnect:ApiAudience"] = "https://example.invalid/api",
                ["DataProtection:KeysPath"] = Path.Join(Path.GetTempPath(), "residencyroll-tests", Guid.NewGuid().ToString("N"))
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IStartupFilter>(new TestSignInStartupFilter());
            if (OidcEnabled)
            {
                services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
                {
                    options.RequireHttpsMetadata = false;
                    options.Configuration = new OpenIdConnectConfiguration
                    {
                        Issuer = "https://example.invalid/",
                        AuthorizationEndpoint = "https://example.invalid/authorize",
                        TokenEndpoint = "https://example.invalid/oauth/token",
                        EndSessionEndpoint = "https://example.invalid/v2/logout"
                    };
                });
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _downstream is not null)
        {
            _downstream.StopAsync().GetAwaiter().GetResult();
            _downstream.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _downstream = null;
        }

        base.Dispose(disposing);
    }

    private sealed class TestSignInStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    if (!context.Request.Path.Equals("/test/signin", StringComparison.OrdinalIgnoreCase))
                    {
                        await nextMiddleware();
                        return;
                    }

                    var properties = new AuthenticationProperties();
                    properties.StoreTokens(
                    [
                        new AuthenticationToken { Name = "access_token", Value = "test-access-token" }
                    ]);

                    var identity = new ClaimsIdentity(
                        [
                            new Claim("name", "Ada Lovelace"),
                            new Claim("email", "ada@example.com")
                        ],
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        "name",
                        "role");

                    await context.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(identity),
                        properties);
                    context.Response.StatusCode = StatusCodes.Status204NoContent;
                });
                next(app);
            };
        }
    }
}

public class OidcEnabledBffFactory : BffWebApplicationFactory
{
    public OidcEnabledBffFactory()
    {
        OidcEnabled = true;
    }
}

public class OidcDisabledBffFactory : BffWebApplicationFactory
{
    public OidcDisabledBffFactory()
    {
        OidcEnabled = false;
    }
}

public class BffOidcEnabledTests : IClassFixture<OidcEnabledBffFactory>
{
    private readonly OidcEnabledBffFactory _factory;

    public BffOidcEnabledTests(OidcEnabledBffFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Proxy_ForwardsPathQueryBody_AndAttachesAccessToken()
    {
        _factory.Capture.Reset();
        var client = CreateClient();
        using var signInBody = new StringContent(string.Empty);
        var signIn = await client.PostAsync("/test/signin", signInBody);
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/trips?startDate=2026-01-01")
        {
            Content = new StringContent("{\"hello\":\"world\"}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-CSRF", "1");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"proxied\":true}", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpMethod.Post.Method, _factory.Capture.Method);
        Assert.Equal("/api/v1/trips?startDate=2026-01-01", _factory.Capture.PathAndQuery);
        Assert.Equal("Bearer test-access-token", _factory.Capture.Authorization);
        Assert.Equal("{\"hello\":\"world\"}", _factory.Capture.Body);
    }

    [Fact]
    public async Task SignInCallback_WithoutACorrelationCookie_RedirectsHome()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/signin-oidc?code=stale&state=stale");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task AnonymousApiCall_Returns401()
    {
        _factory.Capture.Reset();
        var client = CreateClient();

        var response = await client.GetAsync("/api/v1/trips");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Location"));
        Assert.Equal(string.Empty, _factory.Capture.Method);
    }

    [Fact]
    public async Task PostWithoutCsrfHeader_Returns400()
    {
        _factory.Capture.Reset();
        var client = CreateClient();
        await SignInAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/trips", new { hello = "world" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(string.Empty, _factory.Capture.Method);
    }

    [Fact]
    public async Task LogoutWithoutCsrfHeader_Returns400()
    {
        var client = CreateClient();

        using var logoutBody = new StringContent(string.Empty);
        var response = await client.PostAsync("/auth/logout", logoutBody);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LogoutWithFormCsrf_IsAccepted()
    {
        var client = CreateClient();
        using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["X-CSRF"] = "1" });

        var response = await client.PostAsync("/auth/logout", content);

        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("https://evil.example/phish")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    public async Task Login_RejectsNonLocalReturnUrl(string returnUrl)
    {
        var client = CreateClient();

        var response = await client.GetAsync("/auth/login?returnUrl=" + Uri.EscapeDataString(returnUrl));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task McpProxy_ForwardsTheClientToken_WithoutCsrfOrTheCookieToken()
    {
        _factory.Capture.Reset();
        var client = CreateClient();
        await SignInAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent("{\"jsonrpc\":\"2.0\"}", Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer client-token");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("data:", await response.Content.ReadAsStringAsync());
        Assert.Equal("Bearer client-token", _factory.Capture.Authorization);
        Assert.Equal("/mcp", _factory.Capture.PathAndQuery);
    }

    [Fact]
    public async Task McpProxy_AnonymousCall_IsNotChallengedByTheBff()
    {
        _factory.Capture.Reset();
        var client = CreateClient();

        using var body = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/mcp", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(string.Empty, _factory.Capture.Authorization);
    }

    [Fact]
    public async Task McpInfo_ReportsThatOAuthIsRequired()
    {
        var client = CreateClient();
        await SignInAsync(client);

        var response = await client.GetAsync("/bff/mcp-info");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("enabled").GetBoolean());
        Assert.True(document.RootElement.GetProperty("oauthRequired").GetBoolean());
        Assert.EndsWith("/mcp", document.RootElement.GetProperty("url").GetString());
    }

    [Fact]
    public async Task UserEndpoint_ReturnsSignedInUser()
    {
        var client = CreateClient();
        await SignInAsync(client);

        var response = await client.GetAsync("/bff/user");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Ada Lovelace", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("ada@example.com", doc.RootElement.GetProperty("email").GetString());
        Assert.True(doc.RootElement.GetProperty("isAuthenticated").GetBoolean());
    }

    private static async Task SignInAsync(HttpClient client)
    {
        using var content = new StringContent(string.Empty);
        using var response = await client.PostAsync("/test/signin", content);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
}

public class BffOidcDisabledTests : IClassFixture<OidcDisabledBffFactory>
{
    private readonly OidcDisabledBffFactory _factory;

    public BffOidcDisabledTests(OidcDisabledBffFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Proxy_ForwardsWithoutAccessToken()
    {
        _factory.Capture.Reset();
        var client = CreateClient();

        var response = await client.GetAsync("/api/v1/trips?startDate=2026-01-01");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("/api/v1/trips?startDate=2026-01-01", _factory.Capture.PathAndQuery);
        Assert.Equal(string.Empty, _factory.Capture.Authorization);
    }

    [Fact]
    public async Task McpInfo_ReportsThatOAuthIsNotRequired()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/bff/mcp-info");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("enabled").GetBoolean());
        Assert.False(document.RootElement.GetProperty("oauthRequired").GetBoolean());
    }

    [Fact]
    public async Task UserEndpoint_ReturnsLocalUser()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/bff/user");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Local User", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("local@localhost", doc.RootElement.GetProperty("email").GetString());
        Assert.True(doc.RootElement.GetProperty("isAuthenticated").GetBoolean());
    }

    [Fact]
    public async Task Login_RedirectsLocalReturnUrlWithoutOidc()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/auth/login?returnUrl=/manage-trips");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/manage-trips", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Login_RejectsAbsoluteReturnUrl()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/auth/login?returnUrl=" + Uri.EscapeDataString("https://evil.example"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
}
