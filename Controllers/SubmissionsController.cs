using Microsoft.AspNetCore.Mvc;
using mvc_web.Models;
using mvc_web.Services;

namespace mvc_web.Controllers;

public class SubmissionsController : Controller
{
    private readonly FirestoreService _firestore;
    private readonly SessionService _session;

    public SubmissionsController(
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

        ViewData["Title"] = "Teslimler";
        ViewData["PageTitle"] = "Ödev Teslimleri";
        ViewData["PageSubtitle"] = "Öğrencilerin gönderdiği ödev teslimlerini görüntüleyin.";

        var snapshot = await _firestore.Submissions
            .OrderByDescending("createdAt")
            .GetSnapshotAsync();

        var submissions = new List<SubmissionViewModel>();

        foreach (var doc in snapshot.Documents)
        {
            var data = doc.ToDictionary();

            submissions.Add(new SubmissionViewModel
            {
                Id = doc.Id,
                AssignmentId = GetValue(data, "assignmentId"),
                AssignmentTitle = GetValue(data, "assignmentTitle", "-"),
                Lesson = GetValue(data, "lesson", "-"),
                ClassName = GetValue(data, "className", "-"),
                TeacherId = GetValue(data, "teacherId"),
                TeacherName = GetValue(data, "teacherName", GetValue(data, "teacher", "-")),
                StudentNo = GetValue(data, "studentNo", "-"),
                Answer = GetValue(data, "answer"),
                Link = GetValue(data, "link"),
                Status = GetValue(data, "status", "Teslim Edildi"),
                Grade = GetValue(data, "grade"),
                Feedback = GetValue(data, "feedback")
            });
        }

        return View(submissions);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        if (!_session.IsAdmin(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            return RedirectToAction("Index");
        }

        await _firestore.Submissions.Document(id).DeleteAsync();

        TempData["Success"] = "Teslim silindi.";
        return RedirectToAction("Index");
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