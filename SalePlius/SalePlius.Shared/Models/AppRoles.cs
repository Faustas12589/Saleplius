namespace SalePlius.Shared.Models;

public static class AppRoles
{
    public const string User = "User";
    public const string Admin = "Admin";

    public static bool CanCreateContent(string? role) =>
        string.Equals(role, User, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, Admin, StringComparison.OrdinalIgnoreCase);

    public static bool IsAdmin(string? role) =>
        string.Equals(role, Admin, StringComparison.OrdinalIgnoreCase);
}
