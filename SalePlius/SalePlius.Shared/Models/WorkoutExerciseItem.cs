using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SalePlius.Shared.Models;

/// <summary>
/// One exercise inside a workout (the third level: Program → Workout → WorkoutExerciseItem).
/// Points to an exercise in the library and adds sets, reps and notes.
/// </summary>
public class WorkoutExerciseItem
{
    /// <summary>Unique within its workout; used in /api/programs/{p}/workouts/{w}/exercises/{id}.</summary>
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "Pratimo ID yra privalomas")]
    public string ExerciseId { get; set; } = string.Empty;
    public string ExerciseName { get; set; } = string.Empty;
    [Range(1, 50, ErrorMessage = "Serijų skaičius turi būti tarp 1 ir 50")]
    public int Sets { get; set; } = 3;
    [Range(1, 500, ErrorMessage = "Pakartojimų skaičius turi būti tarp 1 ir 500")]
    public int Reps { get; set; } = 10;
    [StringLength(200, ErrorMessage = "Pastaba negali būti ilgesnė nei 200 simbolių")]
    public string Notes { get; set; } = string.Empty;

    /// <summary>Hypermedia links, only filled on the nested exercise endpoints.</summary>
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Link>? Links { get; set; }
}
