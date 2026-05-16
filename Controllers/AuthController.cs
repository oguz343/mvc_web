using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using mvc_web.Services;
using System.Globalization;

namespace mvc_web.Controllers;

public class AuthController : Controller
{
    private readonly FirestoreDb _firestore;

    public AuthController(FirestoreDb firestore)
    {
        _firestore = firestore;
    }

    [HttpGet]
    public async Task<IActionResult> Login()
    {
        await EnsureDefaultAdminAsync();
        await FillLoginStatsAsync();

        return View();
    }

    [HttpGet]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult SetPassword()
    {
        var userId = TempData["SetPasswordUserId"]?.ToString() ?? HttpContext.Session.GetString("UserId") ?? "";
        var name = TempData["SetPasswordName"]?.ToString() ?? HttpContext.Session.GetString("UserName") ?? "";
        var role = TempData["SetPasswordRole"]?.ToString() ?? HttpContext.Session.GetString("UserRole") ?? "";
        var number = TempData["SetPasswordNumber"]?.ToString() ?? HttpContext.Session.GetString("UserNumber") ?? "";

        if (string.IsNullOrWhiteSpace(userId))
        {
            TempData["Error"] = "Yeni sifre olusturmak icin once aktivasyon kodu ile giris yapmalisiniz.";
            return RedirectToAction(nameof(Login));
        }

        KeepSetPasswordState(userId, name, role, number);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPassword(string password, string repeatPassword)
    {
        var userId = TempData["SetPasswordUserId"]?.ToString() ?? HttpContext.Session.GetString("UserId") ?? "";
        var name = TempData["SetPasswordName"]?.ToString() ?? HttpContext.Session.GetString("UserName") ?? "";
        var role = TempData["SetPasswordRole"]?.ToString() ?? HttpContext.Session.GetString("UserRole") ?? "";
        var number = TempData["SetPasswordNumber"]?.ToString() ?? HttpContext.Session.GetString("UserNumber") ?? "";

        if (string.IsNullOrWhiteSpace(userId))
        {
            TempData["Error"] = "Yeni sifre olusturmak icin once aktivasyon kodu ile giris yapmalisiniz.";
            return RedirectToAction(nameof(Login));
        }

        password = password?.Trim() ?? "";
        repeatPassword = repeatPassword?.Trim() ?? "";

        if (password.Length < 4)
        {
            KeepSetPasswordState(userId, name, role, number);
            ModelState.AddModelError("", "Sifre en az 4 karakter olmalidir.");
            return View();
        }

        if (password != repeatPassword)
        {
            KeepSetPasswordState(userId, name, role, number);
            ModelState.AddModelError("", "Sifreler eslesmiyor.");
            return View();
        }

        var hash = PasswordHashService.HashPassword(password);
        var now = Timestamp.FromDateTime(DateTime.UtcNow);

        await _firestore.Collection("users").Document(userId).SetAsync(
            new Dictionary<string, object?>
            {
                ["passwordHash"] = hash,
                ["PasswordHash"] = hash,
                ["password"] = "",
                ["Password"] = "",
                ["activationCode"] = "",
                ["ActivationCode"] = "",
                ["mustChangePassword"] = false,
                ["MustChangePassword"] = false,
                ["passwordUpdatedAt"] = now,
                ["PasswordUpdatedAt"] = now,
                ["updatedAt"] = now,
                ["UpdatedAt"] = now,
            },
            SetOptions.MergeAll
        );

        HttpContext.Session.SetString("UserId", userId);
        HttpContext.Session.SetString("UserDocId", userId);
        HttpContext.Session.SetString("UserRole", role);
        HttpContext.Session.SetString("Role", role);
        HttpContext.Session.SetString("UserNumber", number);
        HttpContext.Session.SetString("Number", number);
        HttpContext.Session.SetString("SchoolNo", number);
        HttpContext.Session.SetString("LoginNumber", number);
        HttpContext.Session.SetString("UserName", string.IsNullOrWhiteSpace(name) ? role : name);
        HttpContext.Session.SetString("Name", string.IsNullOrWhiteSpace(name) ? role : name);

        TempData.Remove("SetPasswordUserId");
        TempData.Remove("SetPasswordName");
        TempData.Remove("SetPasswordRole");
        TempData.Remove("SetPasswordNumber");

        return RedirectByRole(role);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string Role, string Number, string Password)
    {
        await EnsureDefaultAdminAsync();

        Role = (Role ?? "").Trim();
        Number = OnlyDigits(Number);
        Password = Password ?? "";

        if (string.IsNullOrWhiteSpace(Role))
        {
            TempData["Error"] = "Rol seçmelisiniz.";
            await FillLoginStatsAsync();
            return View();
        }

        if (string.IsNullOrWhiteSpace(Number))
        {
            TempData["Error"] = "Numara boş bırakılamaz.";
            await FillLoginStatsAsync();
            return View();
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            TempData["Error"] = "Şifre boş bırakılamaz.";
            await FillLoginStatsAsync();
            return View();
        }

        var wantedRoleKey = NormalizeRole(Role);
        var user = await FindUserAsync(wantedRoleKey, Number);

        if (user == null)
        {
            TempData["Error"] = "Kullanıcı bulunamadı veya rol hatalı.";
            await FillLoginStatsAsync();
            return View();
        }

        var data = user.ToDictionary();

        if (IsDeleted(data))
        {
            TempData["Error"] = "Bu kullanıcı pasif veya silinmiş.";
            await FillLoginStatsAsync();
            return View();
        }

        var passwordHash = GetString(data, "passwordHash", "PasswordHash", "hash", "Hash");
        var legacyPassword = GetString(data, "password", "Password");
        var activationCode = GetString(data, "activationCode", "ActivationCode");
        var mustChangePassword = GetBool(data, "mustChangePassword", "MustChangePassword", "forcePasswordChange", "ForcePasswordChange");

        var passwordOk = false;
        var shouldMigrateToHash = false;

        if (mustChangePassword && !string.IsNullOrWhiteSpace(activationCode))
        {
            passwordOk = activationCode == Password;
        }
        else if (PasswordHashService.IsHash(passwordHash))
        {
            passwordOk = PasswordHashService.VerifyPassword(Password, passwordHash);
        }
        else if (!string.IsNullOrWhiteSpace(legacyPassword))
        {
            passwordOk = legacyPassword == Password;

            if (passwordOk)
            {
                shouldMigrateToHash = true;
            }
        }

        if (!passwordOk)
        {
            TempData["Error"] = "Numara, rol veya şifre hatalı.";
            await FillLoginStatsAsync();
            return View();
        }

        var cleanRole = NormalizeRoleToDisplay(wantedRoleKey);

        var cleanName = FirstNonEmpty(
            GetString(data, "name", "Name"),
            GetString(data, "fullName", "FullName"),
            GetString(data, "userName", "UserName"),
            cleanRole
        );

        var cleanBranch = FirstNonEmpty(
            GetString(data, "branch", "Branch"),
            GetString(data, "teacherBranch", "TeacherBranch")
        );

        var cleanClass = FirstNonEmpty(
            GetString(data, "className", "ClassName"),
            GetString(data, "class", "Class"),
            GetString(data, "studentClass", "StudentClass")
        );

        var cleanNumber = OnlyDigits(FirstNonEmpty(
            GetString(data, "number", "Number"),
            GetString(data, "schoolNo", "SchoolNo"),
            GetString(data, "studentNo", "StudentNo"),
            GetString(data, "teacherNo", "TeacherNo"),
            Number
        ));

        if (shouldMigrateToHash)
        {
            var newHash = PasswordHashService.HashPassword(Password);

            await user.Reference.SetAsync(
                new Dictionary<string, object?>
                {
                    ["passwordHash"] = newHash,
                    ["PasswordHash"] = newHash,

                    ["password"] = "",
                    ["Password"] = "",

                    ["updatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
                    ["UpdatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
                },
                SetOptions.MergeAll
            );
        }

        HttpContext.Session.SetString("UserId", user.Id);
        HttpContext.Session.SetString("UserDocId", user.Id);

        HttpContext.Session.SetString("UserRole", cleanRole);
        HttpContext.Session.SetString("Role", cleanRole);

        HttpContext.Session.SetString("UserNumber", cleanNumber);
        HttpContext.Session.SetString("Number", cleanNumber);
        HttpContext.Session.SetString("SchoolNo", cleanNumber);
        HttpContext.Session.SetString("LoginNumber", cleanNumber);

        HttpContext.Session.SetString("UserName", cleanName);
        HttpContext.Session.SetString("Name", cleanName);

        HttpContext.Session.SetString("UserBranch", cleanBranch);
        HttpContext.Session.SetString("Branch", cleanBranch);

        HttpContext.Session.SetString("UserClass", cleanClass);
        HttpContext.Session.SetString("ClassName", cleanClass);

        if (mustChangePassword)
        {
            KeepSetPasswordState(user.Id, cleanName, cleanRole, cleanNumber);
            return RedirectToAction(nameof(SetPassword));
        }

        return RedirectByRole(cleanRole);
    }

    private IActionResult RedirectByRole(string role)
    {
        return NormalizeRole(role) switch
        {
            "admin" => RedirectToAction("Index", "Dashboard"),
            "ogretmen" => RedirectToAction("Index", "Teacher"),
            "ogrenci" => RedirectToAction("Index", "Portal"),
            "veli" => RedirectToAction("Index", "Portal"),
            _ => RedirectToAction(nameof(Login))
        };
    }

    private void KeepSetPasswordState(string userId, string name, string role, string number)
    {
        TempData["SetPasswordUserId"] = userId;
        TempData["SetPasswordName"] = name;
        TempData["SetPasswordRole"] = role;
        TempData["SetPasswordNumber"] = number;
        TempData.Keep();

        ViewBag.Name = string.IsNullOrWhiteSpace(name) ? "Kullanici" : name;
        ViewBag.Role = role;
        ViewBag.Number = number;
    }

    private async Task FillLoginStatsAsync()
    {
        var activeUserCount = await CountActiveUsersAsync();

        var activeLessonCount = await CountFirstNonEmptyCollectionAsync(
            "lessons",
            "dersler",
            "courses",
            "classesLessons"
        );

        var homeworkStats = await GetHomeworkStatsAsync();

        ViewBag.ActiveUserCount = FormatNumber(activeUserCount);
        ViewBag.ActiveUsersText = FormatNumber(activeUserCount);

        ViewBag.ActiveLessonCount = FormatNumber(activeLessonCount);
        ViewBag.ActiveLessonsText = FormatNumber(activeLessonCount);

        ViewBag.CompletedHomeworkCount = FormatNumber(homeworkStats.CompletedCount);
        ViewBag.CompletedHomeworksText = FormatNumber(homeworkStats.CompletedCount);

        ViewBag.SystemSuccessRateText = homeworkStats.TotalCount > 0
            ? FormatPercent(homeworkStats.CompletedCount, homeworkStats.TotalCount)
            : "%0";

        ViewBag.SystemStatusTitle = "Sistem Aktif";
        ViewBag.SystemStatusSubTitle = "Tüm Sistemler Çevrimiçi";
    }

    private async Task<int> CountActiveUsersAsync()
    {
        var snapshot = await _firestore.Collection("users").GetSnapshotAsync();

        var count = 0;

        foreach (var doc in snapshot.Documents)
        {
            if (!doc.Exists)
            {
                continue;
            }

            var data = doc.ToDictionary();

            if (IsDeleted(data))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private async Task<int> CountFirstNonEmptyCollectionAsync(params string[] collectionNames)
    {
        foreach (var collectionName in collectionNames)
        {
            var docs = await ReadCollectionAsync(collectionName);

            if (docs.Count == 0)
            {
                continue;
            }

            return docs.Count(x => !IsDeleted(x));
        }

        return 0;
    }

    private async Task<(int CompletedCount, int TotalCount)> GetHomeworkStatsAsync()
    {
        var submissionCollections = new[]
        {
            "homeworkSubmissions",
            "submissions",
            "odevTeslimleri",
            "homework_submissions",
            "assignmentSubmissions"
        };

        foreach (var collectionName in submissionCollections)
        {
            var docs = await ReadCollectionAsync(collectionName);
            var activeDocs = docs.Where(x => !IsDeleted(x)).ToList();

            if (activeDocs.Count == 0)
            {
                continue;
            }

            var completed = activeDocs.Count(IsCompletedLike);

            if (completed == 0)
            {
                completed = activeDocs.Count;
            }

            return (completed, activeDocs.Count);
        }

        var homeworkCollections = new[]
        {
            "homeworks",
            "assignments",
            "odevler",
            "homework",
            "assignment"
        };

        foreach (var collectionName in homeworkCollections)
        {
            var docs = await ReadCollectionAsync(collectionName);
            var activeDocs = docs.Where(x => !IsDeleted(x)).ToList();

            if (activeDocs.Count == 0)
            {
                continue;
            }

            var completed = activeDocs.Count(IsCompletedLike);

            return (completed, activeDocs.Count);
        }

        return (0, 0);
    }

    private async Task<List<Dictionary<string, object>>> ReadCollectionAsync(string collectionName)
    {
        try
        {
            var snapshot = await _firestore.Collection(collectionName).GetSnapshotAsync();

            return snapshot.Documents
                .Where(x => x.Exists)
                .Select(x => x.ToDictionary())
                .ToList();
        }
        catch
        {
            return new List<Dictionary<string, object>>();
        }
    }

    private static bool IsCompletedLike(Dictionary<string, object> data)
    {
        if (HasAnyValue(data,
                "submittedAt", "SubmittedAt",
                "completedAt", "CompletedAt",
                "deliveredAt", "DeliveredAt",
                "evaluatedAt", "EvaluatedAt",
                "gradedAt", "GradedAt"))
        {
            return true;
        }

        var status = NormalizeKey(FirstNonEmpty(
            GetString(data, "status", "Status"),
            GetString(data, "state", "State"),
            GetString(data, "submissionStatus", "SubmissionStatus"),
            GetString(data, "homeworkStatus", "HomeworkStatus")
        ));

        return status is
            "completed" or
            "complete" or
            "done" or
            "submitted" or
            "delivered" or
            "graded" or
            "evaluated" or
            "tamamlandi" or
            "teslimedildi" or
            "teslim" or
            "degerlendirildi";
    }

    private static bool HasAnyValue(Dictionary<string, object> data, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!data.TryGetValue(key, out var value) || value == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(value.ToString()))
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatNumber(int value)
    {
        return value.ToString("N0", new CultureInfo("tr-TR"));
    }

    private static string FormatPercent(int completed, int total)
    {
        if (total <= 0)
        {
            return "%0";
        }

        var percent = completed * 100.0 / total;
        return "%" + percent.ToString("0.#", new CultureInfo("tr-TR"));
    }

    private async Task<DocumentSnapshot?> FindUserAsync(string roleKey, string number)
    {
        var snapshot = await _firestore.Collection("users").GetSnapshotAsync();

        foreach (var doc in snapshot.Documents)
        {
            var data = doc.ToDictionary();

            if (IsDeleted(data))
            {
                continue;
            }

            var currentRole = NormalizeRole(FirstNonEmpty(
                GetString(data, "role", "Role"),
                GetString(data, "userRole", "UserRole")
            ));

            var currentNumber = OnlyDigits(FirstNonEmpty(
                GetString(data, "number", "Number"),
                GetString(data, "schoolNo", "SchoolNo"),
                GetString(data, "studentNo", "StudentNo"),
                GetString(data, "teacherNo", "TeacherNo"),
                GetString(data, "parentNo", "ParentNo"),
                GetString(data, "adminNo", "AdminNo")
            ));

            if (currentRole == roleKey && currentNumber == number)
            {
                return doc;
            }
        }

        return null;
    }

    private async Task EnsureDefaultAdminAsync()
    {
        var users = await _firestore.Collection("users").GetSnapshotAsync();

        foreach (var doc in users.Documents)
        {
            var data = doc.ToDictionary();

            var role = NormalizeRole(FirstNonEmpty(
                GetString(data, "role", "Role"),
                GetString(data, "userRole", "UserRole")
            ));

            var number = OnlyDigits(FirstNonEmpty(
                GetString(data, "number", "Number"),
                GetString(data, "schoolNo", "SchoolNo"),
                GetString(data, "adminNo", "AdminNo")
            ));

            if (role == "admin" || number == "0000")
            {
                var passwordHash = GetString(data, "passwordHash", "PasswordHash");

                var update = new Dictionary<string, object?>
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

                    ["isDeleted"] = false,
                    ["IsDeleted"] = false,
                    ["isActive"] = true,
                    ["IsActive"] = true,

                    ["mustChangePassword"] = false,
                    ["MustChangePassword"] = false,

                    ["updatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
                    ["UpdatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
                };

                if (!PasswordHashService.IsHash(passwordHash))
                {
                    var hash = PasswordHashService.HashPassword("admin123");

                    update["passwordHash"] = hash;
                    update["PasswordHash"] = hash;
                    update["password"] = "";
                    update["Password"] = "";
                }

                await doc.Reference.SetAsync(update, SetOptions.MergeAll);
                return;
            }
        }

        var adminHash = PasswordHashService.HashPassword("admin123");

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

    private static bool IsDeleted(Dictionary<string, object> data)
    {
        var deleted = GetString(data, "isDeleted", "IsDeleted", "deleted", "Deleted").Trim().ToLowerInvariant();
        var active = GetString(data, "isActive", "IsActive", "active", "Active").Trim().ToLowerInvariant();

        if (deleted is "true" or "1" or "evet" or "yes")
        {
            return true;
        }

        if (active is "false" or "0" or "hayir" or "hayır" or "no")
        {
            return true;
        }

        return false;
    }

    private static bool GetBool(Dictionary<string, object> data, params string[] keys)
    {
        var value = GetString(data, keys).Trim().ToLowerInvariant();

        return value is "true" or "1" or "evet" or "yes";
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

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
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

    private static string NormalizeRoleToDisplay(string roleKey)
    {
        return roleKey switch
        {
            "admin" => "Admin",
            "ogretmen" => "Öğretmen",
            "ogrenci" => "Öğrenci",
            "veli" => "Veli",
            _ => roleKey
        };
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