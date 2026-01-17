using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResidencyRoll.Api.Mappings;
using ResidencyRoll.Api.Models;
using ResidencyRoll.Api.Services;
using ResidencyRoll.Shared.Trips;
using System.Security.Claims;

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
        existing.ArrivalCountry = request.ArrivalCountry;
        existing.ArrivalCity = request.ArrivalCity;
        existing.ArrivalDateTime = request.ArrivalDateTime;
        existing.ArrivalTimezone = request.ArrivalTimezone;

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
}
