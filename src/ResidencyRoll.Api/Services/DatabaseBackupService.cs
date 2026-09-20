using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using ResidencyRoll.Api.Configuration;
using ResidencyRoll.Shared.Trips;

namespace ResidencyRoll.Api.Services;

/// <summary>
/// Backs up the SQLite database every Monday at 04:00 UTC and prunes old backups.
/// </summary>
public class DatabaseBackupService : BackgroundService
{
    private const string FilePrefix = "residencyroll-";
    private const string FileExtension = ".db";
    private const string TimestampFormat = "yyyyMMdd-HHmmss";

    private readonly IConfiguration _configuration;
    private readonly IOptions<DatabaseBackupOptions> _options;
    private readonly ILogger<DatabaseBackupService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly object _backupLock = new();

    public DatabaseBackupService(
        IConfiguration configuration,
        IOptions<DatabaseBackupOptions> options,
        ILogger<DatabaseBackupService> logger,
        TimeProvider timeProvider)
    {
        _configuration = configuration;
        _options = options;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Returns the next Monday 04:00 UTC strictly after <paramref name="now"/>.
    /// </summary>
    public static DateTimeOffset GetNextRunUtc(DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        var candidate = new DateTimeOffset(utc.Year, utc.Month, utc.Day, 4, 0, 0, TimeSpan.Zero);
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)candidate.DayOfWeek + 7) % 7;
        candidate = candidate.AddDays(daysUntilMonday);
        return candidate <= utc ? candidate.AddDays(7) : candidate;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.Enabled)
        {
            _logger.LogWarning("Database backup is disabled");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = _timeProvider.GetUtcNow();
            var next = GetNextRunUtc(now);
            _logger.LogInformation("Next database backup scheduled for {NextRun:u}", next);

            try
            {
                await DelayUntilAsync(next - now, stoppingToken);
                Backup();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database backup failed");
            }
        }
    }

    /// <summary>
    /// Task.Delay caps at roughly 49 days, so long waits are chunked.
    /// </summary>
    private static async Task DelayUntilAsync(TimeSpan delay, CancellationToken ct)
    {
        var chunk = TimeSpan.FromDays(7);
        while (delay > TimeSpan.Zero)
        {
            var step = delay < chunk ? delay : chunk;
            await Task.Delay(step, ct);
            delay -= step;
        }
    }

    private (string DbPath, string Directory) ResolvePaths()
    {
        var connectionString = _configuration.GetConnectionString("Default") ?? "Data Source=residencyroll.db";
        var dbPath = Path.GetFullPath(new SqliteConnectionStringBuilder(connectionString).DataSource);
        var configured = _options.Value.Directory;
        var directory = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Path.GetDirectoryName(dbPath)!, "backups")
            : Path.GetFullPath(configured);
        return (dbPath, directory);
    }

    public DateTimeOffset? NextScheduledRunUtc =>
        _options.Value.Enabled ? GetNextRunUtc(_timeProvider.GetUtcNow()) : null;

    public IReadOnlyList<BackupInfoDto> ListBackups()
    {
        var (_, directory) = ResolvePaths();
        if (!Directory.Exists(directory))
        {
            return Array.Empty<BackupInfoDto>();
        }

        return Directory
            .EnumerateFiles(directory, $"{FilePrefix}*{FileExtension}")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.Name, StringComparer.Ordinal)
            .Select(f => new BackupInfoDto
            {
                FileName = f.Name,
                SizeBytes = f.Length,
                CreatedUtc = new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero)
            })
            .ToList();
    }

    /// <summary>
    /// Resolves a listed backup by file name; null when it does not exist. Path segments are rejected.
    /// </summary>
    public string? GetBackupPath(string fileName)
    {
        if (fileName != Path.GetFileName(fileName)
            || !fileName.StartsWith(FilePrefix, StringComparison.Ordinal)
            || !fileName.EndsWith(FileExtension, StringComparison.Ordinal))
        {
            return null;
        }

        var path = Path.Combine(ResolvePaths().Directory, fileName);
        return File.Exists(path) ? path : null;
    }

    public string Backup()
    {
        lock (_backupLock)
        {
            var (dbPath, directory) = ResolvePaths();
            var retain = Math.Max(1, _options.Value.RetainCount);
            Directory.CreateDirectory(directory);

            var stamp = _timeProvider.GetUtcNow().ToString(TimestampFormat);
            var finalPath = Path.Combine(directory, $"{FilePrefix}{stamp}{FileExtension}");
            var tempPath = finalPath + ".tmp";

            using (var sourceConnection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString()))
            using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = tempPath,
                Pooling = false
            }.ToString()))
            {
                sourceConnection.Open();
                destination.Open();
                sourceConnection.BackupDatabase(destination);
            }

            File.Move(tempPath, finalPath, overwrite: true);
            _logger.LogInformation("Database backed up to {BackupPath}", finalPath);

            Prune(directory, retain);
            return finalPath;
        }
    }

    private void Prune(string directory, int retain)
    {
        var stale = Directory
            .EnumerateFiles(directory, $"{FilePrefix}*{FileExtension}")
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
            .Skip(retain);

        foreach (var file in stale)
        {
            File.Delete(file);
            _logger.LogInformation("Deleted old database backup {BackupPath}", file);
        }
    }
}
