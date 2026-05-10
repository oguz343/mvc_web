using Microsoft.AspNetCore.Mvc;
using mvc_web.Models;
using mvc_web.Services;

namespace mvc_web.Controllers;

public class DashboardController : Controller
{
    private readonly FirestoreService _firestore;
    private readonly SessionService _session;

    public DashboardController(
        FirestoreService firestore,
        SessionService session
    )
    {
        _firestore = firestore;
        _session = session;
    }

    public async Task<IActionResult> Index()
    {
        if (!_session.IsAdmin(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        ViewData["Title"] = "Dashboard";
        ViewData["PageTitle"] = "Admin Dashboard";
        ViewData["PageSubtitle"] = "Okul sistemi genel durumu, kullanıcılar ve teslim özetleri.";

        var model = new DashboardViewModel
        {
            StudentCount = await _firestore.CountWhereAsync(
                _firestore.Users,
                "role",
                "Öğrenci"
            ),
            TeacherCount = await _firestore.CountWhereAsync(
                _firestore.Users,
                "role",
                "Öğretmen"
            ),
            ParentCount = await _firestore.CountWhereAsync(
                _firestore.Users,
                "role",
                "Veli"
            ),
            ClassCount = await _firestore.CountAsync(_firestore.Classes),
            LessonCount = await _firestore.CountAsync(_firestore.Lessons),
            SubmissionCount = await _firestore.CountAsync(_firestore.Submissions),
            AnnouncementCount = await _firestore.CountAsync(_firestore.Announcements),
            PasswordRequestCount = await _firestore.CountAsync(_firestore.PasswordRequests),
        };

        var latestUsersSnapshot = await _firestore.Users
            .OrderByDescending("createdAt")
            .Limit(6)
            .GetSnapshotAsync();

        foreach (var doc in latestUsersSnapshot.Documents)
        {
            var data = doc.ToDictionary();

            var role = GetValue(data, "role");
            var className = GetValue(data, "className");
            var linkedStudentNo = GetValue(data, "linkedStudentNo");
            var branch = GetValue(data, "branch");

            var detail = role;

            if (role == "Öğretmen" && !string.IsNullOrWhiteSpace(branch))
            {
                detail = $"Branş: {branch}";
            }
            else if (!string.IsNullOrWhiteSpace(className))
            {
                detail = $"Sınıf: {className}";
            }
            else if (!string.IsNullOrWhiteSpace(linkedStudentNo))
            {
                detail = $"Bağlı öğrenci: {linkedStudentNo}";
            }

            model.LatestUsers.Add(new DashboardUserItem
            {
                Id = doc.Id,
                Name = GetValue(data, "name", "-"),
                Role = role,
                Number = GetValue(data, "schoolNo", "-"),
                Detail = detail
            });
        }

        var latestSubmissionsSnapshot = await _firestore.Submissions
            .OrderByDescending("createdAt")
            .Limit(6)
            .GetSnapshotAsync();

        foreach (var doc in latestSubmissionsSnapshot.Documents)
        {
            var data = doc.ToDictionary();

            model.LatestSubmissions.Add(new DashboardSubmissionItem
            {
                Id = doc.Id,
                AssignmentTitle = GetValue(data, "assignmentTitle", "-"),
                StudentNo = GetValue(data, "studentNo", "-"),
                Lesson = GetValue(data, "lesson", "-"),
                Status = GetValue(data, "status", "Teslim Edildi"),
                Grade = GetValue(data, "grade", "-")
            });
        }

        return View(model);
    }

    private static string GetValue(
        Dictionary<string, object> data,
        string key,
        string defaultValue = ""
    )
    {
        if (!data.ContainsKey(key) || data[key] == null)
        {
            return defaultValue;
        }

        return data[key]?.ToString() ?? defaultValue;
    }
}