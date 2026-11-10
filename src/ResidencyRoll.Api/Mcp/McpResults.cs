using System.Collections;
using System.Reflection;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ResidencyRoll.Api.Models;

namespace ResidencyRoll.Api.Mcp;

internal static class McpResults
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static CallToolResult Ok(string summary, object payload) => new()
    {
        Content = [new TextContentBlock { Text = summary }],
        StructuredContent = JsonSerializer.SerializeToElement(payload, Json)
    };

    public static CallToolResult For(Traveller traveller, string summary, object payload)
    {
        Stamp(payload, new McpTravellerRef { Id = traveller.Id, Name = traveller.Name }, []);
        var text = string.IsNullOrWhiteSpace(summary) ? traveller.Name : $"{traveller.Name}: {summary}";
        return Ok(text, payload);
    }

    private static void Stamp(object? payload, McpTravellerRef traveller, HashSet<object> seen)
    {
        if (payload is null)
        {
            return;
        }

        var type = payload.GetType();
        if (type == typeof(string) || type.IsPrimitive || type.IsEnum || type.Namespace == "System")
        {
            return;
        }

        if (!seen.Add(payload))
        {
            return;
        }

        var property = type.GetProperty("Traveller");
        if (property is { CanWrite: true } && property.PropertyType == typeof(McpTravellerRef))
        {
            property.SetValue(payload, traveller);
        }

        if (payload is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                Stamp(item, traveller, seen);
            }
        }

        if (type.Namespace?.StartsWith("ResidencyRoll", StringComparison.Ordinal) != true)
        {
            return;
        }

        foreach (var child in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!child.CanRead || child.GetIndexParameters().Length > 0 || child.Name == "Traveller")
            {
                continue;
            }

            object? value;
            try
            {
                value = child.GetValue(payload);
            }
            catch (TargetInvocationException)
            {
                continue;
            }

            if (value is null || value.GetType().IsValueType)
            {
                continue;
            }

            Stamp(value, traveller, seen);
        }
    }
}

public sealed class McpTravellerRef
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
