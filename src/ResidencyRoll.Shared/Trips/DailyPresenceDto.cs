namespace ResidencyRoll.Shared.Trips;

public class DailyPresenceDto
{
    public DateOnly Date { get; set; }
    public string LocationAtMidnight { get; set; } = string.Empty;
    public List<string> LocationsDuringDay { get; set; } = new();
    public bool IsInTransitAtMidnight { get; set; }
}
