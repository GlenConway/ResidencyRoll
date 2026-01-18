namespace ResidencyRoll.Api.Models;

/// <summary>
/// Defines the residency counting rules for a specific country.
/// </summary>
public class CountryResidencyRule
{
    public string CountryCode { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public ResidencyRuleType RuleType { get; set; }

    /// <summary>
    /// For USA: Transit exception - less than 24 hours while in transit between two foreign points.
    /// </summary>
    public bool HasTransitException { get; set; }

    /// <summary>
    /// Common residency threshold in days (e.g., 183 days for most countries).
    /// </summary>
    public int? ResidencyThresholdDays { get; set; }
}
