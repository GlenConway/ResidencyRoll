using Microsoft.EntityFrameworkCore;
using ResidencyRoll.Api.Data;
using ResidencyRoll.Api.Models;

namespace ResidencyRoll.Api.Services;

/// <summary>
/// The only place that decides which travellers a caller can see, and the only place that
/// matches a legacy trip row to its owner by <see cref="Trip.UserId"/>.
/// </summary>
public class TravellerAccess
{
    private readonly ApplicationDbContext _context;

    public TravellerAccess(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Traveller> ResolveSelfAsync(string userId)
    {
        var existing = await _context.Travellers.FirstOrDefaultAsync(traveller => traveller.LinkedUserId == userId);
        if (existing is not null)
        {
            return existing;
        }

        var home = await _context.UserProfiles.AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => profile.HomeCountry)
            .FirstOrDefaultAsync();

        var created = new Traveller
        {
            OwnerUserId = userId,
            LinkedUserId = userId,
            Name = Traveller.DefaultName,
            HomeCountry = string.IsNullOrWhiteSpace(home) ? null : home.Trim(),
            CreatedUtc = DateTime.UtcNow
        };
        _context.Travellers.Add(created);
        try
        {
            await _context.SaveChangesAsync();
            return created;
        }
        catch (DbUpdateException)
        {
            _context.Entry(created).State = EntityState.Detached;
            return await _context.Travellers.FirstAsync(traveller => traveller.LinkedUserId == userId);
        }
    }

    public Task<bool> CanReadAsync(string userId, int travellerId) =>
        _context.Travellers.AnyAsync(traveller => traveller.Id == travellerId && traveller.OwnerUserId == userId);

    public Task<bool> CanWriteAsync(string userId, int travellerId) => CanReadAsync(userId, travellerId);

    public Task<List<Traveller>> ListAccessibleAsync(string userId) =>
        _context.Travellers.AsNoTracking()
            .Where(traveller => traveller.OwnerUserId == userId)
            .OrderBy(traveller => traveller.Name)
            .ToListAsync();

    public Task<Traveller?> FindOwnedAsync(string userId, int travellerId) =>
        _context.Travellers.FirstOrDefaultAsync(traveller => traveller.Id == travellerId && traveller.OwnerUserId == userId);

    public async Task ApplySignInNameAsync(string userId, string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return;
        }

        var name = displayName.Trim();
        if (name.Length > Traveller.MaxNameLength)
        {
            name = name[..Traveller.MaxNameLength];
        }

        var self = await ResolveSelfAsync(userId);
        if (!string.Equals(self.Name, Traveller.DefaultName, StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(name, Traveller.DefaultName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var taken = await _context.Travellers.AnyAsync(traveller =>
            traveller.OwnerUserId == userId
            && traveller.Id != self.Id
            && traveller.Name.ToLower() == name.ToLower());
        if (taken)
        {
            return;
        }

        self.Name = name;
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Trips for this traveller. A self traveller also keeps legacy rows that were saved
    /// before a traveller id existed (<c>TravellerId</c> 0, still stamped with the owner).
    /// </summary>
    public IQueryable<Trip> TripsFor(Traveller traveller)
    {
        var id = traveller.Id;
        if (string.Equals(traveller.LinkedUserId, traveller.OwnerUserId, StringComparison.Ordinal))
        {
            var owner = traveller.OwnerUserId;
            return _context.Trips.Where(trip => trip.TravellerId == id || (trip.TravellerId == 0 && trip.UserId == owner));
        }

        return _context.Trips.Where(trip => trip.TravellerId == id);
    }

    public int AssignUnscopedTrips()
    {
        return _context.Database.ExecuteSqlRaw(
            """
            UPDATE Trips
            SET TravellerId = (
                SELECT t.Id FROM Travellers t
                WHERE t.LinkedUserId = Trips.UserId AND t.OwnerUserId = Trips.UserId
                LIMIT 1
            ),
            CreatedByUserId = COALESCE(CreatedByUserId, UserId)
            WHERE TravellerId IS NULL
              AND EXISTS (
                SELECT 1 FROM Travellers t
                WHERE t.LinkedUserId = Trips.UserId AND t.OwnerUserId = Trips.UserId
              )
            """);
    }
}
