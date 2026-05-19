using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using mvc_web.Filters;
using mvc_web.Models;
using mvc_web.Services;
using System.Text.RegularExpressions;

namespace mvc_web.Controllers;

[AdminOnly]
public class ReportsController : Controller
{
    private readonly FirestoreDb _firestore;
    private readonly SessionService _session;

    public ReportsController(FirestoreDb firestore, SessionService session)
    {
        _firestore = firestore;
        _session = session;
    }

    [HttpGet]
    public async Task<IActionResult> Index(ReportFilterViewModel filter)
    {
        ViewData["Title"] = "Raporlama";
        ViewData["PageTitle"] = "Ödev Raporları";
        ViewData["PageSubtitle"] = "Teslim eden, teslim etmeyen, tarih ve not raporlarını filtreleyip yazdırın.";

        filter ??= new ReportFilterViewModel();

        var students = await LoadStudents();
        var assignments = await LoadAssignments();
        var submissions = await LoadSubmissions();

        var rows = BuildRows(students, assignments, submissions, filter);
        rows = ApplyFilters(rows, filter);
        rows = ApplySort(rows, filter.Sort);

        var model = new HomeworkReportsViewModel
        {
            Filter = filter,
            Classes = students
                .Select(x => x.ClassName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .OrderBy(x => x)
                .Select(x => new ReportOptionViewModel { Value = x, Text = x })
                .ToList(),
            Assignments = assignments
                .OrderByDescending(x => x.CreatedAt ?? DateTime.MinValue)
                .Select(x => new ReportOptionViewModel
                {
                    Value = x.Id,
                    Text = $"{x.Title} - {x.ClassName} - {x.LessonName}"
                })
                .ToList(),
            Rows = rows
        };

        return View(model);
    }

    private static List<HomeworkReportRowViewModel> BuildRows(
        List<StudentReportItem> students,
        List<AssignmentReportItem> assignments,
        List<SubmissionReportItem> submissions,
        ReportFilterViewModel filter
    )
    {
        var submissionById = submissions
            .Where(x => !string.IsNullOrWhiteSpace(x.AssignmentId) && !string.IsNullOrWhiteSpace(x.StudentNo))
            .GroupBy(x => NormalizeKey($"{x.AssignmentId}_{x.StudentNo}"))
            .ToDictionary(x => x.Key, x => PickBestSubmission(x));

        var submissionByContent = submissions
            .Where(x => !string.IsNullOrWhiteSpace(x.StudentNo))
            .GroupBy(x => NormalizeKey($"{x.Title}_{x.LessonName}_{x.ClassName}_{x.StudentNo}"))
            .ToDictionary(x => x.Key, x => PickBestSubmission(x));

        var rows = new List<HomeworkReportRowViewModel>();

        foreach (var assignment in assignments)
        {
            if (!string.IsNullOrWhiteSpace(filter.AssignmentId) && assignment.Id != filter.AssignmentId)
            {
                continue;
            }

            foreach (var student in students.Where(x => NormalizeClassName(x.ClassName) == NormalizeClassName(assignment.ClassName)))
            {
                var submission = FindSubmission(assignment, student, submissionById, submissionByContent);

                rows.Add(new HomeworkReportRowViewModel
                {
                    StudentName = student.Name,
                    StudentNo = student.Number,
                    ClassName = student.ClassName,
                    AssignmentId = assignment.Id,
                    AssignmentTitle = assignment.Title,
                    LessonName = assignment.LessonName,
                    TeacherName = assignment.TeacherName,
                    DueDate = assignment.DueDate,
                    SubmittedAt = submission?.SubmittedAt,
                    IsSubmitted = submission != null,
                    Score = submission?.Score ?? "",
                    Feedback = submission?.Feedback ?? ""
                });
            }
        }

        return rows;
    }

    private static List<HomeworkReportRowViewModel> ApplyFilters(
        List<HomeworkReportRowViewModel> rows,
        ReportFilterViewModel filter
    )
    {
        if (!string.IsNullOrWhiteSpace(filter.ClassName))
        {
            rows = rows
                .Where(x => NormalizeClassName(x.ClassName) == NormalizeClassName(filter.ClassName))
                .ToList();
        }

        rows = (filter.Status ?? "all") switch
        {
            "submitted" => rows.Where(x => x.IsSubmitted).ToList(),
            "missing" => rows.Where(x => !x.IsSubmitted).ToList(),
            "graded" => rows.Where(x => x.IsGraded).ToList(),
            "top" => rows.Where(x => x.ScoreNumber.HasValue).ToList(),
            _ => rows
        };

        var from = ParseDate(filter.FromDate);
        var to = ParseDate(filter.ToDate)?.Date.AddDays(1).AddTicks(-1);

        if (from.HasValue)
        {
            rows = rows.Where(x => ReportDate(x) >= from.Value).ToList();
        }

        if (to.HasValue)
        {
            rows = rows.Where(x => ReportDate(x) <= to.Value).ToList();
        }

        return rows;
    }

    private static List<HomeworkReportRowViewModel> ApplySort(
        List<HomeworkReportRowViewModel> rows,
        string sort
    )
    {
        return (sort ?? "date_desc") switch
        {
            "score_desc" => rows
                .OrderByDescending(x => x.ScoreNumber ?? double.MinValue)
                .ThenBy(x => x.StudentName)
                .ToList(),
            "score_asc" => rows
                .OrderBy(x => x.ScoreNumber ?? double.MaxValue)
                .ThenBy(x => x.StudentName)
                .ToList(),
            "student" => rows
                .OrderBy(x => x.ClassName)
                .ThenBy(x => x.StudentName)
                .ToList(),
            "assignment" => rows
                .OrderBy(x => x.AssignmentTitle)
                .ThenBy(x => x.StudentName)
                .ToList(),
            _ => rows
                .OrderByDescending(ReportDate)
                .ThenBy(x => x.ClassName)
                .ThenBy(x => x.StudentName)
                .ToList()
        };
    }

    private async Task<List<StudentReportItem>> LoadStudents()
    {
        var result = new List<StudentReportItem>();
        var snapshot = await _firestore.Collection("users").GetSnapshotAsync();

        foreach (var doc in snapshot.Documents)
        {
            var data = doc.ToDictionary();

            if (IsDeleted(data) || !IsStudentRole(GetText(data, "role", "Role", "userRole", "UserRole")))
            {
                continue;
            }

            var number = OnlyDigits(FirstNonEmpty(
                GetText(data, "number", "Number"),
                GetText(data, "schoolNo", "SchoolNo"),
                GetText(data, "studentNo", "StudentNo")
            ));
            var className = NormalizeClassName(FirstNonEmpty(
                GetText(data, "className", "ClassName"),
                GetText(data, "class", "Class")
            ));

            if (string.IsNullOrWhiteSpace(number) || string.IsNullOrWhiteSpace(className))
            {
                continue;
            }

            result.Add(new StudentReportItem
            {
                Id = doc.Id,
                Name = FirstNonEmpty(GetText(data, "name", "Name"), GetText(data, "fullName", "FullName"), "-"),
                Number = number,
                ClassName = className
            });
        }

        return result
            .GroupBy(x => x.Number)
            .Select(x => x.First())
            .ToList();
    }

    private async Task<List<AssignmentReportItem>> LoadAssignments()
    {
        var result = new List<AssignmentReportItem>();
        var seen = new HashSet<string>();

        foreach (var collection in new[] { "homeworks", "assignments" })
        {
            var snapshot = await _firestore.Collection(collection).GetSnapshotAsync();

            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();

                if (IsDeleted(data))
                {
                    continue;
                }

                var title = FirstNonEmpty(GetText(data, "title", "Title"), GetText(data, "name", "Name"), "Ödev");
                var lessonName = FirstNonEmpty(GetText(data, "lessonName", "LessonName"), GetText(data, "lesson", "Lesson"));
                var className = NormalizeClassName(FirstNonEmpty(GetText(data, "className", "ClassName"), GetText(data, "class", "Class"), GetText(data, "targetClass", "TargetClass")));
                var key = NormalizeKey($"{title}_{lessonName}_{className}");

                if (string.IsNullOrWhiteSpace(className) || seen.Contains(key))
                {
                    continue;
                }

                seen.Add(key);
                result.Add(new AssignmentReportItem
                {
                    Id = doc.Id,
                    Title = title,
                    LessonName = lessonName,
                    ClassName = className,
                    TeacherName = FirstNonEmpty(GetText(data, "teacherName", "TeacherName"), GetText(data, "teacher", "Teacher"), "-"),
                    DueDate = GetDate(data, "dueDate", "DueDate", "deadline", "Deadline", "endDate", "EndDate"),
                    CreatedAt = GetDate(data, "createdAt", "CreatedAt")
                });
            }
        }

        return result;
    }

