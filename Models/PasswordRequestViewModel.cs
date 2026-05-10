using System.ComponentModel.DataAnnotations;

namespace mvc_web.Models;

public class PasswordRequestViewModel
{
    public string Id { get; set; } = "";

    [Required(ErrorMessage = "Ad soyad boş bırakılamaz.")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Numara boş bırakılamaz.")]
    public string Number { get; set; } = "";

    [Required(ErrorMessage = "Rol seçilmelidir.")]
    public string Role { get; set; } = "Öğrenci";

    public string Status { get; set; } = "Bekliyor";

    public string Note { get; set; } = "";
}