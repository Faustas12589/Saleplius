using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace SalePlius.Shared.DTOs;

public class CreateRatingDto
{
    // Set by the server from the login token. Ignored if sent in the request body (JSON or XML).
    [JsonIgnore, XmlIgnore]
    public string UserId { get; set; } = string.Empty;
    [JsonIgnore, XmlIgnore]
    public string UserName { get; set; } = string.Empty;

    [Range(1, 5, ErrorMessage = "Įvertinimas turi būti nuo 1 iki 5 balų")]
    public int Score { get; set; } = 5;

    [StringLength(500, ErrorMessage = "Atsiliepimas negali būti ilgesnis nei 500 simbolių")]
    public string Review { get; set; } = string.Empty;
}
