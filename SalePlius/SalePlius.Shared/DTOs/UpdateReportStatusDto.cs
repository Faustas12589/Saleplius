using System.ComponentModel.DataAnnotations;

namespace SalePlius.Shared.DTOs;

public class UpdateReportStatusDto
{
    [Required(ErrorMessage = "Statusas yra privalomas")]
    [RegularExpression("^(Pending|Reviewed|Resolved|Dismissed)$", ErrorMessage = "Statusas gali būti tik: Pending, Reviewed, Resolved arba Dismissed")]
    public string Status { get; set; } = string.Empty;
}
