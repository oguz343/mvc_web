namespace mvc_web.Models;

public class SubmissionViewModel
{
    public string Id { get; set; } = "";

    public string AssignmentId { get; set; } = "";
    public string AssignmentTitle { get; set; } = "";

    public string Lesson { get; set; } = "";
    public string ClassName { get; set; } = "";

    public string TeacherId { get; set; } = "";
    public string TeacherName { get; set; } = "";

    public string StudentNo { get; set; } = "";

    public string Answer { get; set; } = "";
    public string Link { get; set; } = "";

    public string Status { get; set; } = "Teslim Edildi";
    public string Grade { get; set; } = "";
    public string Feedback { get; set; } = "";
}