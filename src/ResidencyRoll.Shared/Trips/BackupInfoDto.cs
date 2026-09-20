namespace ResidencyRoll.Shared.Trips;

public class BackupInfoDto
{
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
}
