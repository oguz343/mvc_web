using System.ComponentModel.DataAnnotations;

namespace mvc_web.Models;

public class GradeSubmissionViewModel
{
    public string Id { get; set; } = "";

    public string AssignmentTitle { get; set; } = "";
    public string StudentNo { get; set; } = "";
    public string Answer { get; set; } = "";
    public string Link { get; set; } = "";

    [Required(ErrorMessage = "Not alanı boş bırakılamaz.")]
    public string Grade { get; set; } = "";

    public string Feedback { get; set; } = "";
}