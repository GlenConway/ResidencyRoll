namespace ResidencyRoll.Api.Configuration;

/// <summary>
/// Options for the weekly SQLite backup job (Mondays 04:00 UTC).
/// </summary>
public class DatabaseBackupOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Directory that receives backup files. Defaults to a "backups" folder beside the database file.
    /// </summary>
    public string? Directory { get; set; }

    /// <summary>
    /// Number of most recent backups to keep.
    /// </summary>
    public int RetainCount { get; set; } = 4;
}
