using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace SalePlius.Shared.DTOs;

public class CreateReportDto
{
    [Required(ErrorMessage = "Objekto tipas yra privalomas")]
    [RegularExpression("^(Program|Exercise|Workout|Comment|User)$", ErrorMessage = "TargetType turi būti: Program, Exercise, Workout, Comment arba User")]
    public string TargetType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Objekto ID yra privalomas")]
    public string TargetId { get; set; } = string.Empty;
    [JsonIgnore, XmlIgnore]
    public string ReportedByUserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nusižengimo priežastis yra privaloma")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Priežastis turi būti nuo 5 iki 500 simbolių")]
    public string Reason { get; set; } = string.Empty;
}
