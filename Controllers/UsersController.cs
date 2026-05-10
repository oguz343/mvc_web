using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using mvc_web.Models;
using mvc_web.Services;

namespace mvc_web.Controllers;

public class UsersController : Controller
{
    private readonly FirestoreService _firestore;
    private readonly SessionService _session;

    public UsersController(
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

        ViewData["Title"] = "Kullanıcılar";
        ViewData["PageTitle"] = "Kullanıcılar";
        ViewData["PageSubtitle"] = "Öğrenci, öğretmen ve veli hesaplarını yönetin.";

        var snapshot = await _firestore.Users
            .OrderByDescending("createdAt")
            .GetSnapshotAsync();

        var users = new List<UserViewModel>();

        foreach (var doc in snapshot.Documents)
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
            else if (role == "Öğrenci" && !string.IsNullOrWhiteSpace(className))
            {
                detail = $"Sınıf: {className}";
            }
            else if (role == "Veli" && !string.IsNullOrWhiteSpace(linkedStudentNo))
            {
                detail = $"Bağlı öğrenci: {linkedStudentNo}";
            }

            users.Add(new UserViewModel
            {
                Id = doc.Id,
                Name = GetValue(data, "name", "-"),
                Role = role,
                SchoolNo = GetValue(data, "schoolNo", "-"),
                Tc = GetValue(data, "tc"),
                Phone = GetValue(data, "phone"),
                ClassName = className,
                LinkedStudentNo = linkedStudentNo,
                Branch = branch,
                ActivationCode = GetValue(data, "activationCode"),
                MustChangePassword = GetBool(data, "mustChangePassword", true),
                Detail = detail
            });
        }

