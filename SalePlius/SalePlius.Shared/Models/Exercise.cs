using System.Text.Json.Serialization;

namespace SalePlius.Shared.Models;

public class Exercise
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string MuscleGroup { get; set; } = string.Empty; // e.g. "Chest", "Back", "Legs", "Shoulders", "Arms", "Core"
    public List<string> SecondaryMuscleGroups { get; set; } = new();
    public string Difficulty { get; set; } = "Intermediate"; // "Beginner", "Intermediate", "Advanced"
    public string Instructions { get; set; } = string.Empty;
    public string Equipment { get; set; } = "Bodyweight"; // "Barbell", "Dumbbell", "Bodyweight", "Machine", "Cable"
    public string CreatedByUserId { get; set; } = string.Empty;
    public string CreatedByUserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public double Rating { get; set; } = 0.0;
    public int RatingCount { get; set; } = 0;

    /// <summary>Hypermedia links, filled in by the API per request (not stored).</summary>
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Link>? Links { get; set; }
}
