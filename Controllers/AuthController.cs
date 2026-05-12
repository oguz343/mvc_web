using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using mvc_web.Models;
using mvc_web.Services;

namespace mvc_web.Controllers
{
    public class AuthController : Controller
    {
        private readonly FirestoreDb _firestore;
        private readonly SessionService _session;

        public AuthController(
            FirestoreDb firestore,
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
                return RedirectByRole(_session.GetRole(HttpContext) ?? "");
            }

            return View(new LoginViewModel
            {
                Role = "Admin"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Role))
            {
                ModelState.AddModelError("", "Rol seçilmelidir.");
            }

            if (string.IsNullOrWhiteSpace(model.Number))
            {
                ModelState.AddModelError("", "Numara boş bırakılamaz.");
            }

            if (string.IsNullOrWhiteSpace(model.Password))
            {
                ModelState.AddModelError("", "Şifre veya aktivasyon kodu boş bırakılamaz.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var role = model.Role.Trim();
            var number = OnlyDigits(model.Number);
            var password = model.Password.Trim();

            if (role == "Admin")
            {
                var admin = await GetAdminCredentials();

                if (number == admin.Number && password == admin.Password)
                {
                    await EnsureAdminDocumentExists(admin.Password);

                    _session.Login(
                        HttpContext,
                        "admin",
                        "Admin",
                        "Admin",
                        admin.Number
                    );

                    return RedirectToAction("Index", "Dashboard");
                }

                ModelState.AddModelError("", "Admin bilgileri hatalı.");
                return View(model);
            }

            var userDoc = await FindUser(role, number);

            if (userDoc == null)
            {
                ModelState.AddModelError("", $"{role} rolünde {number} numaralı kullanıcı bulunamadı.");
                return View(model);
            }

            var data = userDoc.ToDictionary();

            var name = GetString(data, "name", "Name");
            var savedPassword = GetString(data, "password", "Password");
            var activationCode = GetString(data, "activationCode", "ActivationCode");
            var mustChangePassword = GetBool(data, "mustChangePassword", "MustChangePassword");

            if (mustChangePassword)
            {
                if (string.IsNullOrWhiteSpace(activationCode))
                {
                    ModelState.AddModelError("", "Bu kullanıcı için aktivasyon kodu yok. Admin ile iletişime geçin.");
                    return View(model);
                }

                if (password != activationCode)
                {
                    ModelState.AddModelError("", "Aktivasyon kodu hatalı.");
                    return View(model);
                }

                TempData["SetPasswordUserId"] = userDoc.Id;
                TempData["SetPasswordName"] = string.IsNullOrWhiteSpace(name) ? role : name;
                TempData["SetPasswordRole"] = role;
                TempData["SetPasswordNumber"] = number;

                return RedirectToAction(nameof(SetPassword));
            }

            if (string.IsNullOrWhiteSpace(savedPassword))
            {
                ModelState.AddModelError("", "Bu kullanıcı için şifre oluşturulmamış. Aktivasyon kodu ile giriş yapın.");
                return View(model);
            }

            if (password != savedPassword)
            {
                ModelState.AddModelError("", "Şifre hatalı.");
                return View(model);
            }

            _session.Login(
                HttpContext,
                userDoc.Id,
                string.IsNullOrWhiteSpace(name) ? role : name,
                role,
                number
            );

            return RedirectByRole(role);
        }

        [HttpGet]
        public IActionResult SetPassword()
        {
            var userId = TempData["SetPasswordUserId"]?.ToString();
            var name = TempData["SetPasswordName"]?.ToString();
            var role = TempData["SetPasswordRole"]?.ToString();
            var number = TempData["SetPasswordNumber"]?.ToString();

            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["Info"] = "Şifre oluşturmak için önce aktivasyon kodu ile giriş yapmalısınız.";
                return RedirectToAction(nameof(Login));
            }

            TempData.Keep("SetPasswordUserId");
            TempData.Keep("SetPasswordName");
            TempData.Keep("SetPasswordRole");
            TempData.Keep("SetPasswordNumber");

            ViewBag.Name = name ?? "Kullanıcı";
            ViewBag.Role = role ?? "";
            ViewBag.Number = number ?? "";

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPassword(string password, string repeatPassword)
        {
            var userId = TempData["SetPasswordUserId"]?.ToString();
            var name = TempData["SetPasswordName"]?.ToString();
            var role = TempData["SetPasswordRole"]?.ToString();
            var number = TempData["SetPasswordNumber"]?.ToString();

            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["Info"] = "Şifre oluşturmak için önce aktivasyon kodu ile giriş yapmalısınız.";
                return RedirectToAction(nameof(Login));
            }

            if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
            {
                TempData.Keep();

                ViewBag.Name = name ?? "Kullanıcı";
                ViewBag.Role = role ?? "";
                ViewBag.Number = number ?? "";

                ModelState.AddModelError("", "Şifre en az 4 karakter olmalıdır.");
                return View();
            }

            if (password != repeatPassword)
            {
                TempData.Keep();

                ViewBag.Name = name ?? "Kullanıcı";
                ViewBag.Role = role ?? "";
                ViewBag.Number = number ?? "";

                ModelState.AddModelError("", "Şifreler eşleşmiyor.");
                return View();
            }

            await _firestore.Collection("users").Document(userId).UpdateAsync(new Dictionary<string, object>
            {
                { "password", password.Trim() },
                { "activationCode", "" },
                { "mustChangePassword", false },
                { "passwordUpdatedAt", Timestamp.GetCurrentTimestamp() },
                { "updatedAt", Timestamp.GetCurrentTimestamp() }
            });

            _session.Login(
                HttpContext,
                userId,
                name ?? "Kullanıcı",
                role ?? "",
                number ?? ""
            );

            return RedirectByRole(role ?? "");
        }

