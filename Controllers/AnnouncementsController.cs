using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using mvc_web.Models;
using mvc_web.Services;

namespace mvc_web.Controllers;

public class AnnouncementsController : Controller
{
    private readonly FirestoreService _firestore;
    private readonly SessionService _session;

    public AnnouncementsController(
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

        ViewData["Title"] = "Duyurular";
        ViewData["PageTitle"] = "Duyurular";
        ViewData["PageSubtitle"] = "Öğrenci, öğretmen ve velilere yayınlanan duyuruları yönetin.";

        var snapshot = await _firestore.Announcements
            .OrderByDescending("createdAt")
            .GetSnapshotAsync();

        var announcements = new List<AnnouncementViewModel>();

        foreach (var doc in snapshot.Documents)
        {
            var data = doc.ToDictionary();

            announcements.Add(new AnnouncementViewModel
            {
                Id = doc.Id,
                Title = GetValue(data, "title", "-"),
                Content = GetValue(data, "content", "-"),
                Author = GetValue(data, "author", "Admin"),
                Target = GetValue(data, "target", "Tüm Okul")
            });
        }

        return View(announcements);
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (!_session.IsAdmin(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        ViewData["Title"] = "Duyuru Ekle";
        ViewData["PageTitle"] = "Yeni Duyuru";
        ViewData["PageSubtitle"] = "Okul geneline veya belirli rollere duyuru yayınlayın.";

        var adminName = _session.GetName(HttpContext) ?? "Admin";

        return View(new AnnouncementViewModel
        {
            Author = adminName,
            Target = "Tüm Okul"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AnnouncementViewModel model)
    {
        if (!_session.IsAdmin(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        if (string.IsNullOrWhiteSpace(model.Author))
        {
            model.Author = _session.GetName(HttpContext) ?? "Admin";
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Duyuru Ekle";
            ViewData["PageTitle"] = "Yeni Duyuru";
            ViewData["PageSubtitle"] = "Okul geneline veya belirli rollere duyuru yayınlayın.";
            return View(model);
        }

        await _firestore.Announcements.AddAsync(new Dictionary<string, object?>
        {
            { "title", model.Title.Trim() },
            { "content", model.Content.Trim() },
            { "author", string.IsNullOrWhiteSpace(model.Author) ? "Admin" : model.Author.Trim() },
            { "target", model.Target.Trim() },
            { "createdAt", Timestamp.GetCurrentTimestamp() },
            { "updatedAt", Timestamp.GetCurrentTimestamp() }
        });

        TempData["Success"] = "Duyuru yayınlandı.";
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

        var doc = await _firestore.Announcements.Document(id).GetSnapshotAsync();

        if (!doc.Exists)
        {
            TempData["Error"] = "Duyuru bulunamadı.";
            return RedirectToAction("Index");
        }

        var data = doc.ToDictionary();

        var model = new AnnouncementViewModel
        {
            Id = doc.Id,
            Title = GetValue(data, "title"),
            Content = GetValue(data, "content"),
            Author = GetValue(data, "author", "Admin"),
            Target = GetValue(data, "target", "Tüm Okul")
        };

        ViewData["Title"] = "Duyuru Düzenle";
        ViewData["PageTitle"] = "Duyuru Düzenle";
        ViewData["PageSubtitle"] = $"{model.Title} duyurusunu güncelleyin.";

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AnnouncementViewModel model)
    {
        if (!_session.IsAdmin(HttpContext))
        {
            return RedirectToAction("Login", "Auth");
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Duyuru Düzenle";
            ViewData["PageTitle"] = "Duyuru Düzenle";
            ViewData["PageSubtitle"] = "Duyuru bilgilerini güncelleyin.";
            return View(model);
        }

        var announcementRef = _firestore.Announcements.Document(model.Id);
        var announcementDoc = await announcementRef.GetSnapshotAsync();

        if (!announcementDoc.Exists)
        {
            TempData["Error"] = "Duyuru bulunamadı.";
            return RedirectToAction("Index");
        }

        await announcementRef.UpdateAsync(new Dictionary<string, object?>
        {
            { "title", model.Title.Trim() },
            { "content", model.Content.Trim() },
            { "author", string.IsNullOrWhiteSpace(model.Author) ? "Admin" : model.Author.Trim() },
            { "target", model.Target.Trim() },
            { "updatedAt", Timestamp.GetCurrentTimestamp() }
        });

        TempData["Success"] = "Duyuru güncellendi.";
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

        await _firestore.Announcements.Document(id).DeleteAsync();

        TempData["Success"] = "Duyuru silindi.";
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