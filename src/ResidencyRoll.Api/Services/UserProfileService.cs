using ResidencyRoll.Api.Data;
using ResidencyRoll.Api.Models;

namespace ResidencyRoll.Api.Services;

public class UserProfileService
{
    private readonly ApplicationDbContext _context;
    private readonly TravellerAccess _access;

    public UserProfileService(ApplicationDbContext context, TravellerAccess? access = null)
    {
        _context = context;
        _access = access ?? new TravellerAccess(context);
    }

    public async Task<string?> GetHomeCountryAsync(string userId, Traveller? traveller = null)
    {
        var person = traveller ?? await _access.ResolveSelfAsync(userId);
        return string.IsNullOrWhiteSpace(person.HomeCountry) ? null : person.HomeCountry.Trim();
    }

    public async Task<string?> SetHomeCountryAsync(string userId, string? homeCountry, Traveller? traveller = null)
    {
        var normalized = string.IsNullOrWhiteSpace(homeCountry) ? null : homeCountry.Trim();
        if (normalized is { Length: > Traveller.MaxHomeCountryLength })
        {
            throw new ArgumentException($"homeCountry must be {Traveller.MaxHomeCountryLength} characters or fewer.");
        }

        var person = traveller ?? await _access.ResolveSelfAsync(userId);
        var tracked = await _access.FindOwnedAsync(userId, person.Id) ?? person;
        tracked.HomeCountry = normalized;
        await _context.SaveChangesAsync();
        return normalized;
    }
}
