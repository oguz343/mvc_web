using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using mvc_web.Filters;
using mvc_web.Services; 
namespace mvc_web.Controllers;

[Route("SystemReset")]
[AdminOnly]
public class SystemResetController : Controller
{
    private readonly FirestoreDb _firestore;

    public SystemResetController(FirestoreDb firestore)
    {
        _firestore = firestore;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public IActionResult Index()
    {
        return View("~/Views/SystemReset/Index.cshtml");
    }

    [HttpPost("Reset")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reset(string confirmText)
    {
        if ((confirmText ?? "").Trim() != "SIFIRLA")
        {
            TempData["Error"] = "Sıfırlamak için kutuya SIFIRLA yazmalısınız.";
            return RedirectToAction(nameof(Index));
        }

        var report = new List<string>();

        var deletedUsers = await DeleteUsersExceptAdminAsync();
        report.Add($"Kullanıcılar temizlendi: {deletedUsers} kayıt silindi. Admin korundu.");

        var collectionsToDelete = new[]
        {
            "classes",
            "lessons",

            "homeworks",
            "assignments",

            "homework_submissions",
            "submissions",

            "announcements",

            "passwordRequests",
            "password_requests",
            "forgotPasswordRequests",
            "forgot_password_requests",

            "notifications",
            "messages",
            "lessonTypes",
            "assignmentTypes"
        };

        foreach (var collection in collectionsToDelete)
        {
            var count = await DeleteCollectionAsync(collection);
            report.Add($"{collection}: {count} kayıt silindi.");
        }

        await EnsureDefaultAdminAsync();

        TempData["Success"] = "Sistem verileri sıfırlandı. Admin hesabı korundu.";
        TempData["Report"] = string.Join("\n", report);

        return RedirectToAction(nameof(Index));
    }

    private async Task<int> DeleteUsersExceptAdminAsync()
    {
        var snapshot = await _firestore.Collection("users").GetSnapshotAsync();

        var deletedCount = 0;
        var batch = _firestore.StartBatch();
        var operationCount = 0;

        foreach (var doc in snapshot.Documents)
        {
            var data = doc.ToDictionary();

            var role = NormalizeRole(GetString(data, "role", "Role", "userRole", "UserRole"));
            var number = OnlyDigits(GetString(
                data,
                "number",
                "Number",
                "schoolNo",
                "SchoolNo",
                "adminNo",
                "AdminNo",
                "teacherNo",
                "TeacherNo",
                "studentNo",
                "StudentNo"
            ));

            var isAdmin =
                role == "admin" ||
                number == "0000" ||
                doc.Id == "admin" ||
                doc.Id == "0000";

            if (isAdmin)
            {
                continue;
            }

            batch.Delete(doc.Reference);
            operationCount++;
            deletedCount++;

            if (operationCount >= 400)
            {
                await batch.CommitAsync();
                batch = _firestore.StartBatch();
                operationCount = 0;
            }
        }

        if (operationCount > 0)
        {
            await batch.CommitAsync();
        }

        return deletedCount;
    }

    private async Task<int> DeleteCollectionAsync(string collectionName)
    {
        var deletedCount = 0;

        while (true)
        {
            var snapshot = await _firestore
                .Collection(collectionName)
                .Limit(400)
                .GetSnapshotAsync();

            if (snapshot.Documents.Count == 0)
            {
                break;
            }

            var batch = _firestore.StartBatch();

            foreach (var doc in snapshot.Documents)
            {
                batch.Delete(doc.Reference);
                deletedCount++;
            }

            await batch.CommitAsync();

            if (snapshot.Documents.Count < 400)
            {
                break;
            }
        }

        return deletedCount;
    }

    private async Task EnsureDefaultAdminAsync()
    {
        var users = await _firestore.Collection("users").GetSnapshotAsync();
        var adminHash = PasswordHashService.HashPassword("admin123");

        foreach (var doc in users.Documents)
        {
            var data = doc.ToDictionary();

            var role = NormalizeRole(GetString(data, "role", "Role", "userRole", "UserRole"));
            var number = OnlyDigits(GetString(data, "number", "Number", "schoolNo", "SchoolNo", "adminNo", "AdminNo"));

            if (role == "admin" || number == "0000")
            {
                await doc.Reference.SetAsync(
                    new Dictionary<string, object?>
                    {
                        ["name"] = "Admin",
                        ["Name"] = "Admin",
                        ["fullName"] = "Admin",
                        ["FullName"] = "Admin",

                        ["role"] = "admin",
                        ["Role"] = "Admin",
                        ["userRole"] = "admin",
                        ["UserRole"] = "Admin",

                        ["number"] = "0000",
                        ["Number"] = "0000",
                        ["schoolNo"] = "0000",
                        ["SchoolNo"] = "0000",
                        ["adminNo"] = "0000",
                        ["AdminNo"] = "0000",

                        ["passwordHash"] = adminHash,
                        ["PasswordHash"] = adminHash,
                        ["password"] = "",
                        ["Password"] = "",

                        ["isDeleted"] = false,
                        ["IsDeleted"] = false,
                        ["isActive"] = true,
                        ["IsActive"] = true,

                        ["mustChangePassword"] = false,
                        ["MustChangePassword"] = false,

                        ["updatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
                        ["UpdatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
                    },
                    SetOptions.MergeAll
                );

                return;
            }
        }

        await _firestore.Collection("users").AddAsync(
            new Dictionary<string, object?>
            {
                ["name"] = "Admin",
                ["Name"] = "Admin",
                ["fullName"] = "Admin",
                ["FullName"] = "Admin",

                ["role"] = "admin",
                ["Role"] = "Admin",
                ["userRole"] = "admin",
                ["UserRole"] = "Admin",

                ["number"] = "0000",
                ["Number"] = "0000",
                ["schoolNo"] = "0000",
                ["SchoolNo"] = "0000",
                ["adminNo"] = "0000",
                ["AdminNo"] = "0000",

                ["passwordHash"] = adminHash,
                ["PasswordHash"] = adminHash,
                ["password"] = "",
                ["Password"] = "",

                ["isDeleted"] = false,
                ["IsDeleted"] = false,
                ["isActive"] = true,
                ["IsActive"] = true,

                ["mustChangePassword"] = false,
                ["MustChangePassword"] = false,

                ["createdAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
                ["CreatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
                ["updatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
                ["UpdatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
            }
        );
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
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static string NormalizeRole(string value)
    {
        var key = NormalizeKey(value);

        if (key is "admin" or "yonetici")
        {
            return "admin";
        }

        if (key is "ogretmen" or "teacher")
        {
            return "ogretmen";
        }

        if (key is "ogrenci" or "student")
        {
            return "ogrenci";
        }

        if (key is "veli" or "parent")
        {
            return "veli";
        }

        return key;
    }

    private static string NormalizeKey(string value)
    {
        value = (value ?? "").Trim().ToLowerInvariant();

        value = value
            .Replace("ı", "i")
            .Replace("ğ", "g")
            .Replace("ü", "u")
            .Replace("ş", "s")
            .Replace("ö", "o")
            .Replace("ç", "c");

        return new string(value.Where(char.IsLetterOrDigit).ToArray());
    }
}