using System.ComponentModel.DataAnnotations;

namespace SalePlius.Shared.DTOs;

public class LoginDto
{
    [Required(ErrorMessage = "Vartotojo vardas arba el. paštas yra privalomas")]
    public string UsernameOrEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Slaptažodis yra privalomas")]
    public string Password { get; set; } = string.Empty;
}
