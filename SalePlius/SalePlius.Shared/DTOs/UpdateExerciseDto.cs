using System.ComponentModel.DataAnnotations;
using SalePlius.Shared.Models;

namespace SalePlius.Shared.DTOs;

public class UpdateExerciseDto
{
    [Required(ErrorMessage = "Pratimo pavadinimas yra privalomas")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Pavadinimas turi būti nuo 2 iki 100 simbolių")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Aprašymas yra privalomas")]
    [StringLength(2000, ErrorMessage = "Tekstas negali būti ilgesnis nei 2000 simbolių")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Pagrindinė raumenų grupė yra privaloma")]
    public string MuscleGroup { get; set; } = string.Empty;

    public List<string> SecondaryMuscleGroups { get; set; } = new();

    [RegularExpression(Difficulties.Pattern, ErrorMessage = Difficulties.Message)]
    public string Difficulty { get; set; } = "Intermediate";

    [StringLength(2000, ErrorMessage = "Tekstas negali būti ilgesnis nei 2000 simbolių")]
    public string Instructions { get; set; } = string.Empty;

    /// <summary>Must be an existing Equipment category (checked by the API).</summary>
    [Required(ErrorMessage = "Įranga yra privaloma")]
    public string Equipment { get; set; } = string.Empty;
}
