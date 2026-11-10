using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResidencyRoll.Api.Services;
using ResidencyRoll.Shared.Travellers;
using System.Security.Claims;

namespace ResidencyRoll.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/travellers")]
[Authorize]
public class TravellersController : ControllerBase
{
    private readonly TravellerService _travellers;

    public TravellersController(TravellerService travellers)
    {
        _travellers = travellers;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TravellerDto>>> List()
    {
        var userId = UserId();
        var people = await _travellers.ListAsync(userId);
        return Ok(people.Select(traveller => ToDto(traveller, userId)));
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TravellerDto>> Create([FromBody] SaveTravellerDto request)
    {
        try
        {
            var created = await _travellers.CreateAsync(UserId(), request?.Name ?? string.Empty, request?.HomeCountry);
            var dto = ToDto(created, UserId());
            return CreatedAtAction(nameof(List), new { version = "1.0" }, dto);
        }
        catch (TravellerValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TravellerDto>> Update(int id, [FromBody] SaveTravellerDto request)
    {
        try
        {
            var updated = await _travellers.UpdateAsync(UserId(), id, request?.Name ?? string.Empty, request?.HomeCountry);
            return Ok(ToDto(updated, UserId()));
        }
        catch (TravellerResolutionException)
        {
            return NotFound();
        }
        catch (TravellerValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, [FromQuery] bool deleteTrips = false)
    {
        try
        {
            var deleted = await _travellers.DeleteAsync(UserId(), id, deleteTrips);
            return deleted ? NoContent() : NotFound();
        }
        catch (TravellerConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    private static TravellerDto ToDto(Models.Traveller traveller, string userId) => new()
    {
        Id = traveller.Id,
        Name = traveller.Name,
        HomeCountry = string.IsNullOrWhiteSpace(traveller.HomeCountry) ? null : traveller.HomeCountry,
        IsSelf = string.Equals(traveller.LinkedUserId, userId, StringComparison.Ordinal)
    };

    private string UserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException("Unable to determine user identity");
        }

        return userId;
    }
}
