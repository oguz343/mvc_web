using System.ComponentModel.DataAnnotations;

namespace mvc_web.Models;

public class UserViewModel
{
    public string Id { get; set; } = "";

    [Required(ErrorMessage = "Ad soyad boş bırakılamaz.")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Rol seçilmelidir.")]
    public string Role { get; set; } = "Öğrenci";

    [Required(ErrorMessage = "Numara boş bırakılamaz.")]
    public string SchoolNo { get; set; } = "";

    [Required(ErrorMessage = "T.C. kimlik numarası boş bırakılamaz.")]
    [StringLength(11, MinimumLength = 11, ErrorMessage = "T.C. kimlik numarası 11 haneli olmalıdır.")]
    public string Tc { get; set; } = "";

    public string Phone { get; set; } = "";

    public string ClassName { get; set; } = "";

    public string LinkedStudentNo { get; set; } = "";

    public string Branch { get; set; } = "";

    public string ActivationCode { get; set; } = "";

    public bool MustChangePassword { get; set; } = true;

    public string Detail { get; set; } = "";
}