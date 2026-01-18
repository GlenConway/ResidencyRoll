namespace ResidencyRoll.Api.Models;

/// <summary>
/// Represents where a person was physically present on a specific calendar date.
/// </summary>
public class DailyPresence
{
    /// <summary>
    /// The calendar date (local date, no time component).
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// The location at midnight (00:00) local time for this date.
    /// - For Midnight Rule countries: this is the country counted for residency
    /// - For Partial Day Rule countries: we need to check if present ANY part of the day
    /// </summary>
    public string LocationAtMidnight { get; set; } = string.Empty;

    /// <summary>
    /// All locations visited during any part of this calendar day.
    /// Used for Partial Day Rule (USA) calculation.
    /// </summary>
    public HashSet<string> LocationsDuringDay { get; set; } = new HashSet<string>();

    /// <summary>
    /// Indicates if the person was in transit (international waters/airspace) at midnight.
    /// If true, LocationAtMidnight will be "IN_TRANSIT".
    /// </summary>
    public bool IsInTransitAtMidnight { get; set; }
}
