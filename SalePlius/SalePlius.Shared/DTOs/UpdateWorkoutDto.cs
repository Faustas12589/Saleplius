using System.ComponentModel.DataAnnotations;
using SalePlius.Shared.Models;

namespace SalePlius.Shared.DTOs;

public class UpdateWorkoutDto
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

    /// <summary>Replaces the exercise list when given; null keeps the current exercises.</summary>
    public List<WorkoutExerciseItem>? Exercises { get; set; }

    /// <summary>Program week days (1–7); null keeps the current days.</summary>
    public List<int>? Days { get; set; }
}
