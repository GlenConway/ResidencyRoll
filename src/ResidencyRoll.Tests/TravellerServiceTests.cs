using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ResidencyRoll.Api.Data;
using ResidencyRoll.Api.Models;
using ResidencyRoll.Api.Services;
using Xunit;

namespace ResidencyRoll.Tests;

public class TravellerServiceTests
{
    [Fact]
    public async Task Trips_AreIsolatedBetweenTravellersAndAccounts()
    {
        await using var db = NewDatabase();
        var access = new TravellerAccess(db);
        var travellers = new TravellerService(db, access);
        var trips = new TripService(db, new ResidencyCalculationService(), access);

        var glen = await travellers.CreateAsync("glen", "Glen", "Canada");
        glen.LinkedUserId = "glen";
        var sam = await travellers.CreateAsync("glen", "Sam", null);
        await db.SaveChangesAsync();
        var other = await access.ResolveSelfAsync("other");

        await trips.CreateTripAsync(Leg(glen, new DateTime(2026, 1, 1), new DateTime(2026, 1, 10)), "glen");
        await trips.CreateTripAsync(Leg(sam, new DateTime(2026, 2, 1), new DateTime(2026, 2, 5)), "glen");
        await trips.CreateTripAsync(Leg(other, new DateTime(2026, 3, 1), new DateTime(2026, 3, 4)), "other");

        var glenTrips = await trips.GetAllTripsAsync("glen", glen);
        var samTrips = await trips.GetAllTripsAsync("glen", sam);
        Assert.Single(glenTrips);
        Assert.Single(samTrips);
        Assert.NotEqual(glenTrips[0].Id, samTrips[0].Id);
        var otherTrips = await trips.GetAllTripsAsync("other", other);
        Assert.Single(otherTrips);
        Assert.NotEqual(glenTrips[0].Id, otherTrips[0].Id);
        Assert.Null(await access.FindOwnedAsync("other", sam.Id));
    }

    [Fact]
    public async Task Resolve_MatchesNamesIgnoringCase_AndReportsAmbiguity()
    {
        await using var db = NewDatabase();
        var travellers = new TravellerService(db, new TravellerAccess(db));
        await travellers.CreateAsync("glen", "Sam", null);
        var self = await travellers.ResolveAsync("glen", null);
        Assert.Equal("Me", self.Name);

        var sam = await travellers.ResolveAsync("glen", "sam");
        Assert.Equal("Sam", sam.Name);

        await travellers.CreateAsync("glen", "Ann", null);
        await travellers.CreateAsync("glen", "ann smith", null);
        var error = await Assert.ThrowsAsync<TravellerResolutionException>(() => travellers.ResolveAsync("glen", "nobody"));
        Assert.Contains("Sam", error.Message);
        Assert.Contains("Ann", error.Message);
    }

    [Fact]
    public async Task Delete_RefusesTheCallersOwnTraveller()
    {
        await using var db = NewDatabase();
        var travellers = new TravellerService(db, new TravellerAccess(db));
        var self = await travellers.ResolveAsync("glen", null);
        var error = await Assert.ThrowsAsync<TravellerConflictException>(() => travellers.DeleteAsync("glen", self.Id, deleteTrips: true));
        Assert.Contains("cannot be deleted", error.Message);
    }

    [Fact]
    public async Task Delete_WithTrips_RequiresTheFlag_AndCopySkipsDuplicates()
    {
        await using var db = NewDatabase();
        var access = new TravellerAccess(db);
        var travellers = new TravellerService(db, access);
        var trips = new TripService(db, new ResidencyCalculationService(), access);
        var self = await travellers.ResolveAsync("glen", null);
        var sam = await travellers.CreateAsync("glen", "Sam", null);
        await trips.CreateTripAsync(Leg(self, new DateTime(2026, 4, 1, 8, 0, 0), new DateTime(2026, 4, 1, 18, 0, 0)), "glen");
        var first = await travellers.CopyTripsAsync("glen", self.Id, sam.Id);
        var blocked = await Assert.ThrowsAsync<TravellerConflictException>(() => travellers.DeleteAsync("glen", sam.Id, false));
        Assert.Contains("deleteTrips", blocked.Message);

        var second = await travellers.CopyTripsAsync("glen", self.Id, sam.Id);
        Assert.Equal(1, first.Copied);
        Assert.Equal(0, first.Skipped);
        Assert.Equal(0, second.Copied);
        Assert.Equal(1, second.Skipped);

        await travellers.DeleteAsync("glen", sam.Id, deleteTrips: true);
        Assert.Empty(await trips.GetAllTripsAsync("glen", sam));
    }

