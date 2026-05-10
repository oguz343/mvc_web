using System.ComponentModel.DataAnnotations;

namespace mvc_web.Models;

public class CreatePasswordViewModel
{
    public string UserId { get; set; } = "";
    public string Role { get; set; } = "";
    public string Name { get; set; } = "";
    public string Number { get; set; } = "";

    [Required(ErrorMessage = "Yeni şifre boş bırakılamaz.")]
    [MinLength(4, ErrorMessage = "Şifre en az 4 karakter olmalıdır.")]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "Şifre tekrarı boş bırakılamaz.")]
    public string RepeatPassword { get; set; } = "";
}