using ResidencyRoll.Shared.Mcp;

namespace ResidencyRoll.Api.Mcp;

public static class McpUrls
{
    public static string? Audience(IConfiguration configuration) =>
        McpConfiguration.EmptyToNull(configuration["Mcp:Audience"])
        ?? McpConfiguration.EmptyToNull(configuration["Mcp:ResourceUrl"]);

    public static string Resource(HttpRequest request, IConfiguration configuration)
    {
        var configured = McpConfiguration.EmptyToNull(configuration["Mcp:ResourceUrl"]);
        if (configured is not null)
        {
            return configured.TrimEnd('/');
        }

        return $"{request.Scheme}://{request.Host}/mcp";
    }

    public static string Metadata(HttpRequest request, IConfiguration configuration)
    {
        var configured = McpConfiguration.EmptyToNull(configuration["Mcp:ResourceUrl"]);
        if (configured is not null && Uri.TryCreate(configured, UriKind.Absolute, out var uri))
        {
            return $"{uri.Scheme}://{uri.Authority}/.well-known/oauth-protected-resource/mcp";
        }

        return $"{request.Scheme}://{request.Host}/.well-known/oauth-protected-resource/mcp";
    }

    public static IReadOnlyList<string> AllowedOrigins(IConfiguration configuration)
    {
        var cors = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        var mcp = configuration.GetSection("Mcp:AllowedOrigins").Get<string[]>() ?? [];
        return cors
            .Concat(mcp)
            .Select(origin => origin?.Trim())
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToArray();
    }
}
