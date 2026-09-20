using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResidencyRoll.Api.Data;
using ResidencyRoll.Api.Services;
using ResidencyRoll.Shared.Trips;
using System.Reflection;

namespace ResidencyRoll.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class SystemController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly DatabaseBackupService _backupService;
    private readonly ILogger<SystemController> _logger;

    public SystemController(
        ApplicationDbContext context,
        IWebHostEnvironment environment,
        DatabaseBackupService backupService,
        ILogger<SystemController> logger)
    {
        _context = context;
        _environment = environment;
        _backupService = backupService;
        _logger = logger;
    }

    [HttpGet("info")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<SystemInfoDto>> GetInfo()
    {
        var dataSource = _context.Database.GetDbConnection().DataSource;
        var databasePath = string.IsNullOrEmpty(dataSource) ? string.Empty : Path.GetFullPath(dataSource);
        var databaseFile = string.IsNullOrEmpty(databasePath) ? null : new FileInfo(databasePath);

        var version = typeof(SystemController).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

        return Ok(new SystemInfoDto
        {
            DatabasePath = databasePath,
            DatabaseExists = databaseFile?.Exists ?? false,
            DatabaseSizeBytes = databaseFile is { Exists: true } ? databaseFile.Length : 0,
            UserCount = await _context.Trips.Select(t => t.UserId).Distinct().CountAsync(),
            TripCount = await _context.Trips.CountAsync(),
            ApplicationVersion = version,
            Environment = _environment.EnvironmentName,
            Backups = _backupService.ListBackups().ToList(),
            NextBackupUtc = _backupService.NextScheduledRunUtc
        });
    }

    [HttpGet("backups/{fileName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DownloadBackup(string fileName)
    {
        var path = _backupService.GetBackupPath(fileName);
        if (path == null)
        {
            return NotFound();
        }

        return PhysicalFile(path, "application/vnd.sqlite3", fileName);
    }

    [HttpPost("backup")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BackupInfoDto>> BackupNow()
    {
        try
        {
            var path = await Task.Run(_backupService.Backup);
            var file = new FileInfo(path);
            return Ok(new BackupInfoDto
            {
                FileName = file.Name,
                SizeBytes = file.Length,
                CreatedUtc = new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual database backup failed");
            return Problem("Database backup failed.");
        }
    }
}
