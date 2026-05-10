using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using mvc_web.Models;
using mvc_web.Services;

namespace mvc_web.Controllers;

public class LessonsController : Controller
{
    private readonly FirestoreService _firestore;
    private readonly SessionService _session;

    public LessonsController(
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

        ViewData["Title"] = "Dersler";
        ViewData["PageTitle"] = "Dersler";
        ViewData["PageSubtitle"] = "Dersleri sınıf ve öğretmenlerle eşleştirin.";

        var snapshot = await _firestore.Lessons
            .OrderByDescending("createdAt")
            .GetSnapshotAsync();

        var lessons = new List<LessonViewModel>();

        foreach (var doc in snapshot.Documents)
        {
            var data = doc.ToDictionary();

            lessons.Add(new LessonViewModel
            {
                Id = doc.Id,
                Name = GetValue(data, "name", "-"),
                ClassName = GetValue(data, "className", "-"),
                TeacherId = GetValue(data, "teacherId"),
                TeacherName = GetValue(data, "teacherName", GetValue(data, "teacher", "Atanmadı")),
                TeacherBranch = GetValue(data, "teacherBranch", "Branş yok")
            });
        }

        return View(lessons);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!_session.IsAdmin(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        ViewData["Title"] = "Ders Ekle";
        ViewData["PageTitle"] = "Yeni Ders";
        ViewData["PageSubtitle"] = "Dersi bir sınıfa ve öğretmene atayın.";

        await LoadOptions();

        return View(new LessonViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LessonViewModel model)
    {
        if (!_session.IsAdmin(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        await LoadOptions();
        await FillTeacherInfo(model);

        if (string.IsNullOrWhiteSpace(model.TeacherName))
        {
            ModelState.AddModelError(nameof(model.TeacherId), "Seçilen öğretmen bulunamadı.");
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Ders Ekle";
            ViewData["PageTitle"] = "Yeni Ders";
            ViewData["PageSubtitle"] = "Dersi bir sınıfa ve öğretmene atayın.";
            return View(model);
        }

        await _firestore.Lessons.AddAsync(new Dictionary<string, object?>
        {
            { "name", model.Name.Trim() },
            { "className", model.ClassName.Trim() },
            { "teacherId", model.TeacherId.Trim() },
            { "teacherName", model.TeacherName.Trim() },
            { "teacher", model.TeacherName.Trim() },
            { "teacherBranch", string.IsNullOrWhiteSpace(model.TeacherBranch) ? "Branş yok" : model.TeacherBranch.Trim() },
            { "createdAt", Timestamp.GetCurrentTimestamp() },
            { "updatedAt", Timestamp.GetCurrentTimestamp() }
        });

        TempData["Success"] = $"{model.Name} dersi oluşturuldu.";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        if (!_session.IsAdmin(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            return RedirectToAction("Index");
        }

        var doc = await _firestore.Lessons.Document(id).GetSnapshotAsync();

        if (!doc.Exists)
        {
            TempData["Error"] = "Ders bulunamadı.";
            return RedirectToAction("Index");
        }

        var data = doc.ToDictionary();

        var model = new LessonViewModel
        {
            Id = doc.Id,
            Name = GetValue(data, "name"),
            ClassName = GetValue(data, "className"),
            TeacherId = GetValue(data, "teacherId"),
            TeacherName = GetValue(data, "teacherName", GetValue(data, "teacher")),
            TeacherBranch = GetValue(data, "teacherBranch")
        };

        ViewData["Title"] = "Ders Düzenle";
        ViewData["PageTitle"] = "Ders Düzenle";
        ViewData["PageSubtitle"] = $"{model.Name} dersini güncelleyin.";

        await LoadOptions();

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(LessonViewModel model)
    {
        if (!_session.IsAdmin(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        await LoadOptions();
        await FillTeacherInfo(model);

        if (string.IsNullOrWhiteSpace(model.TeacherName))
        {
            ModelState.AddModelError(nameof(model.TeacherId), "Seçilen öğretmen bulunamadı.");
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Ders Düzenle";
            ViewData["PageTitle"] = "Ders Düzenle";
            ViewData["PageSubtitle"] = "Ders bilgilerini güncelleyin.";
            return View(model);
        }

        var lessonRef = _firestore.Lessons.Document(model.Id);
        var lessonDoc = await lessonRef.GetSnapshotAsync();

        if (!lessonDoc.Exists)
        {
            TempData["Error"] = "Ders bulunamadı.";
            return RedirectToAction("Index");
        }

        await lessonRef.UpdateAsync(new Dictionary<string, object?>
        {
            { "name", model.Name.Trim() },
            { "className", model.ClassName.Trim() },
            { "teacherId", model.TeacherId.Trim() },
            { "teacherName", model.TeacherName.Trim() },
            { "teacher", model.TeacherName.Trim() },
            { "teacherBranch", string.IsNullOrWhiteSpace(model.TeacherBranch) ? "Branş yok" : model.TeacherBranch.Trim() },
            { "updatedAt", Timestamp.GetCurrentTimestamp() }
        });

        TempData["Success"] = "Ders güncellendi.";
        return RedirectToAction("Index");
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

        await _firestore.Lessons.Document(id).DeleteAsync();

        TempData["Success"] = "Ders silindi.";
        return RedirectToAction("Index");
    }

    private async Task LoadOptions()
    {
        await LoadClassOptions();
        await LoadTeacherOptions();
    }

    private async Task LoadClassOptions()
    {
        var snapshot = await _firestore.Classes.GetSnapshotAsync();

        var classes = snapshot.Documents
            .Select(doc =>
            {
                var data = doc.ToDictionary();
                return GetValue(data, "name");
            })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        ViewBag.Classes = classes;
    }

    private async Task LoadTeacherOptions()
    {
        var snapshot = await _firestore.Users
            .WhereEqualTo("role", "Öğretmen")
            .GetSnapshotAsync();

        var teachers = new List<TeacherOptionViewModel>();

        foreach (var doc in snapshot.Documents)
        {
            var data = doc.ToDictionary();

            teachers.Add(new TeacherOptionViewModel
            {
                Id = doc.Id,
                Name = GetValue(data, "name", "İsimsiz"),
                Branch = GetValue(data, "branch", "Branş yok")
            });
        }

        ViewBag.Teachers = teachers
            .OrderBy(x => x.Name)
            .ToList();
    }

    private async Task FillTeacherInfo(LessonViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.TeacherId))
        {
            model.TeacherName = "";
            model.TeacherBranch = "";
            return;
        }

        var teacherDoc = await _firestore.Users
            .Document(model.TeacherId)
            .GetSnapshotAsync();

        if (!teacherDoc.Exists)
        {
            model.TeacherName = "";
            model.TeacherBranch = "";
            return;
        }

        var data = teacherDoc.ToDictionary();

        model.TeacherName = GetValue(data, "name", "İsimsiz");
        model.TeacherBranch = GetValue(data, "branch", "Branş yok");
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