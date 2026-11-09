using Microsoft.Extensions.Configuration;

namespace ResidencyRoll.Shared.Mcp;

/// <summary>
/// Reads optional MCP settings. Compose passes empty strings for unset variables, and those count as unset.
/// </summary>
public static class McpConfiguration
{
    public static bool IsEnabled(IConfiguration configuration)
    {
        var raw = configuration["Mcp:Enabled"];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        return !bool.TryParse(raw, out var enabled) || enabled;
    }

    public static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
