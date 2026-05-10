using System.ComponentModel.DataAnnotations;

namespace mvc_web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Rol seçilmelidir.")]
    public string Role { get; set; } = "Öğrenci";

    [Required(ErrorMessage = "Numara boş bırakılamaz.")]
    public string Number { get; set; } = "";

    [Required(ErrorMessage = "Şifre veya aktivasyon kodu boş bırakılamaz.")]
    public string Password { get; set; } = "";
}