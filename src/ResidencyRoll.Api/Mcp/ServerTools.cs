using System.ComponentModel;
using System.Reflection;
using System.Security.Claims;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ResidencyRoll.Api.Services;

namespace ResidencyRoll.Api.Mcp;

[McpServerToolType]
public sealed class ServerTools
{
    [McpServerTool(Name = "get_server_info", Title = "Get server info", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(McpServerInfo))]
    [Description("The build this server is running. version is the release, such as 1.0.100-dev. commit is the git revision baked into that build. airportCount is the number of airports the lookup table contains. Call this when a result does not match the tools you expect.")]
    public static CallToolResult GetServerInfo(AirportCatalog airports, ClaimsPrincipal user)
    {
        McpScopes.Require(user, McpScopes.Read);
        var informational = typeof(ServerTools).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        var plus = informational.LastIndexOf('+');
        var version = plus < 0 ? informational : informational[..plus];
        var commit = plus < 0 || plus == informational.Length - 1 ? "unknown" : informational[(plus + 1)..];
        var result = new McpServerInfo
        {
            Version = version,
            Commit = commit,
            AirportCount = airports.All.Count
        };
        return McpResults.Ok($"ResidencyRoll {version} ({commit}), {result.AirportCount} airports.", result);
    }
}