    private async Task<List<SubmissionReportItem>> LoadSubmissions()
    {
        var result = new List<SubmissionReportItem>();
        var seen = new HashSet<string>();

        foreach (var collection in new[] { "submissions", "homework_submissions" })
        {
            var snapshot = await _firestore.Collection(collection).GetSnapshotAsync();

            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();

                if (IsDeleted(data))
                {
                    continue;
                }

                var item = new SubmissionReportItem
                {
                    Id = doc.Id,
                    AssignmentId = FirstNonEmpty(GetText(data, "assignmentId", "AssignmentId"), GetText(data, "homeworkId", "HomeworkId")),
                    Title = FirstNonEmpty(GetText(data, "title", "Title"), GetText(data, "assignmentTitle", "AssignmentTitle"), GetText(data, "homeworkTitle", "HomeworkTitle")),
                    LessonName = FirstNonEmpty(GetText(data, "lessonName", "LessonName"), GetText(data, "lesson", "Lesson"), GetText(data, "courseName", "CourseName")),
                    ClassName = NormalizeClassName(FirstNonEmpty(GetText(data, "className", "ClassName"), GetText(data, "class", "Class"), GetText(data, "targetClass", "TargetClass"))),
                    StudentNo = OnlyDigits(FirstNonEmpty(GetText(data, "studentNo", "StudentNo"), GetText(data, "studentNumber", "StudentNumber"), GetText(data, "schoolNo", "SchoolNo"))),
                    SubmittedAt = GetDate(data, "submittedAt", "SubmittedAt", "createdAt", "CreatedAt"),
                    Score = FirstNonEmpty(GetText(data, "score", "Score"), GetText(data, "grade", "Grade"), GetText(data, "point", "Point"), GetText(data, "not", "Not")),
                    Feedback = FirstNonEmpty(GetText(data, "feedback", "Feedback"), GetText(data, "comment", "Comment"), GetText(data, "geriDonus", "GeriDonus"))
                };
                var key = NormalizeKey($"{item.AssignmentId}_{item.StudentNo}_{item.Title}_{item.LessonName}_{item.ClassName}");

                if (seen.Contains(key))
                {
                    continue;
                }

                seen.Add(key);
                result.Add(item);
            }
        }