        return View(users);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!_session.IsAdmin(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        ViewData["Title"] = "Kullanıcı Ekle";
        ViewData["PageTitle"] = "Yeni Kullanıcı";
        ViewData["PageSubtitle"] = "Öğrenci, öğretmen veya veli hesabı oluşturun.";

        await LoadClassOptions();

        return View(new UserViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserViewModel model)
    {
        if (!_session.IsAdmin(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        await LoadClassOptions();

        ValidateUserByRole(model);

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Kullanıcı Ekle";
            ViewData["PageTitle"] = "Yeni Kullanıcı";
            ViewData["PageSubtitle"] = "Öğrenci, öğretmen veya veli hesabı oluşturun.";
            return View(model);
        }

        var activationCode = GenerateActivationCode();

        await _firestore.Users.AddAsync(new Dictionary<string, object?>
        {
            { "name", model.Name.Trim() },
            { "role", model.Role.Trim() },
            { "schoolNo", model.SchoolNo.Trim() },
            { "tc", model.Tc.Trim() },
            { "phone", model.Phone?.Trim() ?? "" },
            { "className", model.Role == "Öğrenci" ? model.ClassName.Trim() : "" },
            { "linkedStudentNo", model.Role == "Veli" ? model.LinkedStudentNo.Trim() : "" },
            { "branch", model.Role == "Öğretmen" ? model.Branch.Trim() : "" },
            { "activationCode", activationCode },
            { "mustChangePassword", true },
            { "password", "" },
            { "createdAt", Timestamp.GetCurrentTimestamp() },
            { "updatedAt", Timestamp.GetCurrentTimestamp() }
        });

        TempData["Success"] = $"Kullanıcı oluşturuldu. Aktivasyon kodu: {activationCode}";
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

        var doc = await _firestore.Users.Document(id).GetSnapshotAsync();

        if (!doc.Exists)
        {
            TempData["Error"] = "Kullanıcı bulunamadı.";
            return RedirectToAction("Index");
        }

        var data = doc.ToDictionary();

        var model = new UserViewModel
        {
            Id = doc.Id,
            Name = GetValue(data, "name"),
            Role = GetValue(data, "role", "Öğrenci"),
            SchoolNo = GetValue(data, "schoolNo"),
            Tc = GetValue(data, "tc"),
            Phone = GetValue(data, "phone"),
            ClassName = GetValue(data, "className"),
            LinkedStudentNo = GetValue(data, "linkedStudentNo"),
            Branch = GetValue(data, "branch"),
            ActivationCode = GetValue(data, "activationCode"),
            MustChangePassword = GetBool(data, "mustChangePassword", true)
        };

        ViewData["Title"] = "Kullanıcı Düzenle";
        ViewData["PageTitle"] = "Kullanıcı Düzenle";
        ViewData["PageSubtitle"] = $"{model.Name} hesabını güncelleyin.";

        await LoadClassOptions();

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserViewModel model)
    {
        if (!_session.IsAdmin(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        await LoadClassOptions();

        ValidateUserByRole(model);

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Kullanıcı Düzenle";
            ViewData["PageTitle"] = "Kullanıcı Düzenle";
            ViewData["PageSubtitle"] = $"{model.Name} hesabını güncelleyin.";
            return View(model);
        }

        var userRef = _firestore.Users.Document(model.Id);
        var userDoc = await userRef.GetSnapshotAsync();

        if (!userDoc.Exists)
        {
            TempData["Error"] = "Kullanıcı bulunamadı.";
            return RedirectToAction("Index");
        }

        await userRef.UpdateAsync(new Dictionary<string, object?>
        {
            { "name", model.Name.Trim() },
            { "role", model.Role.Trim() },
            { "schoolNo", model.SchoolNo.Trim() },
            { "tc", model.Tc.Trim() },
            { "phone", model.Phone?.Trim() ?? "" },
            { "className", model.Role == "Öğrenci" ? model.ClassName.Trim() : "" },
            { "linkedStudentNo", model.Role == "Veli" ? model.LinkedStudentNo.Trim() : "" },
            { "branch", model.Role == "Öğretmen" ? model.Branch.Trim() : "" },
            { "updatedAt", Timestamp.GetCurrentTimestamp() }
        });

        TempData["Success"] = "Kullanıcı güncellendi.";
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

        await _firestore.Users.Document(id).DeleteAsync();

        TempData["Success"] = "Kullanıcı silindi.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenewCode(string id)
    {
        if (!_session.IsAdmin(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            return RedirectToAction("Index");
        }

        var code = GenerateActivationCode();

        await _firestore.Users.Document(id).UpdateAsync(new Dictionary<string, object?>
        {
            { "activationCode", code },
            { "mustChangePassword", true },
            { "password", "" },
            { "passwordUpdatedAt", null },
            { "updatedAt", Timestamp.GetCurrentTimestamp() }
        });

        TempData["Success"] = $"Yeni aktivasyon kodu: {code}";
        return RedirectToAction("Index");
    }

    private void ValidateUserByRole(UserViewModel model)
    {
        if (model.Role == "Öğrenci" && string.IsNullOrWhiteSpace(model.ClassName))
        {
            ModelState.AddModelError(nameof(model.ClassName), "Öğrenci için sınıf seçilmelidir.");
        }

        if (model.Role == "Öğretmen" && string.IsNullOrWhiteSpace(model.Branch))
        {
            ModelState.AddModelError(nameof(model.Branch), "Öğretmen için branş girilmelidir.");
        }

        if (model.Role == "Veli" && string.IsNullOrWhiteSpace(model.LinkedStudentNo))
        {
            ModelState.AddModelError(nameof(model.LinkedStudentNo), "Veli için bağlı öğrenci numarası girilmelidir.");
        }

        if (model.Role != "Öğrenci" && !string.IsNullOrWhiteSpace(model.Phone) && model.Phone.Trim().Length != 10)
        {
            ModelState.AddModelError(nameof(model.Phone), "Telefon numarası 10 haneli olmalıdır. Örn: 5551234567");
        }
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

    private static string GenerateActivationCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();

        return new string(
            Enumerable
                .Repeat(chars, 6)
                .Select(s => s[random.Next(s.Length)])
                .ToArray()
        );
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

    private static bool GetBool(
        Dictionary<string, object> data,
        string key,
        bool defaultValue = false
    )
    {
        if (!data.ContainsKey(key) || data[key] == null)
        {
            return defaultValue;
        }

        if (data[key] is bool boolValue)
        {
            return boolValue;
        }

        return bool.TryParse(data[key].ToString(), out var result)
            ? result
            : defaultValue;
    }
}