namespace ResidencyRoll.Shared.Travellers;

public class TravellerDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? HomeCountry { get; set; }
    public bool IsSelf { get; set; }
}

public class SaveTravellerDto
{
    public string Name { get; set; } = string.Empty;
    public string? HomeCountry { get; set; }
}
