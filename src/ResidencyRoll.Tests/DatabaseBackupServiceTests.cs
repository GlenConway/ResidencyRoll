using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ResidencyRoll.Api.Configuration;
using ResidencyRoll.Api.Services;
using Xunit;

namespace ResidencyRoll.Tests;

public class DatabaseBackupServiceTests
{
    [Theory]
    [InlineData("2026-09-20T12:00:00Z", "2026-09-21T04:00:00Z")] // Sunday -> next day
    [InlineData("2026-09-21T03:59:59Z", "2026-09-21T04:00:00Z")] // Monday before 04:00
    [InlineData("2026-09-21T04:00:00Z", "2026-09-28T04:00:00Z")] // exactly 04:00 -> next week
    [InlineData("2026-09-21T10:00:00Z", "2026-09-28T04:00:00Z")] // Monday after 04:00
    [InlineData("2026-09-23T10:00:00Z", "2026-09-28T04:00:00Z")] // midweek
    public void GetNextRunUtc_GivenTime_ShouldReturnNextMondayAt0400Utc(string now, string expected)
    {
        var result = DatabaseBackupService.GetNextRunUtc(DateTimeOffset.Parse(now));
        Assert.Equal(DateTimeOffset.Parse(expected), result);
    }

    [Fact]
    public void GetNextRunUtc_NonUtcOffset_ShouldConvertToUtc()
    {
        // Monday 03:00 UTC expressed as Sunday 22:00 in UTC-5
        var now = new DateTimeOffset(2026, 9, 20, 22, 0, 0, TimeSpan.FromHours(-5));
        var result = DatabaseBackupService.GetNextRunUtc(now);
        Assert.Equal(new DateTimeOffset(2026, 9, 21, 4, 0, 0, TimeSpan.Zero), result);
    }

    private sealed class FakeTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void Backup_MoreThanRetainCount_ShouldKeepOnlyNewestBackups()
    {
        var root = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var dbPath = Path.Join(root, "test.db");
            using (var conn = new SqliteConnection($"Data Source={dbPath};Pooling=False"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "CREATE TABLE t (id INTEGER); INSERT INTO t VALUES (1);";
                cmd.ExecuteNonQuery();
            }

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Default"] = $"Data Source={dbPath}" })
                .Build();
            var time = new FakeTime();
            var service = new DatabaseBackupService(
                config,
                Options.Create(new DatabaseBackupOptions { RetainCount = 4 }),
                NullLogger<DatabaseBackupService>.Instance,
                time);

            for (var week = 0; week < 6; week++)
            {
                time.Now = new DateTimeOffset(2026, 9, 7, 4, 0, 0, TimeSpan.Zero).AddDays(7.0 * week);
                service.Backup();
            }

            var backups = service.ListBackups();
            Assert.Equal(4, backups.Count);
            Assert.Equal("residencyroll-20261012-040000.db", backups[0].FileName);
            Assert.Equal("residencyroll-20260921-040000.db", backups[3].FileName);
            Assert.Empty(Directory.GetFiles(Path.Join(root, "backups"), "*.tmp"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
