using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using mvc_web.Models;
using mvc_web.Services;

namespace mvc_web.Controllers;

public class ClassesController : Controller
{
    private readonly FirestoreService _firestore;
    private readonly SessionService _session;

    public ClassesController(
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

        ViewData["Title"] = "Sınıflar";
        ViewData["PageTitle"] = "Sınıflar";
        ViewData["PageSubtitle"] = "Okuldaki sınıf ve şube bilgilerini yönetin.";

        var snapshot = await _firestore.Classes
            .OrderByDescending("createdAt")
            .GetSnapshotAsync();

        var classes = new List<ClassViewModel>();

        foreach (var doc in snapshot.Documents)
        {
            var data = doc.ToDictionary();

            classes.Add(new ClassViewModel
            {
                Id = doc.Id,
                Name = GetValue(data, "name", "-"),
                Grade = GetValue(data, "grade", "-"),
                Branch = GetValue(data, "branch", "-"),
                TeacherId = GetValue(data, "teacherId"),
                Teacher = GetValue(data, "teacher", "Atanmadı"),
                TeacherBranch = GetValue(data, "teacherBranch", "Branş yok"),
                Capacity = GetInt(data, "capacity"),
                StudentCount = GetInt(data, "studentCount")
            });
        }

        return View(classes);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!_session.IsAdmin(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        ViewData["Title"] = "Sınıf Ekle";
        ViewData["PageTitle"] = "Yeni Sınıf";
        ViewData["PageSubtitle"] = "Sınıf, şube, kapasite ve sınıf öğretmeni belirleyin.";

        await LoadTeacherOptions();

        return View(new ClassViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ClassViewModel model)
    {
        if (!_session.IsAdmin(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        await LoadTeacherOptions();

        await FillTeacherInfo(model);
        ValidateClass(model);

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Sınıf Ekle";
            ViewData["PageTitle"] = "Yeni Sınıf";
            ViewData["PageSubtitle"] = "Sınıf, şube, kapasite ve sınıf öğretmeni belirleyin.";
            return View(model);
        }

        var className = $"{model.Grade}-{model.Branch}";

        await _firestore.Classes.AddAsync(new Dictionary<string, object?>
        {
            { "name", className },
            { "grade", model.Grade.Trim() },
            { "branch", model.Branch.Trim() },
            { "teacherId", model.TeacherId.Trim() },
            { "teacher", model.Teacher.Trim() },
            { "teacherBranch", string.IsNullOrWhiteSpace(model.TeacherBranch) ? "Branş yok" : model.TeacherBranch.Trim() },
            { "capacity", model.Capacity },
            { "studentCount", model.StudentCount },
            { "createdAt", Timestamp.GetCurrentTimestamp() },
            { "updatedAt", Timestamp.GetCurrentTimestamp() }
        });

        TempData["Success"] = $"{className} sınıfı oluşturuldu.";
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

        var doc = await _firestore.Classes.Document(id).GetSnapshotAsync();

        if (!doc.Exists)
        {
            TempData["Error"] = "Sınıf bulunamadı.";
            return RedirectToAction("Index");
        }

        var data = doc.ToDictionary();

        var model = new ClassViewModel
        {
            Id = doc.Id,
            Name = GetValue(data, "name"),
            Grade = GetValue(data, "grade", "9"),
            Branch = GetValue(data, "branch", "A"),
            TeacherId = GetValue(data, "teacherId"),
            Teacher = GetValue(data, "teacher"),
            TeacherBranch = GetValue(data, "teacherBranch"),
            Capacity = GetInt(data, "capacity", 30),
            StudentCount = GetInt(data, "studentCount", 0)
        };

        ViewData["Title"] = "Sınıf Düzenle";
        ViewData["PageTitle"] = "Sınıf Düzenle";
        ViewData["PageSubtitle"] = $"{model.Name} sınıfını güncelleyin.";

        await LoadTeacherOptions();

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ClassViewModel model)
    {
        if (!_session.IsAdmin(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        await LoadTeacherOptions();

        await FillTeacherInfo(model);
        ValidateClass(model);

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Sınıf Düzenle";
            ViewData["PageTitle"] = "Sınıf Düzenle";
            ViewData["PageSubtitle"] = "Sınıf bilgilerini güncelleyin.";
            return View(model);
        }

        var classRef = _firestore.Classes.Document(model.Id);
        var classDoc = await classRef.GetSnapshotAsync();

        if (!classDoc.Exists)
        {
            TempData["Error"] = "Sınıf bulunamadı.";
            return RedirectToAction("Index");
        }

        var className = $"{model.Grade}-{model.Branch}";

        await classRef.UpdateAsync(new Dictionary<string, object?>
        {
            { "name", className },
            { "grade", model.Grade.Trim() },
            { "branch", model.Branch.Trim() },
            { "teacherId", model.TeacherId.Trim() },
            { "teacher", model.Teacher.Trim() },
            { "teacherBranch", string.IsNullOrWhiteSpace(model.TeacherBranch) ? "Branş yok" : model.TeacherBranch.Trim() },
            { "capacity", model.Capacity },
            { "studentCount", model.StudentCount },
            { "updatedAt", Timestamp.GetCurrentTimestamp() }
        });

        TempData["Success"] = "Sınıf güncellendi.";
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

        await _firestore.Classes.Document(id).DeleteAsync();

        TempData["Success"] = "Sınıf silindi.";
        return RedirectToAction("Index");
    }

    private void ValidateClass(ClassViewModel model)
    {
        if (model.StudentCount > model.Capacity)
        {
            ModelState.AddModelError(
                nameof(model.StudentCount),
                "Öğrenci sayısı kapasiteden büyük olamaz."
            );
        }

        if (string.IsNullOrWhiteSpace(model.TeacherId))
        {
            ModelState.AddModelError(
                nameof(model.TeacherId),
                "Sınıf öğretmeni seçilmelidir."
            );
        }

        if (string.IsNullOrWhiteSpace(model.Teacher))
        {
            ModelState.AddModelError(
                nameof(model.TeacherId),
                "Seçilen öğretmen bulunamadı."
            );
        }
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

    private async Task FillTeacherInfo(ClassViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.TeacherId))
        {
            model.Teacher = "";
            model.TeacherBranch = "";
            return;
        }

        var teacherDoc = await _firestore.Users
            .Document(model.TeacherId)
            .GetSnapshotAsync();

        if (!teacherDoc.Exists)
        {
            model.Teacher = "";
            model.TeacherBranch = "";
            return;
        }

        var data = teacherDoc.ToDictionary();

        model.Teacher = GetValue(data, "name", "İsimsiz");
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

    private static int GetInt(
        Dictionary<string, object> data,
        string key,
        int defaultValue = 0
    )
    {
        if (!data.ContainsKey(key) || data[key] == null)
        {
            return defaultValue;
        }

        return int.TryParse(data[key].ToString(), out var value)
            ? value
            : defaultValue;
    }
}