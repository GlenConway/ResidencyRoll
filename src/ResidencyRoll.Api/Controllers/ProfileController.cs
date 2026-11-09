using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResidencyRoll.Api.Services;
using ResidencyRoll.Shared.Profile;
using System.Security.Claims;

namespace ResidencyRoll.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/profile")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly UserProfileService _profiles;

    public ProfileController(UserProfileService profiles)
    {
        _profiles = profiles;
    }

    [HttpGet("home-country")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<HomeCountryDto>> GetHomeCountry()
    {
        return Ok(new HomeCountryDto { HomeCountry = await _profiles.GetHomeCountryAsync(UserId()) });
    }

    [HttpPut("home-country")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<HomeCountryDto>> SetHomeCountry([FromBody] HomeCountryDto request)
    {
        try
        {
            var saved = await _profiles.SetHomeCountryAsync(UserId(), request?.HomeCountry);
            return Ok(new HomeCountryDto { HomeCountry = saved });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

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
