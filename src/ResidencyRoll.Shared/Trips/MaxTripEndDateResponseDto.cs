namespace ResidencyRoll.Shared.Trips;

public class MaxTripEndDateResponseDto
{
    public DateTime MaxEndDate { get; set; }
    public int DaysAtLimit { get; set; }
    public DateOnly WindowStart { get; set; }
    public DateOnly WindowEnd { get; set; }
    public string RuleType { get; set; } = string.Empty;
    public string WindowType { get; set; } = string.Empty;
}
