using System.Net;
using System.Net.Http.Headers;

namespace SalePlius.Client.Services;

/// <summary>
/// Adds the access token to every API request and keeps it fresh:
/// 1. before sending, a token that expires within a minute is renewed first;
/// 2. if the API still answers 401 (e.g. revoked), the token is renewed once and the request repeated.
/// If renewing fails the user is logged out (TokenRefresher does that).
/// </summary>
public class AuthHeaderHandler(CustomAuthStateProvider auth, TokenRefresher refresher) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await auth.GetTokenAsync();
        if (token != null && auth.AccessTokenExpiresSoon)
        {
            token = await refresher.RefreshAsync(token);
        }

        // Keep the body so the request can be sent a second time.
        var body = request.Content == null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentHeaders = request.Content?.Headers.ToList();

        Authorize(request, token);
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized || token == null)
        {
            return response;
        }

        var renewed = await refresher.RefreshAsync(token);
        if (renewed == null || renewed == token)
        {
            return response;
        }

        var retry = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var h in request.Headers.Where(h => h.Key != "Authorization")) retry.Headers.TryAddWithoutValidation(h.Key, h.Value);
        if (body != null)
        {
            retry.Content = new ByteArrayContent(body);
            foreach (var h in contentHeaders!) retry.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
        }

        Authorize(retry, renewed);
        response.Dispose();
        return await base.SendAsync(retry, cancellationToken);
    }

    private static void Authorize(HttpRequestMessage request, string? token)
    {
        request.Headers.Authorization = token == null ? null : new AuthenticationHeaderValue("Bearer", token);
    }
}
