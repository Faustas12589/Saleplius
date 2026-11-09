using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace SalePlius.Shared.DTOs;

public class CreateCommentDto
{
    // Set by the server from the login token. Ignored if sent in the request body (JSON or XML).
    [JsonIgnore, XmlIgnore]
    public string UserId { get; set; } = string.Empty;
    [JsonIgnore, XmlIgnore]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Komentaro tekstas yra privalomas")]
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "Komentaro tekstas turi būti nuo 1 iki 1000 simbolių")]
    public string Content { get; set; } = string.Empty;
}
