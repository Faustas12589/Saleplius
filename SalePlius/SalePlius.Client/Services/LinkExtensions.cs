using SalePlius.Shared.Models;

namespace SalePlius.Client.Services;

/// <summary>
/// Reading hypermedia links from API responses. Instead of the client deciding "can I delete this?"
/// or building "/api/workouts?page=3" itself, it checks which links the API sent.
/// </summary>
public static class LinkExtensions
{
    /// <summary>True when the API offered this action/relation, e.g. links.Has("delete").</summary>
    public static bool Has(this IEnumerable<Link>? links, string rel) =>
        links?.Any(l => l.Rel == rel) == true;

    /// <summary>The URL for a relation, or null if the API didn't send it, e.g. links.Href("next").</summary>
    public static string? Href(this IEnumerable<Link>? links, string rel) =>
        links?.FirstOrDefault(l => l.Rel == rel)?.Href;
}
