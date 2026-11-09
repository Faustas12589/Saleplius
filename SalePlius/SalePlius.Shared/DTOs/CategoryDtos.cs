using System.ComponentModel.DataAnnotations;
using SalePlius.Shared.Models;

namespace SalePlius.Shared.DTOs;

public class CreateCategoryDto
{
    [Required(ErrorMessage = "Kategorijos tipas yra privalomas")]
    [RegularExpression(CategoryTypes.Pattern, ErrorMessage = "Tipas gali būti tik: MuscleGroup, Equipment arba Goal")]
    public string Type { get; set; } = CategoryTypes.MuscleGroup;

    [Required(ErrorMessage = "Pavadinimas yra privalomas")]
    [StringLength(40, MinimumLength = 2, ErrorMessage = "Pavadinimas turi būti nuo 2 iki 40 simbolių")]
    public string Name { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "Aprašymas negali būti ilgesnis nei 200 simbolių")]
    public string Description { get; set; } = string.Empty;
}

/// <summary>The type is fixed once created; only the name and description change.</summary>
public class UpdateCategoryDto
{
    [Required(ErrorMessage = "Pavadinimas yra privalomas")]
    [StringLength(40, MinimumLength = 2, ErrorMessage = "Pavadinimas turi būti nuo 2 iki 40 simbolių")]
    public string Name { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "Aprašymas negali būti ilgesnis nei 200 simbolių")]
    public string Description { get; set; } = string.Empty;
}
