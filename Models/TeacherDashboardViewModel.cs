namespace mvc_web.Models;

public class TeacherDashboardViewModel
{
    public string TeacherId { get; set; } = "";
    public string TeacherName { get; set; } = "";
    public string TeacherNumber { get; set; } = "";
    public string Branch { get; set; } = "";

    public int LessonCount { get; set; }
    public int AssignmentCount { get; set; }
    public int SubmissionCount { get; set; }
    public int GradedSubmissionCount { get; set; }

    public List<LessonViewModel> Lessons { get; set; } = new();
    public List<AssignmentViewModel> LatestAssignments { get; set; } = new();
    public List<SubmissionViewModel> LatestSubmissions { get; set; } = new();
}