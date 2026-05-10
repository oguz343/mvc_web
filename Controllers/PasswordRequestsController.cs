using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using mvc_web.Models;
using mvc_web.Services;

namespace mvc_web.Controllers;

public class PasswordRequestsController : Controller
{
    private readonly FirestoreService _firestore;
    private readonly SessionService _session;

    public PasswordRequestsController(
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

        ViewData["Title"] = "Şifre Talepleri";
        ViewData["PageTitle"] = "Şifre Talepleri";
        ViewData["PageSubtitle"] = "Şifre sıfırlama taleplerini onaylayın, reddedin veya silin.";

        var snapshot = await _firestore.PasswordRequests
            .OrderByDescending("createdAt")
            .GetSnapshotAsync();

        var requests = new List<PasswordRequestViewModel>();

        foreach (var doc in snapshot.Documents)
        {
            var data = doc.ToDictionary();

            requests.Add(new PasswordRequestViewModel
            {
                Id = doc.Id,
                Name = GetValue(data, "name", "Bilinmeyen Kullanıcı"),
                Number = GetValue(data, "number", GetValue(data, "schoolNo", "-")),
                Role = GetValue(data, "role", "Öğrenci"),
                Status = GetValue(data, "status", "Bekliyor"),
                Note = GetValue(data, "note", "Şifre sıfırlama talebi oluşturuldu.")
            });
        }

        return View(requests);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTest(PasswordRequestViewModel model)
    {
        if (!_session.IsAdmin(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            model.Name = "Test Kullanıcı";
        }

        if (string.IsNullOrWhiteSpace(model.Number))
        {
            model.Number = "0001";
        }

        if (string.IsNullOrWhiteSpace(model.Role))
        {
            model.Role = "Öğrenci";
        }

        await _firestore.PasswordRequests.AddAsync(new Dictionary<string, object?>
        {
            { "name", model.Name.Trim() },
            { "number", model.Number.Trim() },
            { "role", model.Role.Trim() },
            { "status", "Bekliyor" },
            { "note", string.IsNullOrWhiteSpace(model.Note) ? "Şifre sıfırlama talebi oluşturuldu." : model.Note.Trim() },
            { "createdAt", Timestamp.GetCurrentTimestamp() },
            { "updatedAt", Timestamp.GetCurrentTimestamp() }
        });

        TempData["Success"] = "Test şifre talebi oluşturuldu.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(string id)
    {
        if (!_session.IsAdmin(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            return RedirectToAction("Index");
        }

        await _firestore.PasswordRequests.Document(id).UpdateAsync(new Dictionary<string, object?>
        {
            { "status", "Onaylandı" },
            { "updatedAt", Timestamp.GetCurrentTimestamp() }
        });

        TempData["Success"] = "Talep onaylandı.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(string id)
    {
        if (!_session.IsAdmin(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            return RedirectToAction("Index");
        }

        await _firestore.PasswordRequests.Document(id).UpdateAsync(new Dictionary<string, object?>
        {
            { "status", "Reddedildi" },
            { "updatedAt", Timestamp.GetCurrentTimestamp() }
        });

        TempData["Success"] = "Talep reddedildi.";
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

        await _firestore.PasswordRequests.Document(id).DeleteAsync();

        TempData["Success"] = "Talep silindi.";
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