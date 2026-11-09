using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SalePlius.Shared.DTOs;
using SalePlius.Shared.Models;

namespace SalePlus.Api.Services;

/// <summary>Why a refresh was refused (all answered with 401).</summary>
public enum RefreshError
{
    None,
    Unknown,   // never issued (or garbage)
    Expired,
    Reused,    // already used once: possibly stolen, the whole session is revoked
    UserGone
}

public interface ITokenService
{
    /// <summary>Signs a short-lived access token (JWT) for the user.</summary>
    string GenerateToken(UserProfile user);

    /// <summary>Login/register: a new session = access token + refresh token.</summary>
    Task<AuthResponseDto> IssueAsync(UserProfile user);

    /// <summary>Trades a refresh token for a new pair (rotation). The old refresh token stops working.</summary>
    Task<(AuthResponseDto? Tokens, RefreshError Error)> RefreshAsync(string refreshToken);

    /// <summary>Logout: the access token (by jti) and the session's refresh tokens stop working at once.</summary>
    Task LogoutAsync(string? jti, DateTime accessExpiresAt, string userId, string? refreshToken);
}

/// <summary>
/// Token strategy:
/// - access token = JWT signed with HMAC-SHA256, lives <c>Jwt:AccessTokenMinutes</c> (15). Carries user id, name, role, jti.
/// - refresh token = 64 random bytes, lives <c>Jwt:RefreshTokenDays</c> (7). Only its SHA-256 hash is stored.
/// - every refresh rotates: the used refresh token is revoked and a new one issued in the same "family" (session).
/// - presenting an already-used refresh token revokes the whole family (theft detection).
/// </summary>
public class TokenService : ITokenService
{
    private readonly IConfiguration _config;
    private readonly IFirestoreService _store;

    public TokenService(IConfiguration config, IFirestoreService store)
    {
        _config = config;
        _store = store;
    }

    private TimeSpan AccessLifetime => TimeSpan.FromMinutes(_config.GetValue("Jwt:AccessTokenMinutes", 15));
    private TimeSpan RefreshLifetime => TimeSpan.FromDays(_config.GetValue("Jwt:RefreshTokenDays", 7));

    public string GenerateToken(UserProfile user) => CreateAccessToken(user, out _);

    public Task<AuthResponseDto> IssueAsync(UserProfile user) => IssueAsync(user, familyId: Guid.NewGuid().ToString("N"));

    public async Task<(AuthResponseDto? Tokens, RefreshError Error)> RefreshAsync(string refreshToken)
    {
        var hash = Hash(refreshToken);
        var stored = await _store.GetRefreshTokenAsync(hash);
        if (stored == null) return (null, RefreshError.Unknown);

        if (stored.RevokedAt != null)
        {
            // Used before: someone else may hold a copy. End that whole session.
            await _store.RevokeRefreshFamilyAsync(stored.FamilyId);
            return (null, RefreshError.Reused);
        }

        if (stored.ExpiresAt <= DateTime.UtcNow) return (null, RefreshError.Expired);

        // Fresh data on every refresh: a role change or a deleted account takes effect here.
        var user = await _store.GetUserByIdAsync(stored.UserId);
        if (user == null)
        {
            await _store.RevokeRefreshFamilyAsync(stored.FamilyId);
            return (null, RefreshError.UserGone);
        }

        var tokens = await IssueAsync(user, stored.FamilyId);
        await _store.RevokeRefreshTokenAsync(hash, replacedBy: Hash(tokens.RefreshToken));
        return (tokens, RefreshError.None);
    }

    public async Task LogoutAsync(string? jti, DateTime accessExpiresAt, string userId, string? refreshToken)
    {
        if (!string.IsNullOrEmpty(jti))
        {
            await _store.RevokeAccessTokenAsync(jti, accessExpiresAt);
        }

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var stored = await _store.GetRefreshTokenAsync(Hash(refreshToken));
            // Only your own session can be ended this way.
            if (stored != null && stored.UserId == userId)
            {
                await _store.RevokeRefreshFamilyAsync(stored.FamilyId);
            }
        }
    }

    private async Task<AuthResponseDto> IssueAsync(UserProfile user, string familyId)
    {
        var access = CreateAccessToken(user, out var accessExpires);

        var refresh = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
        var refreshExpires = DateTime.UtcNow.Add(RefreshLifetime);
        await _store.StoreRefreshTokenAsync(Hash(refresh), user.Id, familyId, refreshExpires);

        return new AuthResponseDto
        {
            Token = access,
            AccessTokenExpiresAt = accessExpires,
            RefreshToken = refresh,
            RefreshTokenExpiresAt = refreshExpires,
            User = user
        };
    }

    private string CreateAccessToken(UserProfile user, out DateTime expires)
    {
        var keyString = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        expires = DateTime.UtcNow.Add(AccessLifetime);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "SalePliusApi",
            audience: _config["Jwt:Audience"] ?? "SalePliusClient",
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Refresh tokens are stored only as SHA-256 hashes, like passwords.</summary>
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
