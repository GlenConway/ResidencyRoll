using Microsoft.EntityFrameworkCore;
using ResidencyRoll.Api.Data;
using ResidencyRoll.Api.Models;

namespace ResidencyRoll.Api.Services;

/// <summary>
/// Creates one self traveller per existing user and stamps trips that have no traveller.
/// Safe to run on every start. Does nothing once the data is filled.
/// </summary>
public static class TravellerBackfill
{
    public static void Run(ApplicationDbContext db)
    {
        if (!TableExists(db, "Travellers") || !TableExists(db, "Trips"))
        {
            return;
        }

        var userIds = ReadStrings(db, "SELECT UserId FROM Trips UNION SELECT UserId FROM UserProfiles");
        var linked = ReadStrings(db, "SELECT LinkedUserId FROM Travellers WHERE LinkedUserId IS NOT NULL")
            .ToHashSet(StringComparer.Ordinal);
        var missing = userIds
            .Where(id => !string.IsNullOrWhiteSpace(id) && !linked.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var unassigned = Count(db, "SELECT COUNT(*) FROM Trips WHERE TravellerId IS NULL");
        if (missing.Count == 0 && unassigned == 0)
        {
            return;
        }

        using var transaction = db.Database.BeginTransaction();
        foreach (var userId in missing)
        {
            var home = db.UserProfiles.AsNoTracking()
                .Where(profile => profile.UserId == userId)
                .Select(profile => profile.HomeCountry)
                .FirstOrDefault();
            db.Travellers.Add(new Traveller
            {
                OwnerUserId = userId,
                LinkedUserId = userId,
                Name = Traveller.DefaultName,
                HomeCountry = string.IsNullOrWhiteSpace(home) ? null : home.Trim(),
                CreatedUtc = DateTime.UtcNow
            });
        }

        db.SaveChanges();
        new TravellerAccess(db).AssignUnscopedTrips();
        transaction.Commit();
    }

    private static bool TableExists(ApplicationDbContext db, string name)
    {
        return Count(db, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = {0}", name) > 0;
    }

    private static int Count(ApplicationDbContext db, string sql, params object[] args)
    {
        return db.Database.SqlQueryRaw<int>(sql, args).AsEnumerable().First();
    }

    private static List<string> ReadStrings(ApplicationDbContext db, string sql)
    {
        return db.Database.SqlQueryRaw<string>(sql).AsEnumerable().Where(value => value is not null).ToList();
    }
}
