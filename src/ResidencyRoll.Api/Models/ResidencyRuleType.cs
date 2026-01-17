namespace ResidencyRoll.Api.Models;

/// <summary>
/// Defines the type of residency counting rule for a country.
/// </summary>
public enum ResidencyRuleType
{
    /// <summary>
    /// Midnight Rule: A day counts only if physically present at 00:00 local time.
    /// Used by: Canada, UK, Australia, New Zealand, etc.
    /// </summary>
    MidnightRule,

    /// <summary>
    /// Partial Day Rule: A day counts if physically present for ANY part of the day.
    /// Used by: USA (Substantial Presence Test), with transit exception.
    /// </summary>
    PartialDayRule
}
