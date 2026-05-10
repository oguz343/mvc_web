using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using mvc_web.Models;
using mvc_web.Services;

namespace mvc_web.Controllers;

public class TeacherController : Controller
{
    private readonly FirestoreService _firestore;
    private readonly SessionService _session;

    public TeacherController(
        FirestoreService firestore,
        SessionService session
    )
    {
        _firestore = firestore;
        _session = session;
    }

    public async Task<IActionResult> Index()
    {
        if (!_session.IsTeacher(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        var teacherId = _session.GetUserId(HttpContext) ?? "";
        var teacherName = _session.GetName(HttpContext) ?? "Öğretmen";
        var teacherNumber = _session.GetNumber(HttpContext) ?? "-";

        ViewData["Title"] = "Öğretmen Dashboard";
        ViewData["PageTitle"] = "Öğretmen Dashboard";
        ViewData["PageSubtitle"] = "Dersleriniz, ödevleriniz ve teslim durumları.";

        var branch = await GetTeacherBranch(teacherId);

        var lessonsSnapshot = await _firestore.Lessons
            .WhereEqualTo("teacherId", teacherId)
            .GetSnapshotAsync();

        var assignmentsSnapshot = await _firestore.Assignments
            .WhereEqualTo("teacherId", teacherId)
            .GetSnapshotAsync();

        var submissionsSnapshot = await _firestore.Submissions
            .WhereEqualTo("teacherId", teacherId)
            .GetSnapshotAsync();

        var model = new TeacherDashboardViewModel
        {
            TeacherId = teacherId,
            TeacherName = teacherName,
            TeacherNumber = teacherNumber,
            Branch = branch,
            LessonCount = lessonsSnapshot.Documents.Count,
            AssignmentCount = assignmentsSnapshot.Documents.Count,
            SubmissionCount = submissionsSnapshot.Documents.Count,
            GradedSubmissionCount = submissionsSnapshot.Documents.Count(doc =>
            {
                var data = doc.ToDictionary();
                return GetValue(data, "status") == "Değerlendirildi";
            })
        };

        foreach (var doc in lessonsSnapshot.Documents.Take(6))
        {
            var data = doc.ToDictionary();

            model.Lessons.Add(new LessonViewModel
            {
                Id = doc.Id,
                Name = GetValue(data, "name", "-"),
                ClassName = GetValue(data, "className", "-"),
                TeacherId = teacherId,
                TeacherName = teacherName,
                TeacherBranch = branch
            });
        }

        foreach (var doc in assignmentsSnapshot.Documents.Take(6))
        {
            var data = doc.ToDictionary();

            model.LatestAssignments.Add(new AssignmentViewModel
            {
                Id = doc.Id,
                Title = GetValue(data, "title", "-"),
                LessonId = GetValue(data, "lessonId"),
                Lesson = GetValue(data, "lesson", "-"),
                ClassName = GetValue(data, "className", "-"),
                TeacherId = teacherId,
                TeacherName = teacherName,
                TeacherBranch = branch,
                DueDate = GetValue(data, "dueDate", "-"),
                Type = GetValue(data, "type", "Metin"),
                Status = GetValue(data, "status", "Aktif"),
                Description = GetValue(data, "description")
            });
        }

        foreach (var doc in submissionsSnapshot.Documents.Take(6))
        {
            var data = doc.ToDictionary();

            model.LatestSubmissions.Add(new SubmissionViewModel
            {
                Id = doc.Id,
                AssignmentId = GetValue(data, "assignmentId"),
                AssignmentTitle = GetValue(data, "assignmentTitle", "-"),
                Lesson = GetValue(data, "lesson", "-"),
                ClassName = GetValue(data, "className", "-"),
                TeacherId = teacherId,
                TeacherName = teacherName,
                StudentNo = GetValue(data, "studentNo", "-"),
                Answer = GetValue(data, "answer"),
                Link = GetValue(data, "link"),
                Status = GetValue(data, "status", "Teslim Edildi"),
                Grade = GetValue(data, "grade"),
                Feedback = GetValue(data, "feedback")
            });
        }

        return View(model);
    }

    public async Task<IActionResult> Assignments()
    {
        if (!_session.IsTeacher(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        var teacherId = _session.GetUserId(HttpContext) ?? "";

        ViewData["Title"] = "Ödevlerim";
        ViewData["PageTitle"] = "Ödevlerim";
        ViewData["PageSubtitle"] = "Kendi derslerinize verdiğiniz ödevleri yönetin.";

        var snapshot = await _firestore.Assignments
            .WhereEqualTo("teacherId", teacherId)
            .GetSnapshotAsync();

        var assignments = new List<AssignmentViewModel>();

        foreach (var doc in snapshot.Documents)
        {
            var data = doc.ToDictionary();

            assignments.Add(new AssignmentViewModel
            {
                Id = doc.Id,
                Title = GetValue(data, "title", "-"),
                LessonId = GetValue(data, "lessonId"),
                Lesson = GetValue(data, "lesson", "-"),
                ClassName = GetValue(data, "className", "-"),
                TeacherId = teacherId,
                TeacherName = GetValue(data, "teacher", GetValue(data, "teacherName", "-")),
                TeacherBranch = GetValue(data, "teacherBranch", "Branş yok"),
                DueDate = GetValue(data, "dueDate", "-"),
                Type = GetValue(data, "type", "Metin"),
                Status = GetValue(data, "status", "Aktif"),
                Description = GetValue(data, "description")
            });
        }

        assignments = assignments
            .OrderByDescending(x => x.Id)
            .ToList();

        return View(assignments);
    }

    [HttpGet]
    public async Task<IActionResult> CreateAssignment()
    {
        if (!_session.IsTeacher(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        ViewData["Title"] = "Ödev Oluştur";
        ViewData["PageTitle"] = "Yeni Ödev";
        ViewData["PageSubtitle"] = "Sadece size atanmış derslere ödev verebilirsiniz.";

        await LoadTeacherLessonOptions();

        return View(new AssignmentViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAssignment(AssignmentViewModel model)
    {
        if (!_session.IsTeacher(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        var teacherId = _session.GetUserId(HttpContext) ?? "";
        var teacherName = _session.GetName(HttpContext) ?? "Öğretmen";
        var teacherBranch = await GetTeacherBranch(teacherId);

        await LoadTeacherLessonOptions();

        var lessonDoc = await GetTeacherLesson(model.LessonId, teacherId);

        if (lessonDoc == null)
        {
            ModelState.AddModelError(
                nameof(model.LessonId),
                "Seçilen ders size ait değil veya bulunamadı."
            );
        }
        else
        {
            var lessonData = lessonDoc.ToDictionary();

            model.Lesson = GetValue(lessonData, "name", "-");
            model.ClassName = GetValue(lessonData, "className", "-");
            model.TeacherId = teacherId;
            model.TeacherName = teacherName;
            model.TeacherBranch = teacherBranch;
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Ödev Oluştur";
            ViewData["PageTitle"] = "Yeni Ödev";
            ViewData["PageSubtitle"] = "Sadece size atanmış derslere ödev verebilirsiniz.";
            return View(model);
        }

        await _firestore.Assignments.AddAsync(new Dictionary<string, object?>
        {
            { "title", model.Title.Trim() },
            { "lessonId", model.LessonId.Trim() },
            { "lesson", model.Lesson.Trim() },
            { "className", model.ClassName.Trim() },
            { "teacherId", teacherId },
            { "teacher", teacherName },
            { "teacherName", teacherName },
            { "teacherBranch", string.IsNullOrWhiteSpace(teacherBranch) ? "Branş yok" : teacherBranch },
            { "dueDate", model.DueDate.Trim() },
            { "type", string.IsNullOrWhiteSpace(model.Type) ? "Metin" : model.Type.Trim() },
            { "status", string.IsNullOrWhiteSpace(model.Status) ? "Aktif" : model.Status.Trim() },
            { "description", model.Description?.Trim() ?? "" },
            { "createdAt", Timestamp.GetCurrentTimestamp() },
            { "updatedAt", Timestamp.GetCurrentTimestamp() }
        });

        TempData["Success"] = "Ödev oluşturuldu.";
        return RedirectToAction("Assignments");
    }

    [HttpGet]
    public async Task<IActionResult> EditAssignment(string id)
    {
        if (!_session.IsTeacher(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        var teacherId = _session.GetUserId(HttpContext) ?? "";

        if (string.IsNullOrWhiteSpace(id))
        {
            return RedirectToAction("Assignments");
        }

        var doc = await _firestore.Assignments.Document(id).GetSnapshotAsync();

        if (!doc.Exists)
        {
            TempData["Error"] = "Ödev bulunamadı.";
            return RedirectToAction("Assignments");
        }

        var data = doc.ToDictionary();

        if (GetValue(data, "teacherId") != teacherId)
        {
            TempData["Error"] = "Bu ödevi düzenleme yetkiniz yok.";
            return RedirectToAction("Assignments");
        }

        ViewData["Title"] = "Ödev Düzenle";
        ViewData["PageTitle"] = "Ödev Düzenle";
        ViewData["PageSubtitle"] = "Verdiğiniz ödevin bilgilerini güncelleyin.";

        await LoadTeacherLessonOptions();

        var model = new AssignmentViewModel
        {
            Id = doc.Id,
            Title = GetValue(data, "title"),
            LessonId = GetValue(data, "lessonId"),
            Lesson = GetValue(data, "lesson"),
            ClassName = GetValue(data, "className"),
            TeacherId = GetValue(data, "teacherId"),
            TeacherName = GetValue(data, "teacherName", GetValue(data, "teacher")),
            TeacherBranch = GetValue(data, "teacherBranch"),
            DueDate = GetValue(data, "dueDate"),
            Type = GetValue(data, "type", "Metin"),
            Status = GetValue(data, "status", "Aktif"),
            Description = GetValue(data, "description")
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAssignment(AssignmentViewModel model)
    {
        if (!_session.IsTeacher(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        var teacherId = _session.GetUserId(HttpContext) ?? "";
        var teacherName = _session.GetName(HttpContext) ?? "Öğretmen";
        var teacherBranch = await GetTeacherBranch(teacherId);

        await LoadTeacherLessonOptions();

        var assignmentDoc = await _firestore.Assignments
            .Document(model.Id)
            .GetSnapshotAsync();

        if (!assignmentDoc.Exists)
        {
            TempData["Error"] = "Ödev bulunamadı.";
            return RedirectToAction("Assignments");
        }

        var assignmentData = assignmentDoc.ToDictionary();

        if (GetValue(assignmentData, "teacherId") != teacherId)
        {
            TempData["Error"] = "Bu ödevi düzenleme yetkiniz yok.";
            return RedirectToAction("Assignments");
        }

        var lessonDoc = await GetTeacherLesson(model.LessonId, teacherId);

        if (lessonDoc == null)
        {
            ModelState.AddModelError(
                nameof(model.LessonId),
                "Seçilen ders size ait değil veya bulunamadı."
            );
        }
        else
        {
            var lessonData = lessonDoc.ToDictionary();

            model.Lesson = GetValue(lessonData, "name", "-");
            model.ClassName = GetValue(lessonData, "className", "-");
            model.TeacherId = teacherId;
            model.TeacherName = teacherName;
            model.TeacherBranch = teacherBranch;
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Ödev Düzenle";
            ViewData["PageTitle"] = "Ödev Düzenle";
            ViewData["PageSubtitle"] = "Verdiğiniz ödevin bilgilerini güncelleyin.";
            return View(model);
        }

        await _firestore.Assignments.Document(model.Id).UpdateAsync(
            new Dictionary<string, object?>
            {
                { "title", model.Title.Trim() },
                { "lessonId", model.LessonId.Trim() },
                { "lesson", model.Lesson.Trim() },
                { "className", model.ClassName.Trim() },
                { "teacherId", teacherId },
                { "teacher", teacherName },
                { "teacherName", teacherName },
                { "teacherBranch", string.IsNullOrWhiteSpace(teacherBranch) ? "Branş yok" : teacherBranch },
                { "dueDate", model.DueDate.Trim() },
                { "type", string.IsNullOrWhiteSpace(model.Type) ? "Metin" : model.Type.Trim() },
                { "status", string.IsNullOrWhiteSpace(model.Status) ? "Aktif" : model.Status.Trim() },
                { "description", model.Description?.Trim() ?? "" },
                { "updatedAt", Timestamp.GetCurrentTimestamp() }
            }
        );

        TempData["Success"] = "Ödev güncellendi.";
        return RedirectToAction("Assignments");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAssignment(string id)
    {
        if (!_session.IsTeacher(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        var teacherId = _session.GetUserId(HttpContext) ?? "";

        if (string.IsNullOrWhiteSpace(id))
        {
            return RedirectToAction("Assignments");
        }

        var assignmentDoc = await _firestore.Assignments
            .Document(id)
            .GetSnapshotAsync();

        if (!assignmentDoc.Exists)
        {
            TempData["Error"] = "Ödev bulunamadı.";
            return RedirectToAction("Assignments");
        }

        var data = assignmentDoc.ToDictionary();

        if (GetValue(data, "teacherId") != teacherId)
        {
            TempData["Error"] = "Bu ödevi silme yetkiniz yok.";
            return RedirectToAction("Assignments");
        }

        await _firestore.Assignments.Document(id).DeleteAsync();

        TempData["Success"] = "Ödev silindi.";
        return RedirectToAction("Assignments");
    }

    public async Task<IActionResult> Submissions()
    {
        if (!_session.IsTeacher(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        var teacherId = _session.GetUserId(HttpContext) ?? "";

        ViewData["Title"] = "Teslimler";
        ViewData["PageTitle"] = "Öğrenci Teslimleri";
        ViewData["PageSubtitle"] = "Öğrencilerin teslimlerini değerlendirin, not ve geri dönüş yazın.";

        var snapshot = await _firestore.Submissions
            .WhereEqualTo("teacherId", teacherId)
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
                TeacherId = teacherId,
                TeacherName = GetValue(data, "teacherName", "-"),
                StudentNo = GetValue(data, "studentNo", "-"),
                Answer = GetValue(data, "answer"),
                Link = GetValue(data, "link"),
                Status = GetValue(data, "status", "Teslim Edildi"),
                Grade = GetValue(data, "grade"),
                Feedback = GetValue(data, "feedback")
            });
        }

        submissions = submissions
            .OrderBy(x => x.Status == "Değerlendirildi")
            .ThenByDescending(x => x.Id)
            .ToList();

        return View(submissions);
    }

    [HttpGet]
    public async Task<IActionResult> GradeSubmission(string id)
    {
        if (!_session.IsTeacher(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        var teacherId = _session.GetUserId(HttpContext) ?? "";

        if (string.IsNullOrWhiteSpace(id))
        {
            return RedirectToAction("Submissions");
        }

        var doc = await _firestore.Submissions.Document(id).GetSnapshotAsync();

        if (!doc.Exists)
        {
            TempData["Error"] = "Teslim bulunamadı.";
            return RedirectToAction("Submissions");
        }

        var data = doc.ToDictionary();

        if (GetValue(data, "teacherId") != teacherId)
        {
            TempData["Error"] = "Bu teslimi değerlendirme yetkiniz yok.";
            return RedirectToAction("Submissions");
        }

        ViewData["Title"] = "Teslim Değerlendir";
        ViewData["PageTitle"] = "Teslim Değerlendir";
        ViewData["PageSubtitle"] = "Öğrenci teslimine not ve geri dönüş açıklaması yazın.";

        var model = new GradeSubmissionViewModel
        {
            Id = doc.Id,
            AssignmentTitle = GetValue(data, "assignmentTitle", "-"),
            StudentNo = GetValue(data, "studentNo", "-"),
            Answer = GetValue(data, "answer"),
            Link = GetValue(data, "link"),
            Grade = GetValue(data, "grade"),
            Feedback = GetValue(data, "feedback")
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GradeSubmission(GradeSubmissionViewModel model)
    {
        if (!_session.IsTeacher(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        var teacherId = _session.GetUserId(HttpContext) ?? "";

        var doc = await _firestore.Submissions.Document(model.Id).GetSnapshotAsync();

        if (!doc.Exists)
        {
            TempData["Error"] = "Teslim bulunamadı.";
            return RedirectToAction("Submissions");
        }

        var data = doc.ToDictionary();

        if (GetValue(data, "teacherId") != teacherId)
        {
            TempData["Error"] = "Bu teslimi değerlendirme yetkiniz yok.";
            return RedirectToAction("Submissions");
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Teslim Değerlendir";
            ViewData["PageTitle"] = "Teslim Değerlendir";
            ViewData["PageSubtitle"] = "Öğrenci teslimine not ve geri dönüş açıklaması yazın.";
            return View(model);
        }

        await _firestore.Submissions.Document(model.Id).UpdateAsync(
            new Dictionary<string, object?>
            {
                { "grade", model.Grade.Trim() },
                { "feedback", model.Feedback?.Trim() ?? "" },
                { "status", "Değerlendirildi" },
                { "gradedAt", Timestamp.GetCurrentTimestamp() },
                { "updatedAt", Timestamp.GetCurrentTimestamp() }
            }
        );

        TempData["Success"] = "Teslim değerlendirildi.";
        return RedirectToAction("Submissions");
    }

    private async Task LoadTeacherLessonOptions()
    {
        var teacherId = _session.GetUserId(HttpContext) ?? "";

        var snapshot = await _firestore.Lessons
            .WhereEqualTo("teacherId", teacherId)
            .GetSnapshotAsync();

        var lessons = new List<TeacherLessonOptionViewModel>();

        foreach (var doc in snapshot.Documents)
        {
            var data = doc.ToDictionary();

            lessons.Add(new TeacherLessonOptionViewModel
            {
                Id = doc.Id,
                Name = GetValue(data, "name", "-"),
                ClassName = GetValue(data, "className", "-")
            });
        }

        ViewBag.TeacherLessons = lessons
            .OrderBy(x => x.ClassName)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private async Task<DocumentSnapshot?> GetTeacherLesson(
        string lessonId,
        string teacherId
    )
    {
        if (string.IsNullOrWhiteSpace(lessonId))
        {
            return null;
        }

        var doc = await _firestore.Lessons
            .Document(lessonId)
            .GetSnapshotAsync();

        if (!doc.Exists)
        {
            return null;
        }

        var data = doc.ToDictionary();

        if (GetValue(data, "teacherId") != teacherId)
        {
            return null;
        }

        return doc;
    }

    private async Task<string> GetTeacherBranch(string teacherId)
    {
        if (string.IsNullOrWhiteSpace(teacherId))
        {
            return "";
        }

        var doc = await _firestore.Users
            .Document(teacherId)
            .GetSnapshotAsync();

        if (!doc.Exists)
        {
            return "";
        }

        var data = doc.ToDictionary();

        return GetValue(data, "branch", "Branş yok");
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