    [Fact]
    public async Task SignIn_RenamesMe_AndLeavesAChosenName()
    {
        await using var db = NewDatabase();
        var access = new TravellerAccess(db);
        await access.ApplySignInNameAsync("glen", "Glen Conway");
        var renamed = await access.ResolveSelfAsync("glen");
        Assert.Equal("Glen Conway", renamed.Name);

        await access.ApplySignInNameAsync("glen", "Someone Else");
        var kept = await access.ResolveSelfAsync("glen");
        Assert.Equal("Glen Conway", kept.Name);
    }

    [Fact]
    public void TripQueries_ShouldNotFilterByUserIdOutsideTravellerAccess()
    {
        var root = ApiDirectory();
        var hits = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}")
                || file.EndsWith("TravellerAccess.cs", StringComparison.Ordinal)
                || file.EndsWith("TravellerBackfill.cs", StringComparison.Ordinal))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                if (lines[index].Contains(".UserId ==", StringComparison.Ordinal))
                {
                    hits.Add($"{Path.GetRelativePath(root, file)}:{index + 1}");
                }
            }
        }

        Assert.Empty(hits);
    }

    private static Trip Leg(Traveller traveller, DateTime departure, DateTime arrival) => new()
    {
        UserId = traveller.OwnerUserId,
        TravellerId = traveller.Id,
        CreatedByUserId = traveller.OwnerUserId,
        DepartureCountry = "Canada",
        DepartureCity = "Halifax",
        DepartureTimezone = "America/Halifax",
        DepartureIataCode = "YHZ",
        DepartureDateTime = departure,
        ArrivalCountry = "United Kingdom",
        ArrivalCity = "London",
        ArrivalTimezone = "Europe/London",
        ArrivalIataCode = "LHR",
        ArrivalDateTime = arrival
    };

    private static ApplicationDbContext NewDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static string ApiDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "ResidencyRoll.Api");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find the API project.");
    }
}

public class TravellerBackfillTests
{
    [Fact]
    public void Backfill_OnASeededDatabaseAndACopy_KeepsCountsAndIsIdempotent()
    {
        var seeded = Path.Combine(Path.GetTempPath(), $"residencyroll-seed-{Guid.NewGuid():N}.db");
        var copy = Path.Combine(Path.GetTempPath(), $"residencyroll-copy-{Guid.NewGuid():N}.db");
        try
        {
            SeedProductionShape(seeded);
            Checkpoint(seeded);
            File.Copy(seeded, copy, overwrite: true);

            var before = ExpectedDays();
            var first = RunBackfill(seeded);
            var second = RunBackfill(seeded);
            var copied = RunBackfill(copy);

            Assert.Equal(before, first.DaysByUser);
            Assert.Equal(first.TravellerIds, second.TravellerIds);
            Assert.Equal(first.TripTravellers, second.TripTravellers);
            Assert.Equal(before, copied.DaysByUser);
            Assert.All(first.TripTravellers, pair => Assert.True(pair.Value > 0));
            Assert.Contains("Canada", first.HomeCountries);
        }
        finally
        {
            TryDelete(seeded);
            TryDelete(copy);
        }
    }

    private static Dictionary<string, int> ExpectedDays()
    {
        var residency = new ResidencyCalculationService();
        var glen = Leg("glen", new DateTime(2026, 1, 2, 8, 0, 0), new DateTime(2026, 1, 20, 12, 0, 0));
        var sam = Leg("sam", new DateTime(2026, 3, 1, 9, 0, 0), new DateTime(2026, 3, 15, 16, 0, 0));
        return new Dictionary<string, int>
        {
            ["glen"] = residency.CalculateResidencyDays(residency.GenerateDailyPresenceLog([glen])).Values.Sum(),
            ["sam"] = residency.CalculateResidencyDays(residency.GenerateDailyPresenceLog([sam])).Values.Sum()
        };
    }

