using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Xml.Serialization;
using SalePlius.Shared.Models;

namespace SalePlius.Shared.DTOs;

public class CreateWorkoutDto
{
    [Required(ErrorMessage = "Treniruotės pavadinimas yra privalomas")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Pavadinimas turi būti nuo 2 iki 100 simbolių")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Aprašymas yra privalomas")]
    [StringLength(2000, ErrorMessage = "Tekstas negali būti ilgesnis nei 2000 simbolių")]
    public string Description { get; set; } = string.Empty;

    [RegularExpression(Difficulties.Pattern, ErrorMessage = Difficulties.Message)]
    public string Difficulty { get; set; } = "Intermediate";

    public List<string> TargetMuscleGroups { get; set; } = new();

    // Set by the server from the login token. Ignored if sent in the request body (JSON or XML).
    [JsonIgnore, XmlIgnore]
    public string CreatedByUserId { get; set; } = string.Empty;
    [JsonIgnore, XmlIgnore]
    public string CreatedByUserName { get; set; } = string.Empty;

    public List<WorkoutExerciseItem> Exercises { get; set; } = new();

    /// <summary>Days of the program week (1–7). Required when creating a workout inside a program.</summary>
    public List<int> Days { get; set; } = new();

    /// <summary>Set by the server from the URL (POST /api/programs/{id}/workouts); ignored on POST /api/workouts.</summary>
    public string ProgramId { get; set; } = string.Empty;
}
