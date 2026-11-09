using System.ComponentModel.DataAnnotations;

namespace SalePlius.Shared.DTOs;

public class CreateUserDto
{
    [Required(ErrorMessage = "Vartotojo vardas yra privalomas")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Vartotojo vardas turi būti nuo 3 iki 50 simbolių")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Slaptažodis yra privalomas")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Slaptažodis turi būti bent 8 simbolių")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "El. paštas yra privalomas")]
    [EmailAddress(ErrorMessage = "Neteisingas el. pašto formatas")]
    public string Email { get; set; } = string.Empty;

    [RegularExpression("^(User|Admin)$", ErrorMessage = "Rolė gali būti tik User arba Admin")]
    public string Role { get; set; } = "User";

    [StringLength(500, ErrorMessage = "Aprašymas negali būti ilgesnis nei 500 simbolių")]
    public string Bio { get; set; } = string.Empty;
}
