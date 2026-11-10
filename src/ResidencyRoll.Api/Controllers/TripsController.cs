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
    private readonly ResidencyQueryService _queryService;
    private readonly ItineraryParsingService _itineraryParsingService;
    private readonly TravellerAccess _access;
    private readonly TravellerService _travellers;

    public TripsController(
        TripService tripService,
        ResidencyQueryService queryService,
        ItineraryParsingService itineraryParsingService,
        TravellerAccess access,
        TravellerService travellers)
    {
        _tripService = tripService;
        _queryService = queryService;
        _itineraryParsingService = itineraryParsingService;
        _access = access;
        _travellers = travellers;
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

    private async Task<Models.Traveller?> AccessibleTraveller(int? travellerId)
    {
        var userId = GetUserId();
        if (travellerId is null)
        {
            return await _access.ResolveSelfAsync(userId);
        }

        return await _access.FindOwnedAsync(userId, travellerId.Value);
    }

    private void StampNew(Models.Trip entity, Models.Traveller traveller)
    {
        entity.TravellerId = traveller.Id;
        entity.UserId = traveller.OwnerUserId;
        entity.CreatedByUserId = GetUserId();
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TripDto>>> GetTrips([FromQuery] int? travellerId = null)
    {
        var traveller = await AccessibleTraveller(travellerId);
        if (traveller is null)
        {
            return NotFound();
        }

        var trips = await _tripService.GetAllTripsAsync(GetUserId(), traveller);
        return Ok(trips.Select(t => t.ToDto()));
    }

    [HttpGet("timeline")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TripDto>>> GetTimeline([FromQuery] int? travellerId = null)
    {
        var traveller = await AccessibleTraveller(travellerId);
        if (traveller is null)
        {
            return NotFound();
        }

        var trips = await _tripService.GetTripsForTimelineAsync(GetUserId(), traveller);
        return Ok(trips.Select(t => t.ToDto()));
    }

    [HttpGet("days-per-country")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CountryDaysDto>>> GetTotalDaysPerCountry([FromQuery] int? travellerId = null)
    {
        var traveller = await AccessibleTraveller(travellerId);
        if (traveller is null)
        {
            return NotFound();
        }

        var totals = await _tripService.GetTotalDaysPerCountryAsync(GetUserId(), traveller);
        var result = totals.Select(kvp => new CountryDaysDto { CountryName = kvp.Key, Days = kvp.Value });
        return Ok(result);
    }

    [HttpGet("days-per-country/last365")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CountryDaysDto>>> GetDaysPerCountryLast365([FromQuery] int? travellerId = null)
    {
        var traveller = await AccessibleTraveller(travellerId);
        if (traveller is null)
        {
            return NotFound();
        }

        var totals = await _tripService.GetDaysPerCountryInLast365DaysAsync(GetUserId(), traveller);
        var result = totals.Select(kvp => new CountryDaysDto { CountryName = kvp.Key, Days = kvp.Value });
        return Ok(result);
    }

    [HttpGet("total-days-away/last365")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> GetTotalDaysAwayLast365([FromQuery] int? travellerId = null)
    {
        var traveller = await AccessibleTraveller(travellerId);
        if (traveller is null)
        {
            return NotFound();
        }

        var total = await _tripService.GetTotalDaysAwayInLast365DaysAsync(GetUserId(), traveller);
        return Ok(total);
    }

    [HttpGet("days-at-home/last365")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> GetDaysAtHomeLast365([FromQuery] int? travellerId = null)
    {
        var traveller = await AccessibleTraveller(travellerId);
        if (traveller is null)
        {
            return NotFound();
        }

        var total = await _tripService.GetDaysAtHomeInLast365DaysAsync(GetUserId(), traveller);
        return Ok(total);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TripDto>> GetTrip(int id, [FromQuery] int? travellerId = null)
    {
        var traveller = await AccessibleTraveller(travellerId);
        if (traveller is null)
        {
            return NotFound();
        }

        var trip = await _tripService.GetTripByIdAsync(id, GetUserId(), traveller);
        if (trip == null)
        {
            return NotFound();
        }
        return Ok(trip.ToDto());
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTrip(
        [FromBody] TripDto request,
        [FromQuery] int? travellerId = null,
        [FromQuery] int[]? travellerIds = null)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var targets = new List<Models.Traveller>();
        if (travellerIds is { Length: > 0 })
        {
            foreach (var id in travellerIds.Distinct())
            {
                var person = await _access.FindOwnedAsync(GetUserId(), id);
                if (person is null)
                {
                    return NotFound();
                }

                targets.Add(person);
            }
        }
        else
        {
            var person = await AccessibleTraveller(travellerId);
            if (person is null)
            {
                return NotFound();
            }

            targets.Add(person);
        }

        if (targets.Count == 1)
        {
            var entity = request.ToEntity();
            StampNew(entity, targets[0]);
            var created = await _tripService.CreateTripAsync(entity, GetUserId());
            var dto = created.ToDto();
            return CreatedAtAction(nameof(GetTrip), new { id = dto.Id, version = "1.0" }, dto);
        }

        var createdMany = await _tripService.CreateForTravellersAsync(request.ToEntity(), targets, GetUserId());
        return CreatedAtAction(nameof(GetTrip), new { id = createdMany[0].Id, version = "1.0" }, createdMany.Select(trip => trip.ToDto()));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTrip(int id, [FromBody] TripDto request, [FromQuery] int? travellerId = null)
    {
        if (id != request.Id)
        {
            return BadRequest("ID mismatch");
        }

        var traveller = await AccessibleTraveller(travellerId);
        if (traveller is null)
        {
            return NotFound();
        }

        var userId = GetUserId();
        var existing = await _tripService.GetTripByIdAsync(id, userId, traveller);
        if (existing == null)
        {
            return NotFound();
        }

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

        await _tripService.UpdateTripAsync(existing, userId, traveller);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTrip(int id, [FromQuery] int? travellerId = null)
    {
        var traveller = await AccessibleTraveller(travellerId);
        if (traveller is null)
        {
            return NotFound();
        }

        var userId = GetUserId();
        var existing = await _tripService.GetTripByIdAsync(id, userId, traveller);
        if (existing == null)
        {
            return NotFound();
        }

        await _tripService.DeleteTripAsync(id, userId, traveller);
        return NoContent();
    }

    [HttpPost("forecast")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ForecastResponseDto>> Forecast([FromBody] ForecastRequestDto request, [FromQuery] int? travellerId = null)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var traveller = await AccessibleTraveller(travellerId);
        if (traveller is null)
        {
            return NotFound();
        }

        var hypotheticalTrips = request.Legs.Select(leg => new Trip
        {
            UserId = traveller.OwnerUserId,
            TravellerId = traveller.Id,
            DepartureCountry = leg.DepartureCountry,
            DepartureCity = leg.DepartureCity,
            DepartureDateTime = leg.DepartureDateTime,
            DepartureTimezone = leg.DepartureTimezone,
            DepartureIataCode = leg.DepartureIataCode,
            ArrivalCountry = leg.ArrivalCountry,
            ArrivalCity = leg.ArrivalCity,
            ArrivalDateTime = leg.ArrivalDateTime,
            ArrivalTimezone = leg.ArrivalTimezone,
            ArrivalIataCode = leg.ArrivalIataCode
        }).ToList();

        return Ok(await _queryService.ForecastAsync(GetUserId(), hypotheticalTrips, traveller: traveller));
    }

    [HttpPost("forecast/max-end-date")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Obsolete("Legacy single-trip forecast endpoint. Use the multi-leg POST /forecast endpoint instead.")]
    public async Task<ActionResult<MaxTripEndDateResponseDto>> CalculateMaxEndDate([FromBody] MaxTripEndDateRequestDto request)
    {
        throw new NotSupportedException("The max-end-date forecast endpoint has been removed. Use the multi-leg /forecast endpoint instead.");
    }

    [HttpPost("forecast/standard-durations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Obsolete("Legacy single-trip forecast endpoint. Use the multi-leg POST /forecast endpoint instead.")]
    public async Task<ActionResult<IEnumerable<StandardDurationForecastItemDto>>> GetStandardDurationForecasts([FromBody] StandardDurationForecastRequestDto request)
    {
        throw new NotSupportedException("The standard-durations forecast endpoint has been removed. Use the multi-leg /forecast endpoint instead.");
    }

    [HttpGet("daily-presence")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<DailyPresenceDto>>> GetDailyPresence(
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null,
        [FromQuery] int? travellerId = null)
    {
        var traveller = await AccessibleTraveller(travellerId);
        if (traveller is null)
        {
            return NotFound();
        }

        return Ok(await _queryService.GetDailyPresenceAsync(GetUserId(), startDate, endDate, traveller));
    }

    [HttpGet("residency-summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ResidencySummaryDto>>> GetResidencySummary(
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null,
        [FromQuery] int? travellerId = null)
    {
        var traveller = await AccessibleTraveller(travellerId);
        if (traveller is null)
        {
            return NotFound();
        }

        return Ok(await _queryService.GetResidencySummaryAsync(GetUserId(), startDate, endDate, traveller));
    }

    [HttpGet("export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportTrips([FromQuery] int? travellerId = null)
    {
        var traveller = await AccessibleTraveller(travellerId);
        if (traveller is null)
        {
            return NotFound();
        }

        var trips = await _tripService.GetAllTripsAsync(GetUserId(), traveller);
        
        var csvLines = new List<string> 
        { 
            "DepartureCountry,DepartureCity,DepartureDateTime,DepartureTimezone,DepartureIataCode,ArrivalCountry,ArrivalCity,ArrivalDateTime,ArrivalTimezone,ArrivalIataCode,Traveller" 
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
                EscapeCsv(trip.ArrivalIataCode ?? string.Empty),
                EscapeCsv(traveller.Name));
            csvLines.Add(line);
        }

        var bytes = Encoding.UTF8.GetBytes(string.Join('\n', csvLines));
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var filename = $"trips_{timestamp}.csv";
        return File(bytes, "text/csv", filename);
    }

    [HttpGet("daily-presence-export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportDailyPresence(
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null,
        [FromQuery] int? travellerId = null)
    {
        var traveller = await AccessibleTraveller(travellerId);
        if (traveller is null)
        {
            return NotFound();
        }

        var presence = await _queryService.GetDailyPresenceAsync(GetUserId(), startDate, endDate, traveller);

        var csvLines = new List<string> { "date,primary_country,secondary_country,traveller" };

        foreach (var dp in presence)
        {
            var primary = dp.LocationAtMidnight;
            var secondary = string.Join(";", dp.LocationsDuringDay
                .Where(l => !string.Equals(l, primary, StringComparison.OrdinalIgnoreCase))
                .OrderBy(l => l));

            csvLines.Add(string.Join(',',
                dp.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                EscapeCsv(primary),
                EscapeCsv(secondary),
                EscapeCsv(traveller.Name)));
        }

        var bytes = Encoding.UTF8.GetBytes(string.Join('\n', csvLines));
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return File(bytes, "text/csv", $"daily_presence_{timestamp}.csv");
    }

    [HttpPost("import")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<object>> ImportTrips(IFormFile file, [FromQuery] int? travellerId = null)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("File is empty.");
        }

        var defaultTraveller = await AccessibleTraveller(travellerId);
        if (defaultTraveller is null)
        {
            return NotFound();
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

        var staged = new List<(Trip Trip, string? TravellerName)>();
        var invalidRows = new List<string>();
        var isFirst = true;
        var travellerColumn = false;

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
                travellerColumn = ParseCsvLine(line).Any(part => part.Equals("Traveller", StringComparison.OrdinalIgnoreCase));
                isFirst = false;
                continue;
            }

            isFirst = false;

            var parts = ParseCsvLine(line);
            string? rowTraveller = travellerColumn && parts.Length > 10 ? parts[10] : null;
            
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

                staged.Add((new Trip
                {
                    DepartureCountry = parts[0],
                    DepartureCity = parts[1],
                    DepartureDateTime = departureDateTime,
                    DepartureTimezone = parts[3],
                    DepartureIataCode = string.IsNullOrWhiteSpace(parts[4]) ? null : parts[4],
                    ArrivalCountry = parts[5],
                    ArrivalCity = parts[6],
                    ArrivalDateTime = arrivalDateTime,
                    ArrivalTimezone = parts[8],
                    ArrivalIataCode = string.IsNullOrWhiteSpace(parts[9]) ? null : parts[9]
                }, rowTraveller));
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

                staged.Add((new Trip
                {
                    DepartureCountry = parts[0],
                    DepartureCity = parts[1],
                    DepartureDateTime = departureDateTime,
                    DepartureTimezone = parts[3],
                    ArrivalCountry = parts[4],
                    ArrivalCity = parts[5],
                    ArrivalDateTime = arrivalDateTime,
                    ArrivalTimezone = parts[7]
                }, null));
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
                staged.Add((new Trip
                {
                    DepartureCountry = parts[0],
                    DepartureCity = string.Empty,
                    DepartureDateTime = end,
                    DepartureTimezone = "UTC",
                    ArrivalCountry = parts[0],
                    ArrivalCity = string.Empty,
                    ArrivalDateTime = start,
                    ArrivalTimezone = "UTC"
                }, null));
            }
            else
            {
                invalidRows.Add($"Row has incorrect number of columns (expected 8 or 10, got {parts.Length}): {line}");
                continue;
            }
        }

        if (staged.Count == 0)
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
        
        var named = new Dictionary<string, Models.Traveller>(StringComparer.OrdinalIgnoreCase);
        foreach (var (trip, name) in staged)
        {
            var target = defaultTraveller;
            if (!string.IsNullOrWhiteSpace(name))
            {
                var key = name.Trim();
                if (!named.TryGetValue(key, out target!))
                {
                    try
                    {
                        target = await _travellers.ResolveAsync(userId, key);
                        named[key] = target;
                    }
                    catch (TravellerResolutionException ex)
                    {
                        creationErrors.Add(ex.Message);
                        continue;
                    }
                }
            }

            StampNew(trip, target);
            try
            {
                await _tripService.CreateTripAsync(trip, userId);
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

    [HttpPost("parse-itinerary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ItineraryParsingResponseDto>> ParseItinerary([FromBody] ItineraryParsingRequestDto request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ItineraryText))
        {
            return BadRequest(new ItineraryParsingResponseDto 
            { 
                Error = "ItineraryText is required" 
            });
        }

        var result = await _itineraryParsingService.ParseItineraryAsync(request.ItineraryText);
        return Ok(result);
    }

    [HttpGet("parse-itinerary/availability")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<bool> GetItineraryParsingAvailability()
    {
        return Ok(_itineraryParsingService.IsConfigured());
    }
}
