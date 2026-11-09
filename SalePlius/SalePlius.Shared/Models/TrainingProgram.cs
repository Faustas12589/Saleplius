using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace SalePlius.Shared.Models;

/// <summary>
/// A multi-week training program: a weekly schedule of workouts (Program → Workouts → Exercises).
/// Named TrainingProgram so it does not clash with the app's Program class.
/// </summary>
public class TrainingProgram
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Goal { get; set; } = "Bendras"; // e.g. "Jėga", "Masė", "Ištvermė", "Bendras"
    public string Difficulty { get; set; } = "Intermediate"; // "Beginner", "Intermediate", "Advanced"
    public int DurationWeeks { get; set; } = 4;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string CreatedByUserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The weekly schedule. The same week repeats for DurationWeeks.</summary>
    public List<ProgramWorkoutItem> Workouts { get; set; } = new();

    /// <summary>Every muscle group the program's workouts target (read-only, built from the schedule).</summary>
    public List<string> MuscleGroups => Workouts
        .SelectMany(w => w.TargetMuscleGroups)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    /// <summary>Hypermedia links, filled in by the API per request (not stored).</summary>
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Link>? Links { get; set; }
}

/// <summary>One workout placed on a day of the program's week.</summary>
public class ProgramWorkoutItem
{
    [Required(ErrorMessage = "Treniruotės ID yra privalomas")]
    public string WorkoutId { get; set; } = string.Empty;

    /// <summary>Filled in by the server from the workout itself.</summary>
    public string WorkoutTitle { get; set; } = string.Empty;

    /// <summary>Filled in by the server from the workout itself.</summary>
    public List<string> TargetMuscleGroups { get; set; } = new();

    /// <summary>Day of the week, 1 = Monday … 7 = Sunday.</summary>
    [Range(1, 7, ErrorMessage = "Diena turi būti nuo 1 (pirmadienis) iki 7 (sekmadienis)")]
    public int Day { get; set; } = 1;

    [StringLength(200, ErrorMessage = "Pastaba negali būti ilgesnė nei 200 simbolių")]
    public string Notes { get; set; } = string.Empty;
}
