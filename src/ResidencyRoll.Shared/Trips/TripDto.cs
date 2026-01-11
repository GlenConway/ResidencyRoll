namespace ResidencyRoll.Shared.Trips;

public class TripDto
{
    public int Id { get; set; }
    public string CountryName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int DurationDays => Math.Max(0, (EndDate - StartDate).Days);
}
