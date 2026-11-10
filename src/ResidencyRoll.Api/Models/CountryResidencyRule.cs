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
    /// Reaching the threshold counts as residency.
    /// </summary>
    public int? ResidencyThresholdDays { get; set; }

    /// <summary>
    /// Which period the threshold applies to. Rolling 365 days, a tax year, or a calendar year.
    /// </summary>
    public CountingWindowType WindowType { get; set; } = CountingWindowType.Rolling365;

    /// <summary>
    /// Month the tax year starts, when <see cref="WindowType"/> is <see cref="CountingWindowType.TaxYear"/>.
    /// </summary>
    public int? TaxYearStartMonth { get; set; }

    /// <summary>
    /// Day the tax year starts, when <see cref="WindowType"/> is <see cref="CountingWindowType.TaxYear"/>.
    /// </summary>
    public int? TaxYearStartDay { get; set; }

    /// <summary>
    /// Days the home country requires in <see cref="MinimumWindowType"/>.
    /// Nova Scotia MSI requires 183 days in the calendar year. The log counts the country, not the province.
    /// </summary>
    public int? MinimumPresenceDays { get; set; }

    /// <summary>
    /// Period for <see cref="MinimumPresenceDays"/>. Null when the country has no minimum.
    /// </summary>
    public CountingWindowType? MinimumWindowType { get; set; }
}

public enum CountingWindowType
{
    Rolling365,
    TaxYear,
    CalendarYear
}
