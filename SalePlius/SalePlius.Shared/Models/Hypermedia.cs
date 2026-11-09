using System.Text.Json.Serialization;

namespace SalePlius.Shared.Models;

/// <summary>
/// A hypermedia link: "you can do <see cref="Method"/> on <see cref="Href"/>, and it means <see cref="Rel"/>".
/// The API only includes links the current user is actually allowed to follow.
/// </summary>
public class Link
{
    public Link() { }

    public Link(string rel, string href, string method = "GET")
    {
        Rel = rel;
        Href = href;
        Method = method;
    }

    /// <summary>What the link means, e.g. "self", "update", "next".</summary>
    public string Rel { get; set; } = string.Empty;

    /// <summary>Where it points, relative to the API host.</summary>
    public string Href { get; set; } = string.Empty;

    /// <summary>HTTP method to use.</summary>
    public string Method { get; set; } = "GET";
}

/// <summary>Page numbers and links of a <see cref="PagedResult{T}"/>, whatever the item type.</summary>
public interface IPageInfo
{
    int Page { get; }
    int TotalPages { get; }
    int TotalCount { get; }
    List<Link> Links { get; }
}

/// <summary>One page of a list, plus how to reach the other pages.</summary>
public class PagedResult<T> : IPageInfo
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }

    /// <summary>self, first, last, and prev/next when they exist (plus "create" when allowed).</summary>
    [JsonPropertyName("_links")]
    public List<Link> Links { get; set; } = new();
}
