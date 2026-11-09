using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using SalePlius.Shared.DTOs;
using SalePlius.Shared.Models;

namespace SalePlius.Client.Services;

/// <summary>
/// Who is logged in, plus their tokens: a short-lived access token (sent with every API call)
/// and a long-lived refresh token (used only to get a new access token). Kept in localStorage so a
/// page reload stays logged in. The user counts as logged in while the refresh token is valid,
/// even if the access token has already expired: <see cref="TokenRefresher"/> renews it on the next call.
/// </summary>
public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private const string StorageKey = "saleplius_session";

    private readonly IJSRuntime _js;
    private Session? _session;
    private bool _initialized;

    public CustomAuthStateProvider(IJSRuntime js)
    {
        _js = js;
    }

    /// <summary>What is kept in localStorage.</summary>
    private record Session(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken, DateTime RefreshTokenExpiresAt, UserProfile User);

    public UserProfile? CurrentUser => _session?.User;
    public string? Token => _session?.AccessToken;
    public string? RefreshToken => _session?.RefreshToken;

    /// <summary>True when the access token is gone or expires within the next minute.</summary>
    public bool AccessTokenExpiresSoon => _session == null || _session.AccessTokenExpiresAt <= DateTime.UtcNow.AddMinutes(1);

    public bool IsAuthenticated => _session != null && _session.RefreshTokenExpiresAt > DateTime.UtcNow;
    public bool IsAdmin => IsAuthenticated && AppRoles.IsAdmin(CurrentUser?.Role);
    public bool CanCreateContent => IsAuthenticated && AppRoles.CanCreateContent(CurrentUser?.Role);

    /// <summary>True when the logged-in user created the item.</summary>
    public bool Owns(string? ownerId) => IsAuthenticated && !string.IsNullOrEmpty(ownerId) && CurrentUser!.Id == ownerId;

    /// <summary>True when the logged-in user may edit/delete the item (owner or admin).</summary>
    public bool CanModify(string? ownerId) => Owns(ownerId) || IsAdmin;

    public async Task<string?> GetTokenAsync()
    {
        if (!_initialized) await GetAuthenticationStateAsync();
        return IsAuthenticated ? _session!.AccessToken : null;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_initialized)
        {
            _initialized = true;
            try
            {
                var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
                _session = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<Session>(json);
            }
            catch
            {
                _session = null;
            }
        }

        return IsAuthenticated ? CreateAuthState(_session!.User) : Anonymous;
    }

    /// <summary>After login, register or a token refresh: store the new tokens (and the user as the server sees them now).</summary>
    public async Task SetTokensAsync(AuthResponseDto tokens)
    {
        var roleChanged = _session?.User.Role != tokens.User.Role || _session?.User.Id != tokens.User.Id;
        _session = new Session(tokens.Token, tokens.AccessTokenExpiresAt, tokens.RefreshToken, tokens.RefreshTokenExpiresAt, tokens.User);
        _initialized = true;

        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, JsonSerializer.Serialize(_session));
        }
        catch
        {
        }

        if (roleChanged) NotifyAuthenticationStateChanged(Task.FromResult(CreateAuthState(tokens.User)));
    }

    public Task LoginAsync(AuthResponseDto tokens) => SetTokensAsync(tokens);

    /// <summary>Forgets the session locally. (The server side is ended by ApiService.LogoutAsync first.)</summary>
    public async Task LogoutAsync()
    {
        _session = null;
        _initialized = true;

        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
            // Keys used by the earlier version of the app.
            await _js.InvokeVoidAsync("localStorage.removeItem", "saleplius_token");
            await _js.InvokeVoidAsync("localStorage.removeItem", "saleplius_user");
        }
        catch
        {
        }

        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous));
    }

    private static AuthenticationState Anonymous => new(new ClaimsPrincipal(new ClaimsIdentity()));

    private static AuthenticationState CreateAuthState(UserProfile user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };

        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt")));
    }
}
