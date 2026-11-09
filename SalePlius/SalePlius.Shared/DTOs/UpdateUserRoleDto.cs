using System.ComponentModel.DataAnnotations;

namespace SalePlius.Shared.DTOs;

public class UpdateUserRoleDto
{
    [Required(ErrorMessage = "Rolė yra privaloma")]
    [RegularExpression("^(User|Admin)$", ErrorMessage = "Rolė gali būti tik User arba Admin")]
    public string Role { get; set; } = string.Empty;
}
