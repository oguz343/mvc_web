using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using mvc_web.Models;
using mvc_web.Services;

namespace mvc_web.Controllers;

public class AuthController : Controller
{
    private readonly FirestoreService _firestore;
    private readonly SessionService _session;

    public AuthController(
        FirestoreService firestore,
        SessionService session
    )
    {
        _firestore = firestore;
        _session = session;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (_session.IsLoggedIn(HttpContext))
        {
            var role = _session.GetRole(HttpContext);

            if (role == "Admin")
            {
                return RedirectToAction("Index", "Dashboard");
            }

            if (role == "Öğretmen")
            {
                return RedirectToAction("Index", "Teacher");
            }
        }

        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var role = model.Role.Trim();
        var number = model.Number.Trim();
        var password = model.Password.Trim();

        if (role == "Öğrenci")
        {
            ModelState.AddModelError(
                "",
                "Öğrenci girişi Flutter uygulamasından yapılır. MVC web paneli sadece admin ve öğretmen içindir."
            );
            return View(model);
        }

        if (role == "Veli")
        {
            ModelState.AddModelError(
                "",
                "Veli girişi Flutter uygulamasından yapılır. MVC web paneli sadece admin ve öğretmen içindir."
            );
            return View(model);
        }

        if (role == "Admin")
        {
            if (number == "0000" && password == "admin123")
            {
                _session.Login(
                    HttpContext,
                    userId: "admin",
                    role: "Admin",
                    name: "Admin",
                    number: "0000"
                );

                return RedirectToAction("Index", "Dashboard");
            }

            ModelState.AddModelError("", "Admin bilgileri hatalı.");
            return View(model);
        }

        if (role != "Öğretmen")
        {
            ModelState.AddModelError("", "Geçersiz rol seçimi.");
            return View(model);
        }

        var snapshot = await _firestore.Users
            .WhereEqualTo("schoolNo", number)
            .WhereEqualTo("role", "Öğretmen")
            .Limit(1)
            .GetSnapshotAsync();

        if (snapshot.Documents.Count == 0)
        {
            ModelState.AddModelError("", "Bu bilgilere ait öğretmen bulunamadı.");
            return View(model);
        }

        var userDoc = snapshot.Documents.First();
        var data = userDoc.ToDictionary();

        var name = GetValue(data, "name", "Öğretmen");
        var activationCode = GetValue(data, "activationCode");
        var savedPassword = GetValue(data, "password");
        var mustChangePassword = GetBool(data, "mustChangePassword");

        if (mustChangePassword)
        {
            if (password != activationCode)
            {
                ModelState.AddModelError("", "Aktivasyon kodu hatalı.");
                return View(model);
            }

            return RedirectToAction(
                "CreatePassword",
                "Auth",
                new
                {
                    userId = userDoc.Id,
                    role = "Öğretmen",
                    name,
                    number
                }
            );
        }

        if (string.IsNullOrWhiteSpace(savedPassword))
        {
            ModelState.AddModelError(
                "",
                "Bu öğretmen hesabı için şifre oluşturulmamış. Aktivasyon kodu ile giriş yapın."
            );
            return View(model);
        }

        if (password != savedPassword)
        {
            ModelState.AddModelError("", "Şifre hatalı.");
            return View(model);
        }

        _session.Login(
            HttpContext,
            userId: userDoc.Id,
            role: "Öğretmen",
            name: name,
            number: number
        );

        return RedirectToAction("Index", "Teacher");
    }

    [HttpGet]
    public IActionResult CreatePassword(
        string userId,
        string role,
        string name,
        string number
    )
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return RedirectToAction("Login");
        }

        if (role != "Öğretmen")
        {
            TempData["Info"] = "MVC web panelinde sadece öğretmen şifre oluşturabilir.";
            return RedirectToAction("Login");
        }

        return View(new CreatePasswordViewModel
        {
            UserId = userId,
            Role = role,
            Name = name,
            Number = number
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePassword(CreatePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.Role != "Öğretmen")
        {
            ModelState.AddModelError("", "MVC web panelinde sadece öğretmen şifre oluşturabilir.");
            return View(model);
        }

        if (model.NewPassword != model.RepeatPassword)
        {
            ModelState.AddModelError("", "Şifreler eşleşmiyor.");
            return View(model);
        }

        var userRef = _firestore.Users.Document(model.UserId);
        var userDoc = await userRef.GetSnapshotAsync();

        if (!userDoc.Exists)
        {
            ModelState.AddModelError("", "Kullanıcı bulunamadı.");
            return View(model);
        }

        var data = userDoc.ToDictionary();
        var userRole = GetValue(data, "role");

        if (userRole != "Öğretmen")
        {
            ModelState.AddModelError("", "Bu hesap öğretmen hesabı değil.");
            return View(model);
        }

        await userRef.UpdateAsync(new Dictionary<string, object?>
        {
            { "password", model.NewPassword.Trim() },
            { "mustChangePassword", false },
            { "activationCode", "" },
            { "passwordUpdatedAt", Timestamp.GetCurrentTimestamp() },
            { "updatedAt", Timestamp.GetCurrentTimestamp() }
        });

        _session.Login(
            HttpContext,
            userId: model.UserId,
            role: "Öğretmen",
            name: model.Name,
            number: model.Number
        );

        return RedirectToAction("Index", "Teacher");
    }

    [HttpGet]
    public IActionResult Logout()
    {
        _session.Logout(HttpContext);
        return RedirectToAction("Login", "Auth");
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