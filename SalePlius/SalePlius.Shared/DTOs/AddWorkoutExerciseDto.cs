using System.ComponentModel.DataAnnotations;

namespace SalePlius.Shared.DTOs;

public class AddWorkoutExerciseDto
{
    [Required(ErrorMessage = "Pratimo ID yra privalomas")]
    public string ExerciseId { get; set; } = string.Empty;

    public string ExerciseName { get; set; } = string.Empty;

    [Range(1, 50, ErrorMessage = "Serijų skaičius turi būti tarp 1 ir 50")]
    public int Sets { get; set; } = 3;

    [Range(1, 500, ErrorMessage = "Pakartojimų skaičius turi būti tarp 1 ir 500")]
    public int Reps { get; set; } = 10;

    [StringLength(200, ErrorMessage = "Pastaba negali būti ilgesnė nei 200 simbolių")]
    public string Notes { get; set; } = string.Empty;
}
