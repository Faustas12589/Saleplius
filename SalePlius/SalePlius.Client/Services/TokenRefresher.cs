using System.Net.Http.Json;
using SalePlius.Shared.DTOs;

namespace SalePlius.Client.Services;

/// <summary>
/// Gets a new access token with the refresh token. One refresh at a time: the API rotates refresh tokens and
/// treats a reused one as theft, so two parallel refreshes with the same token would end the session.
/// Uses its own HttpClient ("auth") without AuthHeaderHandler, so refreshing never triggers another refresh.
/// </summary>
public class TokenRefresher(IHttpClientFactory httpFactory, CustomAuthStateProvider auth)
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Refreshes unless someone else already did while we waited (the token changed from
    /// <paramref name="staleToken"/>). Returns the access token to use, or null when the session is over.
    /// </summary>
    public async Task<string?> RefreshAsync(string? staleToken)
    {
        await _lock.WaitAsync();
        try
        {
            if (!auth.IsAuthenticated || auth.RefreshToken == null) return null;
            if (auth.Token != staleToken && !auth.AccessTokenExpiresSoon) return auth.Token; // already renewed

            var res = await httpFactory.CreateClient("auth")
                .PostAsJsonAsync("api/auth/refresh", new RefreshRequestDto { RefreshToken = auth.RefreshToken });

            if (!res.IsSuccessStatusCode)
            {
                await auth.LogoutAsync();   // refresh token expired, reused or revoked: log in again
                return null;
            }

            var tokens = await res.Content.ReadFromJsonAsync<AuthResponseDto>();
            if (tokens == null) return null;

            await auth.SetTokensAsync(tokens);
            return tokens.Token;
        }
        catch
        {
            return auth.Token;   // network trouble: keep the session, try again on the next call
        }
        finally
        {
            _lock.Release();
        }
    }
}
