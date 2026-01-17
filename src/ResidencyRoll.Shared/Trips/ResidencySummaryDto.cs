namespace ResidencyRoll.Shared.Trips;

public class ResidencySummaryDto
{
    public string CountryName { get; set; } = string.Empty;
    public int TotalDays { get; set; }
    public string RuleType { get; set; } = string.Empty; // "Midnight" or "PartialDay"
    public int ThresholdDays { get; set; } = 183;
    public bool IsApproachingThreshold { get; set; }
    public int DaysUntilThreshold { get; set; }
}