    private static BackfillSnapshot RunBackfill(string path)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite($"Data Source={path}").Options;
        using var db = new ApplicationDbContext(options);
        var pending = db.Database.GetPendingMigrations().ToList();
        var addTravellers = pending.FirstOrDefault(name => name.EndsWith("_AddTravellers", StringComparison.Ordinal));
        if (addTravellers is not null)
        {
            db.Database.Migrate(addTravellers);
        }

        TravellerBackfill.Run(db);
        db.ChangeTracker.Clear();

        var residency = new ResidencyCalculationService();
        var days = new Dictionary<string, int>();
        foreach (var userId in new[] { "glen", "sam" })
        {
            var trips = db.Trips.Where(trip => trip.UserId == userId).ToList();
            days[userId] = residency.CalculateResidencyDays(residency.GenerateDailyPresenceLog(trips)).Values.Sum();
        }

        var snapshot = new BackfillSnapshot(
            days,
            db.Travellers.OrderBy(traveller => traveller.OwnerUserId).Select(traveller => traveller.Id).ToList(),
            db.Trips.OrderBy(trip => trip.Id).ToDictionary(trip => trip.Id, trip => trip.TravellerId),
            db.Travellers.Select(traveller => traveller.HomeCountry ?? string.Empty).ToList());

        db.Database.Migrate();
        db.ChangeTracker.Clear();
        Assert.DoesNotContain(db.Database.GetPendingMigrations(), name => name.EndsWith("_RequireTripTraveller", StringComparison.Ordinal));
        Assert.Equal(2, db.Trips.Count());
        Assert.All(db.Trips, trip => Assert.True(trip.TravellerId > 0));
        return snapshot;
    }

    private static void Checkpoint(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        command.ExecuteNonQuery();
    }

    private static void SeedProductionShape(string path)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite($"Data Source={path}").Options;
        using (var db = new ApplicationDbContext(options))
        {
            var before = db.Database.GetMigrations().Single(name => name.EndsWith("_AddUserProfile", StringComparison.Ordinal));
            db.Database.Migrate(before);
        }

        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO UserProfiles (UserId, HomeCountry) VALUES ('glen', 'Canada');
            INSERT INTO Trips (UserId, DepartureCountry, DepartureCity, DepartureDateTime, DepartureTimezone, DepartureIataCode, ArrivalCountry, ArrivalCity, ArrivalDateTime, ArrivalTimezone, ArrivalIataCode)
            VALUES ('glen', 'Canada', 'Halifax', '2026-01-02 08:00:00', 'America/Halifax', 'YHZ', 'United Kingdom', 'London', '2026-01-20 12:00:00', 'Europe/London', 'LHR');
            INSERT INTO Trips (UserId, DepartureCountry, DepartureCity, DepartureDateTime, DepartureTimezone, DepartureIataCode, ArrivalCountry, ArrivalCity, ArrivalDateTime, ArrivalTimezone, ArrivalIataCode)
            VALUES ('sam', 'Canada', 'Halifax', '2026-03-01 09:00:00', 'America/Halifax', 'YHZ', 'France', 'Paris', '2026-03-15 16:00:00', 'Europe/Paris', 'CDG');
            """;
        command.ExecuteNonQuery();
    }

    private static Trip Leg(string userId, DateTime departure, DateTime arrival) => new()
    {
        UserId = userId,
        DepartureCountry = "Canada",
        DepartureCity = "Halifax",
        DepartureTimezone = "America/Halifax",
        DepartureDateTime = departure,
        ArrivalCountry = userId == "glen" ? "United Kingdom" : "France",
        ArrivalCity = userId == "glen" ? "London" : "Paris",
        ArrivalTimezone = userId == "glen" ? "Europe/London" : "Europe/Paris",
        ArrivalDateTime = arrival
    };

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }

    private sealed record BackfillSnapshot(
        Dictionary<string, int> DaysByUser,
        List<int> TravellerIds,
        Dictionary<int, int> TripTravellers,
        List<string> HomeCountries);
}
