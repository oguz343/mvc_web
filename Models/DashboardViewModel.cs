namespace mvc_web.Models;

public class DashboardViewModel
{
    public int StudentCount { get; set; }
    public int TeacherCount { get; set; }
    public int ParentCount { get; set; }
    public int ClassCount { get; set; }
    public int LessonCount { get; set; }
    public int SubmissionCount { get; set; }
    public int AnnouncementCount { get; set; }
    public int PasswordRequestCount { get; set; }

    public List<DashboardUserItem> LatestUsers { get; set; } = new();
    public List<DashboardSubmissionItem> LatestSubmissions { get; set; } = new();
}

public class DashboardUserItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string Number { get; set; } = "";
    public string Detail { get; set; } = "";
}

public class DashboardSubmissionItem
{
    public string Id { get; set; } = "";
    public string AssignmentTitle { get; set; } = "";
    public string StudentNo { get; set; } = "";
    public string Lesson { get; set; } = "";
    public string Status { get; set; } = "";
    public string Grade { get; set; } = "";
}