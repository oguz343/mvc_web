namespace mvc_web.Models;

public class ReportFilterViewModel
{
    public string ClassName { get; set; } = "";
    public string AssignmentId { get; set; } = "";
    public string Status { get; set; } = "all";
    public string FromDate { get; set; } = "";
    public string ToDate { get; set; } = "";
    public string Sort { get; set; } = "date_desc";
}

public class ReportOptionViewModel
{
    public string Value { get; set; } = "";
    public string Text { get; set; } = "";
}

public class HomeworkReportRowViewModel
{
    public string StudentName { get; set; } = "";
    public string StudentNo { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string AssignmentId { get; set; } = "";
    public string AssignmentTitle { get; set; } = "";
    public string LessonName { get; set; } = "";
    public string TeacherName { get; set; } = "";
    public DateTime? DueDate { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public bool IsSubmitted { get; set; }
    public string Score { get; set; } = "";
    public string Feedback { get; set; } = "";

    public bool IsGraded =>
        !string.IsNullOrWhiteSpace(Score) ||
        !string.IsNullOrWhiteSpace(Feedback);

    public double? ScoreNumber
    {
        get
        {
            if (double.TryParse(
                    Score.Replace(",", "."),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var value
                ))
            {
                return value;
            }

            return null;
        }
    }
}

public class HomeworkReportsViewModel
{
    public ReportFilterViewModel Filter { get; set; } = new();
    public List<ReportOptionViewModel> Classes { get; set; } = new();
    public List<ReportOptionViewModel> Assignments { get; set; } = new();
    public List<HomeworkReportRowViewModel> Rows { get; set; } = new();

    public int SubmittedCount => Rows.Count(x => x.IsSubmitted);
    public int MissingCount => Rows.Count(x => !x.IsSubmitted);
    public int GradedCount => Rows.Count(x => x.IsGraded);
}
