using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResidencyRoll.Api.Mappings;
using ResidencyRoll.Api.Models;
using ResidencyRoll.Api.Services;
using ResidencyRoll.Shared.Trips;
using System.Globalization;
using System.Security.Claims;
using System.Text;

namespace ResidencyRoll.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class TripsController : ControllerBase
{
    private readonly TripService _tripService;
    private readonly ResidencyCalculationService _residencyService;

    public TripsController(TripService tripService, ResidencyCalculationService residencyService)
    {
        _tripService = tripService;
        _residencyService = residencyService;
    }

    private string GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException("Unable to determine user identity");
        }
        return userId;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TripDto>>> GetTrips()
    {
        var userId = GetUserId();
        var trips = await _tripService.GetAllTripsAsync(userId);
        return Ok(trips.Select(t => t.ToDto()));
    }

    [HttpGet("timeline")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TripDto>>> GetTimeline()
    {
        var userId = GetUserId();
        var trips = await _tripService.GetTripsForTimelineAsync(userId);
        return Ok(trips.Select(t => t.ToDto()));
    }

    [HttpGet("days-per-country")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CountryDaysDto>>> GetTotalDaysPerCountry()
    {
        var userId = GetUserId();
        var totals = await _tripService.GetTotalDaysPerCountryAsync(userId);
        var result = totals.Select(kvp => new CountryDaysDto { CountryName = kvp.Key, Days = kvp.Value });
        return Ok(result);
    }

    [HttpGet("days-per-country/last365")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CountryDaysDto>>> GetDaysPerCountryLast365()
    {
        var userId = GetUserId();
        var totals = await _tripService.GetDaysPerCountryInLast365DaysAsync(userId);
        var result = totals.Select(kvp => new CountryDaysDto { CountryName = kvp.Key, Days = kvp.Value });
        return Ok(result);
    }

    [HttpGet("total-days-away/last365")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> GetTotalDaysAwayLast365()
    {
        var userId = GetUserId();
        var total = await _tripService.GetTotalDaysAwayInLast365DaysAsync(userId);
        return Ok(total);
    }

    [HttpGet("days-at-home/last365")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> GetDaysAtHomeLast365()
    {
        var userId = GetUserId();
        var total = await _tripService.GetDaysAtHomeInLast365DaysAsync(userId);
        return Ok(total);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TripDto>> GetTrip(int id)
    {
        var userId = GetUserId();
        var trip = await _tripService.GetTripByIdAsync(id, userId);
        if (trip == null)
        {
            return NotFound();
        }
        return Ok(trip.ToDto());
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TripDto>> CreateTrip([FromBody] TripDto request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = GetUserId();
        var entity = request.ToEntity();
        entity.UserId = userId;
        var created = await _tripService.CreateTripAsync(entity);
        var dto = created.ToDto();
        return CreatedAtAction(nameof(GetTrip), new { id = dto.Id, version = "1.0" }, dto);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTrip(int id, [FromBody] TripDto request)
    {
        if (id != request.Id)
        {
            return BadRequest("ID mismatch");
        }

        var userId = GetUserId();
        var existing = await _tripService.GetTripByIdAsync(id, userId);
        if (existing == null)
        {
            return NotFound();
        }

        // Update legacy fields
        existing.CountryName = request.CountryName;
        existing.StartDate = request.StartDate;
        existing.EndDate = request.EndDate;
        
        // Update timezone-aware fields
        existing.DepartureCountry = request.DepartureCountry;
        existing.DepartureCity = request.DepartureCity;
        existing.DepartureDateTime = request.DepartureDateTime;
        existing.DepartureTimezone = request.DepartureTimezone;
        existing.DepartureIataCode = request.DepartureIataCode;
        existing.ArrivalCountry = request.ArrivalCountry;
        existing.ArrivalCity = request.ArrivalCity;
        existing.ArrivalDateTime = request.ArrivalDateTime;
        existing.ArrivalTimezone = request.ArrivalTimezone;
        existing.ArrivalIataCode = request.ArrivalIataCode;

        await _tripService.UpdateTripAsync(existing, userId);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTrip(int id)
    {
        var userId = GetUserId();
        var existing = await _tripService.GetTripByIdAsync(id, userId);
        if (existing == null)
        {
            return NotFound();
        }

        await _tripService.DeleteTripAsync(id, userId);
        return NoContent();
    }

    [HttpPost("forecast")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ForecastResponseDto>> Forecast([FromBody] ForecastRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = GetUserId();
        var (current, forecast) = await _tripService.ForecastDaysWithTripAsync(userId, request.CountryName, request.TripStart, request.TripEnd);

        var response = new ForecastResponseDto
        {
            Current = current.Select(kvp => new CountryDaysDto { CountryName = kvp.Key, Days = kvp.Value }).ToList(),
            Forecast = forecast.Select(kvp => new CountryDaysDto { CountryName = kvp.Key, Days = kvp.Value }).ToList()
        };

        return Ok(response);
    }

    [HttpPost("forecast/max-end-date")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MaxTripEndDateResponseDto>> CalculateMaxEndDate([FromBody] MaxTripEndDateRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = GetUserId();
        var (maxEndDate, daysAtLimit) = await _tripService.CalculateMaxTripEndDateAsync(userId, request.CountryName, request.TripStart, request.DayLimit);
        return Ok(new MaxTripEndDateResponseDto
        {
            MaxEndDate = maxEndDate,
            DaysAtLimit = daysAtLimit
        });
    }

    [HttpPost("forecast/standard-durations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<StandardDurationForecastItemDto>>> GetStandardDurationForecasts([FromBody] StandardDurationForecastRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = GetUserId();
        var results = await _tripService.CalculateStandardDurationForecastsAsync(userId, request.CountryName, request.TripStart, request.DayLimit, request.Durations);

        var response = results.Select(r => new StandardDurationForecastItemDto
        {
            DurationDays = r.DurationDays,
            EndDate = r.EndDate,
            TotalDaysInCountry = r.TotalDaysInCountry,
            ExceedsLimit = r.ExceedsLimit
        });

        return Ok(response);
    }

    [HttpGet("daily-presence")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<DailyPresenceDto>>> GetDailyPresence(
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null)
    {
        var userId = GetUserId();
        var trips = await _tripService.GetAllTripsAsync(userId);
        var dailyPresence = _residencyService.GenerateDailyPresenceLog(trips);
        
        // Filter by date range if provided
        var filteredPresence = dailyPresence.AsEnumerable();
        if (startDate.HasValue)
        {
            filteredPresence = filteredPresence.Where(dp => dp.Date >= startDate.Value);
        }
        if (endDate.HasValue)
        {
            filteredPresence = filteredPresence.Where(dp => dp.Date <= endDate.Value);
        }
        
        var result = filteredPresence.Select(dp => new DailyPresenceDto
        {
            Date = dp.Date,
            LocationAtMidnight = dp.LocationAtMidnight,
            LocationsDuringDay = dp.LocationsDuringDay.ToList(),
            IsInTransitAtMidnight = dp.IsInTransitAtMidnight
        });
        
        return Ok(result);
    }

    [HttpGet("residency-summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ResidencySummaryDto>>> GetResidencySummary(
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null)
    {
        var userId = GetUserId();
        var trips = await _tripService.GetAllTripsAsync(userId);
        var dailyPresence = _residencyService.GenerateDailyPresenceLog(trips);
        var residencyDays = _residencyService.CalculateResidencyDays(dailyPresence, startDate, endDate);
        
        var summary = new List<ResidencySummaryDto>();
        foreach (var kvp in residencyDays.OrderByDescending(x => x.Value))
        {
            var rule = _residencyService.GetCountryRule(kvp.Key);
            var thresholdDays = rule.ResidencyThresholdDays ?? 183;
            var daysUntilThreshold = thresholdDays - kvp.Value;
            
            summary.Add(new ResidencySummaryDto
            {
                CountryName = kvp.Key,
                TotalDays = kvp.Value,
                RuleType = rule.RuleType == ResidencyRuleType.MidnightRule ? "Midnight" : "PartialDay",
                ThresholdDays = thresholdDays,
                IsApproachingThreshold = daysUntilThreshold <= 30 && daysUntilThreshold > 0,
                DaysUntilThreshold = Math.Max(0, daysUntilThreshold)
            });
        }
        
        return Ok(summary);
    }

    [HttpGet("export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportTrips()
    {
        var userId = GetUserId();
        var trips = await _tripService.GetAllTripsAsync(userId);
        
        var csvLines = new List<string> 
        { 
            "DepartureCountry,DepartureCity,DepartureDateTime,DepartureTimezone,DepartureIataCode,ArrivalCountry,ArrivalCity,ArrivalDateTime,ArrivalTimezone,ArrivalIataCode" 
        };

        foreach (var trip in trips.OrderBy(t => t.ArrivalDateTime))
        {
            var line = string.Join(',',
                EscapeCsv(trip.DepartureCountry ?? string.Empty),
                EscapeCsv(trip.DepartureCity ?? string.Empty),
                trip.DepartureDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                EscapeCsv(trip.DepartureTimezone ?? "UTC"),
                EscapeCsv(trip.DepartureIataCode ?? string.Empty),
                EscapeCsv(trip.ArrivalCountry ?? string.Empty),
                EscapeCsv(trip.ArrivalCity ?? string.Empty),
                trip.ArrivalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                EscapeCsv(trip.ArrivalTimezone ?? "UTC"),
                EscapeCsv(trip.ArrivalIataCode ?? string.Empty));
            csvLines.Add(line);
        }

        var bytes = Encoding.UTF8.GetBytes(string.Join('\n', csvLines));
        return File(bytes, "text/csv", "trips.csv");
    }

    [HttpPost("import")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<object>> ImportTrips(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("File is empty.");
        }

        var userId = GetUserId();

        // Helper function to validate IANA timezone
        bool IsValidIanaTimezone(string? timezone)
        {
            if (string.IsNullOrWhiteSpace(timezone))
                return false;
            
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(timezone);
                return true;
            }
            catch
            {
                return false;
            }
        }

        using var reader = new StreamReader(file.OpenReadStream());
        var content = await reader.ReadToEndAsync();
        var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        var newTrips = new List<Trip>();
        var invalidRows = new List<string>();
        var isFirst = true;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (isFirst && (line.StartsWith("DepartureCountry", StringComparison.OrdinalIgnoreCase) || 
                            line.StartsWith("CountryName", StringComparison.OrdinalIgnoreCase)))
            {
                isFirst = false;
                continue;
            }

            isFirst = false;

            var parts = ParseCsvLine(line);
            
            // Support new format with IATA codes (v2): DepartureCountry,DepartureCity,DepartureDateTime,DepartureTimezone,DepartureIataCode,ArrivalCountry,ArrivalCity,ArrivalDateTime,ArrivalTimezone,ArrivalIataCode
            if (parts.Length >= 10)
            {
                if (!DateTime.TryParse(parts[2], CultureInfo.InvariantCulture, DateTimeStyles.None, out var departureDateTime) ||
                    !DateTime.TryParse(parts[7], CultureInfo.InvariantCulture, DateTimeStyles.None, out var arrivalDateTime))
                {
                    invalidRows.Add($"Invalid date format in row: {line}");
                    continue;
                }

                // Validate timezones
                if (!IsValidIanaTimezone(parts[3]))
                {
                    invalidRows.Add($"Invalid departure timezone '{parts[3]}' (use IANA format like 'America/Halifax', not 'AST'): {line}");
                    continue;
                }
                if (!IsValidIanaTimezone(parts[8]))
                {
                    invalidRows.Add($"Invalid arrival timezone '{parts[8]}' (use IANA format like 'America/Halifax', not 'AST'): {line}");
                    continue;
                }

                newTrips.Add(new Trip
                {
                    UserId = userId,
                    DepartureCountry = parts[0],
                    DepartureCity = parts[1],
                    DepartureDateTime = departureDateTime,
                    DepartureTimezone = parts[3],
                    DepartureIataCode = string.IsNullOrWhiteSpace(parts[4]) ? null : parts[4],
                    ArrivalCountry = parts[5],
                    ArrivalCity = parts[6],
                    ArrivalDateTime = arrivalDateTime,
                    ArrivalTimezone = parts[8],
                    ArrivalIataCode = string.IsNullOrWhiteSpace(parts[9]) ? null : parts[9],
                    // Set legacy fields for compatibility
                    CountryName = parts[5],
                    StartDate = arrivalDateTime,
                    EndDate = departureDateTime
                });
            }
            // Support old format (v1): DepartureCountry,DepartureCity,DepartureDateTime,DepartureTimezone,ArrivalCountry,ArrivalCity,ArrivalDateTime,ArrivalTimezone
            else if (parts.Length >= 8)
            {
                if (!DateTime.TryParse(parts[2], CultureInfo.InvariantCulture, DateTimeStyles.None, out var departureDateTime) ||
                    !DateTime.TryParse(parts[6], CultureInfo.InvariantCulture, DateTimeStyles.None, out var arrivalDateTime))
                {
                    invalidRows.Add($"Invalid date format in row: {line}");
                    continue;
                }

                // Validate timezones
                if (!IsValidIanaTimezone(parts[3]))
                {
                    invalidRows.Add($"Invalid departure timezone '{parts[3]}' (use IANA format like 'America/Halifax', not 'AST'): {line}");
                    continue;
                }
                if (!IsValidIanaTimezone(parts[7]))
                {
                    invalidRows.Add($"Invalid arrival timezone '{parts[7]}' (use IANA format like 'America/Halifax', not 'AST'): {line}");
                    continue;
                }

                newTrips.Add(new Trip
                {
                    UserId = userId,
                    DepartureCountry = parts[0],
                    DepartureCity = parts[1],
                    DepartureDateTime = departureDateTime,
                    DepartureTimezone = parts[3],
                    ArrivalCountry = parts[4],
                    ArrivalCity = parts[5],
                    ArrivalDateTime = arrivalDateTime,
                    ArrivalTimezone = parts[7],
                    // Set legacy fields for compatibility
                    CountryName = parts[4],
                    StartDate = arrivalDateTime,
                    EndDate = departureDateTime
                });
            }
            // Legacy format support (for migration): CountryName,StartDate,EndDate
            else if (parts.Length >= 3)
            {
                if (!DateTime.TryParse(parts[1], CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) ||
                    !DateTime.TryParse(parts[2], CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
                {
                    invalidRows.Add($"Invalid date format in legacy format row (expected yyyy-MM-dd HH:mm:ss): {line}");
                    continue;
                }

                // Convert legacy format to new format with UTC
                newTrips.Add(new Trip
                {
                    UserId = userId,
                    DepartureCountry = parts[0],
                    DepartureCity = string.Empty,
                    DepartureDateTime = end,
                    DepartureTimezone = "UTC",
                    ArrivalCountry = parts[0],
                    ArrivalCity = string.Empty,
                    ArrivalDateTime = start,
                    ArrivalTimezone = "UTC",
                    CountryName = parts[0],
                    StartDate = start,
                    EndDate = end
                });
            }
            else
            {
                invalidRows.Add($"Row has incorrect number of columns (expected 8 or 10, got {parts.Length}): {line}");
                continue;
            }
        }

        if (newTrips.Count == 0)
        {
            var errorMessage = "No valid trips found in file.";
            if (invalidRows.Count > 0)
            {
                errorMessage += $"\n\nAll {invalidRows.Count} rows had errors:\n" + string.Join("\n", invalidRows.Take(15));
                if (invalidRows.Count > 15)
                {
                    errorMessage += $"\n... and {invalidRows.Count - 15} more errors";
                }
            }
            return BadRequest(errorMessage);
        }

        var successCount = 0;
        var creationErrors = new List<string>();
        
        foreach (var trip in newTrips)
        {
            try
            {
                await _tripService.CreateTripAsync(trip);
                successCount++;
            }
            catch (Exception ex)
            {
                creationErrors.Add($"Failed to create trip ({trip.ArrivalCountry} -> {trip.DepartureCountry}): {ex.Message}");
            }
        }

        var allErrors = invalidRows.Concat(creationErrors).ToList();

        if (successCount == 0)
        {
            var errorMessage = "No trips were successfully imported.";
            if (allErrors.Count > 0)
            {
                errorMessage += $"\n\nErrors encountered:\n" + string.Join("\n", allErrors.Take(15));
                if (allErrors.Count > 15)
                {
                    errorMessage += $"\n... and {allErrors.Count - 15} more errors";
                }
            }
            return BadRequest(errorMessage);
        }

        var resultMessage = $"Successfully imported {successCount} trip(s).";
        if (allErrors.Count > 0)
        {
            resultMessage += $"\n\nNote: {allErrors.Count} error(s) encountered:\n" + 
                            string.Join("\n", allErrors.Take(5));
            if (allErrors.Count > 5)
            {
                resultMessage += $"\n... and {allErrors.Count - 5} more errors";
            }
        }

        return Ok(new { Imported = successCount, Message = resultMessage, Errors = allErrors.Count });
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"'))
        {
            return '"' + value.Replace("\"", "\"\"") + '"';
        }
        return value;
    }

    private static string[] ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        values.Add(current.ToString());
        return values.ToArray();
    }
}
