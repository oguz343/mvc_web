using System.ComponentModel.DataAnnotations;

namespace mvc_web.Models;

public class LessonViewModel
{
    public string Id { get; set; } = "";

    [Required(ErrorMessage = "Ders adı boş bırakılamaz.")]
    public string Name { get; set; } = "Matematik";

    [Required(ErrorMessage = "Sınıf seçilmelidir.")]
    public string ClassName { get; set; } = "";

    [Required(ErrorMessage = "Öğretmen seçilmelidir.")]
    public string TeacherId { get; set; } = "";

    public string TeacherName { get; set; } = "";

    public string TeacherBranch { get; set; } = "";
}