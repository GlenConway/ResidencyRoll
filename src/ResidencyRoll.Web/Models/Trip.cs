using System;

namespace ResidencyRoll.Web.Models;

public class Trip
{
    public int Id { get; set; }
    public string CountryName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    // Inclusive duration in days; not mapped to the database.
    public int DurationDays => Math.Max(0, (EndDate - StartDate).Days + 1);
}
