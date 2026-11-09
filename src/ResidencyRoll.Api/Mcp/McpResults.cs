using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace ResidencyRoll.Api.Mcp;

internal static class McpResults
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static CallToolResult Ok(string summary, object payload) => new()
    {
        Content = [new TextContentBlock { Text = summary }],
        StructuredContent = JsonSerializer.SerializeToElement(payload, Json)
    };
}
