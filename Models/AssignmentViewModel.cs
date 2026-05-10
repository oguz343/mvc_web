using System.ComponentModel.DataAnnotations;

namespace mvc_web.Models;

public class AssignmentViewModel
{
    public string Id { get; set; } = "";

    [Required(ErrorMessage = "Ödev başlığı boş bırakılamaz.")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Ders seçilmelidir.")]
    public string LessonId { get; set; } = "";

    public string Lesson { get; set; } = "";
    public string ClassName { get; set; } = "";

    public string TeacherId { get; set; } = "";
    public string TeacherName { get; set; } = "";
    public string TeacherBranch { get; set; } = "";

    [Required(ErrorMessage = "Son teslim tarihi boş bırakılamaz.")]
    public string DueDate { get; set; } = "";

    public string Type { get; set; } = "Metin";
    public string Status { get; set; } = "Aktif";
    public string Description { get; set; } = "";
}

public class TeacherLessonOptionViewModel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ClassName { get; set; } = "";
}