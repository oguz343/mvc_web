using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using mvc_web.Filters;
using mvc_web.Models;
using mvc_web.Services;

namespace mvc_web.Controllers
{
    [AdminOnly]
    public class AdminAccountController : Controller
    {
        private readonly FirestoreDb _firestore;
        private readonly IWebHostEnvironment _environment;

        public AdminAccountController(
            FirestoreDb firestore,
            IWebHostEnvironment environment
        )
        {
            _firestore = firestore;
            _environment = environment;
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View(new AdminPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(AdminPasswordViewModel model)
        {
            ModelState.Clear();

            model.CurrentPassword = model.CurrentPassword?.Trim() ?? "";
            model.NewPassword = model.NewPassword?.Trim() ?? "";
            model.RepeatPassword = model.RepeatPassword?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(model.CurrentPassword))
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "Mevcut sifre bos birakilamaz.");
            }

            if (string.IsNullOrWhiteSpace(model.NewPassword))
            {
                ModelState.AddModelError(nameof(model.NewPassword), "Yeni sifre bos birakilamaz.");
            }

            if (model.NewPassword.Length < 6)
            {
                ModelState.AddModelError(nameof(model.NewPassword), "Yeni sifre en az 6 karakter olmalidir.");
            }

            if (model.NewPassword != model.RepeatPassword)
            {
                ModelState.AddModelError(nameof(model.RepeatPassword), "Yeni sifreler eslesmiyor.");
            }

            var adminDoc = await FindAdminUserAsync();

            if (adminDoc == null)
            {
                ModelState.AddModelError("", "Admin kullanicisi bulunamadi.");
            }
            else if (!await VerifyCurrentPasswordAsync(adminDoc, model.CurrentPassword))
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "Mevcut sifre hatali.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var now = Timestamp.GetCurrentTimestamp();
            var newHash = PasswordHashService.HashPassword(model.NewPassword);

            await adminDoc!.Reference.SetAsync(
                new Dictionary<string, object?>
                {
                    ["passwordHash"] = newHash,
                    ["PasswordHash"] = newHash,
                    ["password"] = "",
                    ["Password"] = "",
                    ["mustChangePassword"] = false,
                    ["MustChangePassword"] = false,
                    ["updatedAt"] = now,
                    ["UpdatedAt"] = now,
                },
                SetOptions.MergeAll
            );

            await _firestore
                .Collection("system")
                .Document("admin_account")
                .SetAsync(
                    new Dictionary<string, object?>
                    {
                        ["number"] = "0000",
                        ["password"] = "",
                        ["passwordHash"] = newHash,
                        ["updatedAt"] = now,
                    },
                    SetOptions.MergeAll
                );

            TempData["Success"] = "Admin sifresi basariyla guncellendi.";
            return RedirectToAction(nameof(ChangePassword));
        }

        private async Task<bool> VerifyCurrentPasswordAsync(DocumentSnapshot adminDoc, string password)
        {
            var data = adminDoc.ToDictionary();
            var passwordHash = GetString(data, "passwordHash", "PasswordHash", "hash", "Hash");
            var legacyPassword = GetString(data, "password", "Password");

            if (PasswordHashService.IsHash(passwordHash))
            {
                return PasswordHashService.VerifyPassword(password, passwordHash)
                    || await VerifySystemAdminPasswordAsync(password);
            }

            if (!string.IsNullOrWhiteSpace(legacyPassword))
            {
                return legacyPassword == password
                    || await VerifySystemAdminPasswordAsync(password);
            }

            return await VerifySystemAdminPasswordAsync(password);
        }

        private async Task<DocumentSnapshot?> FindAdminUserAsync()
        {
            var users = await _firestore.Collection("users").GetSnapshotAsync();

            foreach (var doc in users.Documents)
            {
                if (!doc.Exists)
                {
                    continue;
                }

                var data = doc.ToDictionary();
                var role = NormalizeKey(GetString(data, "role", "Role", "userRole", "UserRole"));
                var number = OnlyDigits(GetString(data, "number", "Number", "schoolNo", "SchoolNo", "adminNo", "AdminNo"));

                if (role == "admin" || number == "0000")
                {
                    return doc;
                }
            }

            return null;
        }

        private async Task<string> GetSystemAdminPasswordAsync()
        {
            var doc = await _firestore
                .Collection("system")
                .Document("admin_account")
                .GetSnapshotAsync();

            if (!doc.Exists)
            {
                return IsDevelopment() ? "admin123" : "";
            }

            var data = doc.ToDictionary();
            return GetString(data, "passwordHash", "PasswordHash", "password", "Password", "adminPassword", "AdminPassword", "sifre", "Sifre", "ÅŸifre", "Åifre");
        }

        private async Task<bool> VerifySystemAdminPasswordAsync(string password)
        {
            var systemPassword = await GetSystemAdminPasswordAsync();

            if (string.IsNullOrWhiteSpace(systemPassword))
            {
                return false;
            }

            if (PasswordHashService.IsHash(systemPassword))
            {
                return PasswordHashService.VerifyPassword(password, systemPassword);
            }

            return systemPassword == password;
        }

        private bool IsDevelopment()
        {
            return _environment.IsDevelopment();
        }

        private static string GetString(Dictionary<string, object> data, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!data.TryGetValue(key, out var value) || value == null)
                {
                    continue;
                }

                var text = value.ToString();

                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text.Trim();
                }
            }

            return "";
        }

        private static string OnlyDigits(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? ""
                : new string(value.Where(char.IsDigit).ToArray());
        }

        private static string NormalizeKey(string value)
        {
            value = (value ?? "").Trim().ToLowerInvariant();

            value = value
                .Replace("Ä±", "i")
                .Replace("ÄŸ", "g")
                .Replace("Ã¼", "u")
                .Replace("ÅŸ", "s")
                .Replace("Ã¶", "o")
                .Replace("Ã§", "c");

            return new string(value.Where(char.IsLetterOrDigit).ToArray());
        }
    }
}
