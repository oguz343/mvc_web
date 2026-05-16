using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using mvc_web.Models;
using mvc_web.Services;
using mvc_web.Filters;
namespace mvc_web.Controllers
{
    [AdminOnly]
    public class AdminAccountController : Controller
    {
        private readonly FirestoreDb _firestore;
        private readonly SessionService _session;

        public AdminAccountController(
            FirestoreDb firestore,
            SessionService session
        )
        {
            _firestore = firestore;
            _session = session;
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            if (!_session.IsAdmin(HttpContext))
            {
                return RedirectToAction("Login", "Auth");
            }

            return View(new AdminPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(AdminPasswordViewModel model)
        {
            if (!_session.IsAdmin(HttpContext))
            {
                return RedirectToAction("Login", "Auth");
            }

            ModelState.Clear();

            model.CurrentPassword = model.CurrentPassword?.Trim() ?? "";
            model.NewPassword = model.NewPassword?.Trim() ?? "";
            model.RepeatPassword = model.RepeatPassword?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(model.CurrentPassword))
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "Mevcut şifre boş bırakılamaz.");
            }

            if (string.IsNullOrWhiteSpace(model.NewPassword))
            {
                ModelState.AddModelError(nameof(model.NewPassword), "Yeni şifre boş bırakılamaz.");
            }

            if (model.NewPassword.Length < 6)
            {
                ModelState.AddModelError(nameof(model.NewPassword), "Yeni şifre en az 6 karakter olmalıdır.");
            }

            if (model.NewPassword != model.RepeatPassword)
            {
                ModelState.AddModelError(nameof(model.RepeatPassword), "Yeni şifreler eşleşmiyor.");
            }

            var currentAdminPassword = await GetAdminPassword();

            if (model.CurrentPassword != currentAdminPassword)
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "Mevcut şifre hatalı.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var now = Timestamp.GetCurrentTimestamp();

            await _firestore
                .Collection("system")
                .Document("admin_account")
                .SetAsync(
                    new Dictionary<string, object>
                    {
                        { "number", "0000" },
                        { "password", model.NewPassword },
                        { "updatedAt", now }
                    },
                    SetOptions.MergeAll
                );

            TempData["Success"] = "Admin şifresi başarıyla güncellendi.";
            return RedirectToAction(nameof(ChangePassword));
        }

        private async Task<string> GetAdminPassword()
        {
            var doc = await _firestore
                .Collection("system")
                .Document("admin_account")
                .GetSnapshotAsync();

            if (!doc.Exists)
            {
                return "admin123";
            }

            var data = doc.ToDictionary();

            if (data.TryGetValue("password", out var value) && value != null)
            {
                var password = value.ToString();

                if (!string.IsNullOrWhiteSpace(password))
                {
                    return password;
                }
            }

            return "admin123";
        }
    }
}