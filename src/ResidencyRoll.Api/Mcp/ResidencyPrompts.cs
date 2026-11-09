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
        [Description("Home country. Optional. Leave empty to use the saved home country.")] string? homeCountry = null)
    {
        var home = string.IsNullOrWhiteSpace(homeCountry)
            ? "Call get_home_country. If none is saved, ask the user and call set_home_country before answering days left or days away."
            : $"The user's home country is {homeCountry.Trim()}. Call set_home_country if that is not already saved.";
        return
        [
            new ChatMessage(
                ChatRole.User,
                $"{home} Call get_current_location for the current country and last arrival airport, then get_residency_summary for the last 365 days. Read ruleType before treating a country as midnight-only; partial-day countries count any presence. If they describe a future trip, call forecast_trips and use peakDays and thresholdStatus, because the count at the end of the window can be lower than the peak.")
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
