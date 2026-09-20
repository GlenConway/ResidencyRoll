namespace ResidencyRoll.Shared.Trips;

public class SystemInfoDto
{
    public string DatabasePath { get; set; } = string.Empty;
    public bool DatabaseExists { get; set; }
    public long DatabaseSizeBytes { get; set; }
    public int UserCount { get; set; }
    public int TripCount { get; set; }
    public string ApplicationVersion { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public List<BackupInfoDto> Backups { get; set; } = new();
    public DateTimeOffset? NextBackupUtc { get; set; }
}
