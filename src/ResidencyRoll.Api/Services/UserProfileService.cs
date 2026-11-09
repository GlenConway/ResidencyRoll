using Microsoft.EntityFrameworkCore;
using ResidencyRoll.Api.Data;
using ResidencyRoll.Api.Models;

namespace ResidencyRoll.Api.Services;

public class UserProfileService
{
    private readonly ApplicationDbContext _context;

    public UserProfileService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string?> GetHomeCountryAsync(string userId)
    {
        var profile = await _context.UserProfiles.AsNoTracking().FirstOrDefaultAsync(profile => profile.UserId == userId);
        return string.IsNullOrWhiteSpace(profile?.HomeCountry) ? null : profile.HomeCountry;
    }

    public async Task<string?> SetHomeCountryAsync(string userId, string? homeCountry)
    {
        var normalized = string.IsNullOrWhiteSpace(homeCountry) ? null : homeCountry.Trim();
        if (normalized is { Length: > UserProfile.MaxHomeCountryLength })
        {
            throw new ArgumentException($"homeCountry must be {UserProfile.MaxHomeCountryLength} characters or fewer.");
        }

        var profile = await _context.UserProfiles.FirstOrDefaultAsync(item => item.UserId == userId);
        if (profile is null)
        {
            if (normalized is null)
            {
                return null;
            }

            _context.UserProfiles.Add(new UserProfile { UserId = userId, HomeCountry = normalized });
        }
        else
        {
            profile.HomeCountry = normalized;
        }

        await _context.SaveChangesAsync();
        return normalized;
    }
}
