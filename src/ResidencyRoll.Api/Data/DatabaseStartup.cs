using Microsoft.EntityFrameworkCore;
using ResidencyRoll.Api.Services;

namespace ResidencyRoll.Api.Data;

public static class DatabaseStartup
{
    public const string RequireTravellerSuffix = "_RequireTripTraveller";

    /// <summary>
    /// Applies schema migrations, and runs <see cref="TravellerBackfill"/> after
    /// <c>AddTravellers</c> and before <c>RequireTripTraveller</c>.
    /// </summary>
    public static void Apply(ApplicationDbContext db)
    {
        var pending = db.Database.GetPendingMigrations().ToList();
        var require = pending.FirstOrDefault(name => name.EndsWith(RequireTravellerSuffix, StringComparison.Ordinal));
        if (require is not null)
        {
            var earlier = pending.TakeWhile(name => !string.Equals(name, require, StringComparison.Ordinal)).LastOrDefault();
            if (earlier is not null)
            {
                db.Database.Migrate(earlier);
            }

            TravellerBackfill.Run(db);
            db.Database.Migrate();
            return;
        }

        db.Database.Migrate();
        TravellerBackfill.Run(db);
    }
}
