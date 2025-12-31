namespace ResidencyRoll.Web.Models;

public class Trip
{
    public int Id { get; set; }
    public string CountryName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}