        public IActionResult Logout()
        {
            _session.Logout(HttpContext);
            return RedirectToAction(nameof(Login));
        }

        private async Task<(string Number, string Password)> GetAdminCredentials()
        {
            var doc = await _firestore
                .Collection("system")
                .Document("admin_account")
                .GetSnapshotAsync();

            if (!doc.Exists)
            {
                return ("0000", "admin123");
            }

            var data = doc.ToDictionary();

            var number = GetString(data, "number", "Number");
            var password = GetString(data, "password", "Password");

            if (string.IsNullOrWhiteSpace(number))
            {
                number = "0000";
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                password = "admin123";
            }

            return (number, password);
        }

        private async Task EnsureAdminDocumentExists(string password)
        {
            var docRef = _firestore
                .Collection("system")
                .Document("admin_account");

            var doc = await docRef.GetSnapshotAsync();

            if (doc.Exists)
            {
                return;
            }

            await docRef.SetAsync(new Dictionary<string, object>
            {
                { "number", "0000" },
                { "password", password },
                { "createdAt", Timestamp.GetCurrentTimestamp() },
                { "updatedAt", Timestamp.GetCurrentTimestamp() }
            });
        }

        private async Task<DocumentSnapshot?> FindUser(string role, string number)
        {
            var snapshot = await _firestore
                .Collection("users")
                .WhereEqualTo("role", role)
                .GetSnapshotAsync();

            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();

                var schoolNo = OnlyDigits(
                    GetString(data, "schoolNo", "SchoolNo", "number", "Number")
                );

                if (schoolNo == number)
                {
                    return doc;
                }
            }

            return null;
        }

        private IActionResult RedirectByRole(string role)
        {
            if (role == "Admin")
            {
                return RedirectToAction("Index", "Dashboard");
            }

            if (role == "Öğretmen")
            {
                return RedirectToAction("Index", "Teacher");
            }

            if (role == "Öğrenci" || role == "Veli")
            {
                return RedirectToAction("Index", "Portal");
            }

            return RedirectToAction(nameof(Login));
        }

        private static string GetString(Dictionary<string, object> data, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (data.TryGetValue(key, out var value) && value != null)
                {
                    return value.ToString() ?? "";
                }
            }

            return "";
        }

        private static bool GetBool(Dictionary<string, object> data, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (data.TryGetValue(key, out var value) && value != null)
                {
                    if (value is bool boolValue)
                    {
                        return boolValue;
                    }

                    if (bool.TryParse(value.ToString(), out var parsed))
                    {
                        return parsed;
                    }
                }
            }

            return false;
        }

        private static string OnlyDigits(string value)
        {
            return new string((value ?? "").Where(char.IsDigit).ToArray());
        }
    }
}