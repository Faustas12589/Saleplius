using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SalePlus.Api.Services;
using SalePlius.Shared.DTOs;
using SalePlius.Shared.Models;

namespace SalePlus.Api.Controllers;

/// <summary>
/// Prisijungimas ir žetonai. Prisijungus grąžinami du žetonai:
/// access (JWT, 15 min., siunčiamas "Authorization: Bearer") ir refresh (7 d., tik žetonams atnaujinti).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json", "application/xml")]
public class AuthController : ControllerBase
{
    private readonly IFirestoreService _service;
    private readonly ITokenService _tokenService;

    public AuthController(IFirestoreService service, ITokenService tokenService)
    {
        _service = service;
        _tokenService = tokenService;
    }

    /// <summary>Prisijungti vartotojo vardu arba el. paštu. Grąžina access ir refresh žetonus.</summary>
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    [Consumes("application/json", "application/xml")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto)
    {
        var (user, error) = await _service.AuthenticateUserAsync(dto.UsernameOrEmail, dto.Password);
        if (error != null || user == null)
        {
            return Unauthorized(new { message = error ?? "Neteisingi prisijungimo duomenys." });
        }

        return Ok(await _tokenService.IssueAsync(user));
    }

    /// <summary>Užsiregistruoti. Naujas vartotojas visada gauna rolę User ir iškart yra prisijungęs.</summary>
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    [Consumes("application/json", "application/xml")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterDto dto)
    {
        var (user, error) = await _service.RegisterUserAsync(dto);
        if (error != null || user == null)
        {
            return Conflict(new { message = error ?? "Nepavyko užregistruoti vartotojo." });
        }

        return CreatedAtAction(nameof(Me), null, await _tokenService.IssueAsync(user));
    }

    /// <summary>
    /// Atnaujinti žetonus: senas refresh žetonas iškeičiamas į naują porą ir daugiau nebegalioja (rotacija).
    /// Pakartotinai panaudotas refresh žetonas užbaigia visą sesiją (vagystės aptikimas).
    /// </summary>
    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    [Consumes("application/json", "application/xml")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> Refresh([FromBody] RefreshRequestDto dto)
    {
        var (tokens, error) = await _tokenService.RefreshAsync(dto.RefreshToken);
        return error switch
        {
            RefreshError.None => Ok(tokens),
            RefreshError.Expired => Unauthorized(new { message = "Atnaujinimo žetonas nebegalioja. Prisijunkite iš naujo." }),
            RefreshError.Reused => Unauthorized(new { message = "Šis atnaujinimo žetonas jau panaudotas, sesija užbaigta. Prisijunkite iš naujo." }),
            RefreshError.UserGone => Unauthorized(new { message = "Vartotojo paskyra nebeegzistuoja." }),
            _ => Unauthorized(new { message = "Neteisingas atnaujinimo žetonas." })
        };
    }

    /// <summary>
    /// Atsijungti: dabartinis access žetonas iškart nebegalioja, o su refresh žetonu – ir visa sesija.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout([FromBody] LogoutDto? dto)
    {
        var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        var expires = long.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Exp), out var exp)
            ? DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime
            : DateTime.UtcNow.AddHours(1);

        await _tokenService.LogoutAsync(jti, expires, User.FindFirstValue(ClaimTypes.NameIdentifier)!, dto?.RefreshToken);
        return NoContent();
    }

    /// <summary>Gauti šiuo metu prisijungusio vartotojo informaciją.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfile>> Me()
    {
        var user = await _service.GetUserByIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
        return user == null ? NotFound(new { message = "Vartotojas nerastas." }) : Ok(user);
    }
}
