using System.Security.Claims;
using SalePlius.Shared.Models;

namespace SalePlus.Api.Services;

public static class OwnershipExtensions
{
    /// <summary>
    /// True when the current user is the owner of the resource or an administrator.
    /// </summary>
    public static bool CanModify(this ClaimsPrincipal user, string? ownerId)
    {
        if (user.IsInRole(AppRoles.Admin)) return true;

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(userId) && userId == ownerId;
    }

    /// <summary>
    /// Id of the logged-in user, always taken from the token (never from the request body).
    /// Only call it on endpoints that require login.
    /// </summary>
    public static string GetUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("No user id in token.");

    /// <summary>Username of the logged-in user, from the token.</summary>
    public static string GetUserName(this ClaimsPrincipal user) => user.Identity?.Name ?? string.Empty;
}
