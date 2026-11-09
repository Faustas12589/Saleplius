using System.Text.Json.Serialization;

namespace SalePlius.Shared.Models;

public class Workout
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Difficulty { get; set; } = "Intermediate"; // "Beginner", "Intermediate", "Advanced"
    public List<string> TargetMuscleGroups { get; set; } = new();
    public string CreatedByUserId { get; set; } = string.Empty;
    public string CreatedByUserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<WorkoutExerciseItem> Exercises { get; set; } = new();
    public double AverageRating { get; set; } = 0.0;
    public int RatingsCount { get; set; } = 0;

    /// <summary>The program this workout belongs to ("" = standalone). Program → Workout is one-to-many.</summary>
    public string ProgramId { get; set; } = string.Empty;

    /// <summary>Days of the program week (1 = Monday … 7 = Sunday) this workout is done on.</summary>
    public List<int> Days { get; set; } = new();

    /// <summary>Hypermedia links, filled in by the API per request (not stored).</summary>
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Link>? Links { get; set; }
}
