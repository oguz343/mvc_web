using System.ComponentModel.DataAnnotations;

namespace mvc_web.Models;

public class ClassViewModel
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Sınıf seviyesi seçilmelidir.")]
    public string Grade { get; set; } = "9";

    [Required(ErrorMessage = "Şube seçilmelidir.")]
    public string Branch { get; set; } = "A";

    [Required(ErrorMessage = "Sınıf öğretmeni seçilmelidir.")]
    public string TeacherId { get; set; } = "";

    public string Teacher { get; set; } = "";

    public string TeacherBranch { get; set; } = "";

    [Range(1, 999, ErrorMessage = "Kapasite 1 ile 999 arasında olmalıdır.")]
    public int Capacity { get; set; } = 30;

    [Range(0, 999, ErrorMessage = "Öğrenci sayısı 0 ile 999 arasında olmalıdır.")]
    public int StudentCount { get; set; } = 0;
}

public class TeacherOptionViewModel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Branch { get; set; } = "";
}