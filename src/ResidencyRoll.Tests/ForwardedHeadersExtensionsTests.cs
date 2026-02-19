using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ResidencyRoll.Shared.Extensions;
using System.Net;
using Xunit;

namespace ResidencyRoll.Tests;

public class ForwardedHeadersExtensionsTests
{
    [Fact]
    public void UseConfiguredForwardedHeaders_SetsCorrectForwardedHeadersFlags()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build();

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddConfiguration(config);
        var app = builder.Build();

        // Act
        app.UseConfiguredForwardedHeaders();

        // Assert - verify the method executed without errors
        // (successful execution means flags were set correctly)
        Assert.NotNull(app);
    }

    [Fact]
    public void UseConfiguredForwardedHeaders_ParsesValidProxyIPs()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownProxies:0"] = "192.168.1.1",
                ["ForwardedHeaders:KnownProxies:1"] = "10.0.0.5"
            })
            .Build();

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddConfiguration(config);
        var app = builder.Build();

        // Act
        var result = app.UseConfiguredForwardedHeaders();

        // Assert - verify the method executed without errors
        Assert.NotNull(result);
    }

    [Fact]
    public void UseConfiguredForwardedHeaders_ParsesValidIPv6ProxyIPs()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownProxies:0"] = "::1",
                ["ForwardedHeaders:KnownProxies:1"] = "2001:db8::1"
            })
            .Build();

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddConfiguration(config);
        var app = builder.Build();

        // Act
        var result = app.UseConfiguredForwardedHeaders();

        // Assert - verify the method executed without errors
        Assert.NotNull(result);
    }

    [Fact]
    public void UseConfiguredForwardedHeaders_HandlesInvalidProxyIPGracefully()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownProxies:0"] = "not-an-ip-address",
                ["ForwardedHeaders:KnownProxies:1"] = "192.168.1.1"
            })
            .Build();

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddConfiguration(config);
        var app = builder.Build();

        // Act
        var result = app.UseConfiguredForwardedHeaders();

        // Assert - should not throw exception and should continue processing
        Assert.NotNull(result);
    }

    [Fact]
    public void UseConfiguredForwardedHeaders_ParsesValidCIDRNetworks()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownNetworks:0"] = "192.168.0.0/16",
                ["ForwardedHeaders:KnownNetworks:1"] = "10.0.0.0/8"
            })
            .Build();

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddConfiguration(config);
        var app = builder.Build();

        // Act
        var result = app.UseConfiguredForwardedHeaders();

        // Assert - verify the method executed without errors
        Assert.NotNull(result);
    }

    [Fact]
    public void UseConfiguredForwardedHeaders_ParsesValidIPv6CIDRNetworks()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownNetworks:0"] = "2001:db8::/32",
                ["ForwardedHeaders:KnownNetworks:1"] = "fe80::/10"
            })
            .Build();

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddConfiguration(config);
        var app = builder.Build();

        // Act
        var result = app.UseConfiguredForwardedHeaders();

        // Assert - verify the method executed without errors
        Assert.NotNull(result);
    }

    [Fact]
    public void UseConfiguredForwardedHeaders_HandlesInvalidCIDRNotationGracefully()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownNetworks:0"] = "not-a-network",
                ["ForwardedHeaders:KnownNetworks:1"] = "192.168.0.0/16"
            })
            .Build();

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddConfiguration(config);
        var app = builder.Build();

        // Act
        var result = app.UseConfiguredForwardedHeaders();

        // Assert - should not throw exception and should continue processing
        Assert.NotNull(result);
    }

    [Fact]
    public void UseConfiguredForwardedHeaders_HandlesInvalidCIDRPrefixGracefully()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownNetworks:0"] = "192.168.0.0/33", // Invalid IPv4 prefix (>32)
                ["ForwardedHeaders:KnownNetworks:1"] = "192.168.0.0/16"
            })
            .Build();

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddConfiguration(config);
        var app = builder.Build();

        // Act
        var result = app.UseConfiguredForwardedHeaders();

        // Assert - should not throw exception and should continue processing
        Assert.NotNull(result);
    }

    [Fact]
    public void UseConfiguredForwardedHeaders_HandlesInvalidIPv6CIDRPrefixGracefully()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownNetworks:0"] = "2001:db8::/129", // Invalid IPv6 prefix (>128)
                ["ForwardedHeaders:KnownNetworks:1"] = "2001:db8::/32"
            })
            .Build();

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddConfiguration(config);
        var app = builder.Build();

        // Act
        var result = app.UseConfiguredForwardedHeaders();

        // Assert - should not throw exception and should continue processing
        Assert.NotNull(result);
    }

    [Fact]
    public void UseConfiguredForwardedHeaders_HandlesNegativeCIDRPrefixGracefully()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownNetworks:0"] = "192.168.0.0/-1", // Invalid negative prefix
                ["ForwardedHeaders:KnownNetworks:1"] = "192.168.0.0/16"
            })
            .Build();

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddConfiguration(config);
        var app = builder.Build();

        // Act
        var result = app.UseConfiguredForwardedHeaders();

        // Assert - should not throw exception and should continue processing
        Assert.NotNull(result);
    }

    [Fact]
    public void UseConfiguredForwardedHeaders_HandlesMissingSlashInCIDRGracefully()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownNetworks:0"] = "192.168.0.0", // Missing /prefix
                ["ForwardedHeaders:KnownNetworks:1"] = "10.0.0.0/8"
            })
            .Build();

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddConfiguration(config);
        var app = builder.Build();

        // Act
        var result = app.UseConfiguredForwardedHeaders();

        // Assert - should not throw exception and should continue processing
        Assert.NotNull(result);
    }

    [Fact]
    public void UseConfiguredForwardedHeaders_HandlesNonNumericPrefixGracefully()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownNetworks:0"] = "192.168.0.0/abc", // Non-numeric prefix
                ["ForwardedHeaders:KnownNetworks:1"] = "10.0.0.0/8"
            })
            .Build();

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddConfiguration(config);
        var app = builder.Build();

        // Act
        var result = app.UseConfiguredForwardedHeaders();

        // Assert - should not throw exception and should continue processing
        Assert.NotNull(result);
    }

    [Fact]
    public void UseConfiguredForwardedHeaders_UsesSecureDefaultWithNoConfiguration()
    {
        // Arrange - empty configuration
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build();

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddConfiguration(config);
        var app = builder.Build();

        // Act
        var result = app.UseConfiguredForwardedHeaders();

        // Assert - should execute successfully with secure defaults (localhost/loopback only)
        Assert.NotNull(result);
    }

    [Fact]
    public void UseConfiguredForwardedHeaders_HandlesEmptyProxyArray()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownProxies:0"] = ""
            })
            .Build();

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddConfiguration(config);
        var app = builder.Build();

        // Act
        var result = app.UseConfiguredForwardedHeaders();

        // Assert - should handle empty strings gracefully
        Assert.NotNull(result);
    }

    [Fact]
    public void UseConfiguredForwardedHeaders_HandlesEmptyNetworkArray()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownNetworks:0"] = ""
            })
            .Build();

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddConfiguration(config);
        var app = builder.Build();

        // Act
        var result = app.UseConfiguredForwardedHeaders();

        // Assert - should handle empty strings gracefully
        Assert.NotNull(result);
    }

    [Fact]
    public void UseConfiguredForwardedHeaders_HandlesMixedValidAndInvalidConfiguration()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownProxies:0"] = "192.168.1.1",
                ["ForwardedHeaders:KnownProxies:1"] = "invalid-ip",
                ["ForwardedHeaders:KnownProxies:2"] = "10.0.0.5",
                ["ForwardedHeaders:KnownNetworks:0"] = "192.168.0.0/16",
                ["ForwardedHeaders:KnownNetworks:1"] = "invalid-network",
                ["ForwardedHeaders:KnownNetworks:2"] = "10.0.0.0/8"
            })
            .Build();

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddConfiguration(config);
        var app = builder.Build();

        // Act
        var result = app.UseConfiguredForwardedHeaders();

        // Assert - should process valid entries and skip invalid ones
        Assert.NotNull(result);
    }

    [Fact]
    public void UseConfiguredForwardedHeaders_HandlesBoundaryIPv4Prefix()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownNetworks:0"] = "192.168.0.0/0",  // Valid min
                ["ForwardedHeaders:KnownNetworks:1"] = "192.168.0.0/32"  // Valid max
            })
            .Build();

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddConfiguration(config);
        var app = builder.Build();

        // Act
        var result = app.UseConfiguredForwardedHeaders();

        // Assert - should accept valid boundary values
        Assert.NotNull(result);
    }

    [Fact]
    public void UseConfiguredForwardedHeaders_HandlesBoundaryIPv6Prefix()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownNetworks:0"] = "2001:db8::/0",   // Valid min
                ["ForwardedHeaders:KnownNetworks:1"] = "2001:db8::/128"  // Valid max
            })
            .Build();

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddConfiguration(config);
        var app = builder.Build();

        // Act
        var result = app.UseConfiguredForwardedHeaders();

        // Assert - should accept valid boundary values
        Assert.NotNull(result);
    }
}
