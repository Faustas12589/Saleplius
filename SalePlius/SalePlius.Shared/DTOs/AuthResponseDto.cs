using System.ComponentModel.DataAnnotations;
using SalePlius.Shared.Models;

namespace SalePlius.Shared.DTOs;

/// <summary>Returned by login, register and refresh.</summary>
public class AuthResponseDto
{
    /// <summary>Access token (JWT). Send as "Authorization: Bearer ..."; short-lived.</summary>
    public string Token { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; set; }

    /// <summary>Opaque refresh token. Only for POST /api/auth/refresh and /api/auth/logout; long-lived.</summary>
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAt { get; set; }

    public UserProfile User { get; set; } = new();
}

public class RefreshRequestDto
{
    [Required(ErrorMessage = "Atnaujinimo žetonas (refreshToken) yra privalomas")]
    public string RefreshToken { get; set; } = string.Empty;
}

public class LogoutDto
{
    /// <summary>The session's refresh token; when given, the whole session is ended.</summary>
    public string? RefreshToken { get; set; }
}
