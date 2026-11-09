using System.Text.Json.Serialization;

namespace SalePlius.Shared.Models;

public class ViolationReport
{
    public string Id { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty; // "Exercise", "Workout", "Comment", "User"
    public string TargetId { get; set; } = string.Empty;
    public string ReportedByUserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // "Pending", "Reviewed", "Resolved", "Dismissed"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }

    /// <summary>Hypermedia links, filled in by the API per request (not stored).</summary>
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Link>? Links { get; set; }
}
