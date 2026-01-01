using System;

namespace ResidencyRoll.Web.Models;

public class Trip
{
    public int Id { get; set; }
    public string CountryName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    // Counts midnights between arrival (inclusive) and departure (exclusive).
    public int DurationDays => Math.Max(0, (EndDate - StartDate).Days);
}
