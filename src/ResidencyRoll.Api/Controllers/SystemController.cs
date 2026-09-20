using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResidencyRoll.Api.Data;
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

    public SystemController(ApplicationDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
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
            Environment = _environment.EnvironmentName
        });
    }
}
