using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using ResidencyRoll.Api.Models;
using ResidencyRoll.Api.Services;

namespace ResidencyRoll.Api.Mcp;

[McpServerPromptType]
public sealed class ResidencyPrompts
{
    [McpServerPrompt(Name = "residency_check"), Description("Asks for the home country, then reviews the residency summary and an optional forecast.")]
    public static IEnumerable<ChatMessage> ResidencyCheck(
        [Description("Home country. Ask the user when it is missing.")] string homeCountry)
    {
        return
        [
            new ChatMessage(
                ChatRole.User,
                $"The user's home country is {homeCountry}. Call get_residency_summary for the last 365 days with this homeCountry. If they describe a future trip, call forecast_trips before saying whether any country crosses its threshold.")
        ];
    }
}

[McpServerResourceType]
public sealed class CountryRulesResource
{
    [McpServerResource(UriTemplate = "residencyroll://rules/countries", Name = "country-rules", MimeType = "application/json")]
    [Description("Known country residency rules: midnight versus partial day, and the day threshold.")]
    public static string Read(ResidencyCalculationService rules)
    {
        var payload = rules.ListCountryRules().Select(rule => new
        {
            countryName = rule.CountryName,
            countryCode = rule.CountryCode,
            ruleType = rule.RuleType == ResidencyRuleType.MidnightRule ? "Midnight" : "PartialDay",
            thresholdDays = rule.ResidencyThresholdDays ?? 183,
            hasTransitException = rule.HasTransitException
        });
        return JsonSerializer.Serialize(payload);
    }
}
