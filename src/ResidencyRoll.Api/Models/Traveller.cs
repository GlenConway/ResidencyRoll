namespace ResidencyRoll.Api.Models;

public class Traveller
{
    public const int MaxNameLength = 100;
    public const int MaxHomeCountryLength = 100;
    public const string DefaultName = "Me";

    public int Id { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;
    public string Name { get; set; } = DefaultName;
    public string? HomeCountry { get; set; }

    /// <summary>The login that is this person. Null when they never sign in.</summary>
    public string? LinkedUserId { get; set; }

    public DateTime CreatedUtc { get; set; }
}
