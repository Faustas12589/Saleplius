using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Xml.Serialization;
using SalePlius.Shared.Models;

namespace SalePlius.Shared.DTOs;

public class CreateProgramDto
{
    [Required(ErrorMessage = "Programos pavadinimas yra privalomas")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Pavadinimas turi būti nuo 2 iki 100 simbolių")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Aprašymas yra privalomas")]
    [StringLength(2000, ErrorMessage = "Tekstas negali būti ilgesnis nei 2000 simbolių")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Must be an existing Goal category (checked by the API).</summary>
    [Required(ErrorMessage = "Tikslas yra privalomas")]
    public string Goal { get; set; } = "Bendras";

    [RegularExpression("^(Beginner|Intermediate|Advanced)$", ErrorMessage = "Sudėtingumas: Beginner, Intermediate arba Advanced")]
    public string Difficulty { get; set; } = "Intermediate";

    [Range(1, 52, ErrorMessage = "Trukmė turi būti nuo 1 iki 52 savaičių")]
    public int DurationWeeks { get; set; } = 4;

    public List<ProgramWorkoutItem> Workouts { get; set; } = new();

    // Set by the server from the login token. Ignored if sent in the request body (JSON or XML).
    [JsonIgnore, XmlIgnore]
    public string CreatedByUserId { get; set; } = string.Empty;
    [JsonIgnore, XmlIgnore]
    public string CreatedByUserName { get; set; } = string.Empty;
}

public class UpdateProgramDto
{
    [Required(ErrorMessage = "Programos pavadinimas yra privalomas")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Pavadinimas turi būti nuo 2 iki 100 simbolių")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Aprašymas yra privalomas")]
    [StringLength(2000, ErrorMessage = "Tekstas negali būti ilgesnis nei 2000 simbolių")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Must be an existing Goal category (checked by the API).</summary>
    [Required(ErrorMessage = "Tikslas yra privalomas")]
    public string Goal { get; set; } = "Bendras";

    [RegularExpression("^(Beginner|Intermediate|Advanced)$", ErrorMessage = "Sudėtingumas: Beginner, Intermediate arba Advanced")]
    public string Difficulty { get; set; } = "Intermediate";

    [Range(1, 52, ErrorMessage = "Trukmė turi būti nuo 1 iki 52 savaičių")]
    public int DurationWeeks { get; set; } = 4;

    public List<ProgramWorkoutItem> Workouts { get; set; } = new();
}