        return result;
    }

    private static SubmissionReportItem? FindSubmission(
        AssignmentReportItem assignment,
        StudentReportItem student,
        Dictionary<string, SubmissionReportItem> byId,
        Dictionary<string, SubmissionReportItem> byContent
    )
    {
        var idKey = NormalizeKey($"{assignment.Id}_{student.Number}");

        if (byId.TryGetValue(idKey, out var byIdMatch))
        {
            return byIdMatch;
        }

        var contentKey = NormalizeKey($"{assignment.Title}_{assignment.LessonName}_{assignment.ClassName}_{student.Number}");

        return byContent.TryGetValue(contentKey, out var byContentMatch) ? byContentMatch : null;
    }

    private static SubmissionReportItem PickBestSubmission(IEnumerable<SubmissionReportItem> submissions)
    {
        return submissions
            .OrderByDescending(x => !string.IsNullOrWhiteSpace(x.Score) || !string.IsNullOrWhiteSpace(x.Feedback))
            .ThenByDescending(x => x.SubmittedAt ?? DateTime.MinValue)
            .First();
    }

    private static DateTime ReportDate(HomeworkReportRowViewModel row)
    {
        return row.SubmittedAt ?? row.DueDate ?? DateTime.MinValue;
    }

    private static DateTime? ParseDate(string value)
    {
        return DateTime.TryParse(value, out var date) ? date.Date : null;
    }

    private static bool IsDeleted(Dictionary<string, object> data)
    {
        var deleted = GetText(data, "isDeleted", "IsDeleted", "deleted", "Deleted").Trim().ToLowerInvariant();
        var active = GetText(data, "isActive", "IsActive", "active", "Active").Trim().ToLowerInvariant();
        var status = NormalizeKey(GetText(data, "status", "Status"));

        return deleted is "true" or "1" or "evet" or "yes" ||
               active is "false" or "0" or "hayir" or "no" ||
               status is "silindi" or "deleted" or "pasif" or "inactive";
    }

    private static bool IsStudentRole(string role)
    {
        var key = NormalizeKey(role);

        return key == "ogrenci" ||
               key == "student" ||
               key.Contains("renci");
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return "";
    }

    private static string GetText(Dictionary<string, object> data, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!data.TryGetValue(key, out var value) || value == null)
            {
                continue;
            }

            if (value is Timestamp timestamp)
            {
                return timestamp.ToDateTime().ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            }

            var text = value.ToString();

            if (!string.IsNullOrWhiteSpace(text))
            {
                return text.Trim();
            }
        }

        return "";
    }

    private static DateTime? GetDate(Dictionary<string, object> data, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!data.TryGetValue(key, out var value) || value == null)
            {
                continue;
            }

            if (value is Timestamp timestamp)
            {
                return timestamp.ToDateTime().ToLocalTime();
            }

            if (value is DateTime dateTime)
            {
                return dateTime.ToLocalTime();
            }

            if (DateTime.TryParse(value.ToString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static string OnlyDigits(string value)
    {
        return new string((value ?? "").Where(char.IsDigit).ToArray());
    }

    private static string NormalizeClassName(string value)
    {
        var original = (value ?? "").Trim();

        if (string.IsNullOrWhiteSpace(original))
        {
            return "";
        }

        var text = original
            .ToUpperInvariant()
            .Replace("SINIF", "")
            .Replace("SUBE", "")
            .Replace("SÄ°NÄ°F", "")
            .Replace("ŞUBE", "")
            .Replace("ÅUBE", "")
            .Replace("_", "-")
            .Replace("/", "-")
            .Replace("\\", "-")
            .Replace(".", "-");

        text = Regex.Replace(text, @"\s+", "");

        var match = Regex.Match(text, @"(9|10|11|12)[^\dA-Z]*([A-Z])");

        if (match.Success)
        {
            return $"{match.Groups[1].Value}-{match.Groups[2].Value}";
        }

        match = Regex.Match(text, @"([A-Z])[^\dA-Z]*(9|10|11|12)");

        if (match.Success)
        {
            return $"{match.Groups[2].Value}-{match.Groups[1].Value}";
        }

        return original.ToUpperInvariant();
    }

    private static string NormalizeKey(string value)
    {
        value = (value ?? "").Trim().ToLowerInvariant();

        value = value
            .Replace("ı", "i")
            .Replace("ğ", "g")
            .Replace("ü", "u")
            .Replace("ş", "s")
            .Replace("ö", "o")
            .Replace("ç", "c")
            .Replace("ı", "i")
            .Replace("ğ", "g")
            .Replace("ü", "u")
            .Replace("ş", "s")
            .Replace("ö", "o")
            .Replace("ç", "c")
            .Replace("Ä±", "i")
            .Replace("ÄŸ", "g")
            .Replace("Ã¼", "u")
            .Replace("ÅŸ", "s")
            .Replace("Ã¶", "o")
            .Replace("Ã§", "c");

        return new string(value.Where(char.IsLetterOrDigit).ToArray());
    }

    private sealed class StudentReportItem
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Number { get; set; } = "";
        public string ClassName { get; set; } = "";
    }

    private sealed class AssignmentReportItem
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string LessonName { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string TeacherName { get; set; } = "";
        public DateTime? DueDate { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    private sealed class SubmissionReportItem
    {
        public string Id { get; set; } = "";
        public string AssignmentId { get; set; } = "";
        public string Title { get; set; } = "";
        public string LessonName { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string StudentNo { get; set; } = "";
        public DateTime? SubmittedAt { get; set; }
        public string Score { get; set; } = "";
        public string Feedback { get; set; } = "";
    }
}
