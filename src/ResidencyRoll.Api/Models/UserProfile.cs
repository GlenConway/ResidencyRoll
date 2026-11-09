namespace ResidencyRoll.Api.Models;

public class UserProfile
{
    public const int MaxHomeCountryLength = 100;

    public string UserId { get; set; } = string.Empty;
    public string? HomeCountry { get; set; }
}
