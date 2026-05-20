using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using mvc_web.Models;

namespace mvc_web.Controllers
{
    public class TeacherAnnouncementsController : Controller
    {
        private readonly FirestoreDb _firestore;

        public TeacherAnnouncementsController(FirestoreDb firestore)
        {
            _firestore = firestore;
        }

        public async Task<IActionResult> Index()
        {
            if (!IsTeacherLoggedIn())
            {
                return RedirectToAction("Login", "Auth");
            }

            var announcements = new List<TeacherAnnouncementViewModel>();

            var snapshot = await _firestore
                .Collection("announcements")
                .GetSnapshotAsync();

            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();

                if (IsDeletedOrInvalid(data))
                {
                    continue;
                }

                var title = FirstNonEmpty(
                    GetString(data, "title", "Title"),
                    GetString(data, "name", "Name")
                );

                var content = FirstNonEmpty(
                    GetString(data, "content", "Content"),
                    GetString(data, "message", "Message"),
                    GetString(data, "description", "Description")
                );

                if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(content))
                {
                    continue;
                }

                /*
                 * Eski kalıntı/test duyuruları burada eliyoruz.
                 * Sende görünen saçma kayıt: Test / Test duyuru / tarih "-"
                 * Bu tarz kayıtlarda genelde CreatedAt yok.
                 */
                var createdAt = GetDate(
                    data,
                    "createdAt",
                    "CreatedAt",
                    "publishedAt",
                    "PublishedAt",
                    "date",
                    "Date"
                );

                if (createdAt == null)
                {
                    continue;
                }

                var target = FirstNonEmpty(
                    GetString(data, "target", "Target"),
                    GetString(data, "targetRole", "TargetRole"),
                    GetString(data, "audience", "Audience"),
                    "Tüm Okul"
                );

                /*
                 * İstediğin mantık:
                 * - Tüm Okul görünür
                 * - Öğretmen görünür
                 * - Öğretmenler görünür
                 */
                if (!IsAnnouncementForTeacher(target))
                {
                    continue;
                }

                announcements.Add(new TeacherAnnouncementViewModel
                {
                    Id = doc.Id,
                    Title = string.IsNullOrWhiteSpace(title) ? "Duyuru" : title,
                    Content = string.IsNullOrWhiteSpace(content) ? "-" : content,
                    Author = FirstNonEmpty(
                        GetString(data, "author", "Author"),
                        GetString(data, "createdBy", "CreatedBy"),
                        GetString(data, "publisher", "Publisher"),
                        "Admin"
                    ),
                    Target = NormalizeTargetName(target),
                    CreatedAt = createdAt
                });
            }

            announcements = announcements
                .OrderByDescending(x => x.CreatedAt ?? DateTime.MinValue)
                .ToList();

            return View(announcements);
        }

        private bool IsTeacherLoggedIn()
        {
            var role =
                HttpContext.Session.GetString("Role") ??
                HttpContext.Session.GetString("UserRole") ??
                "";

            return role == "Öğretmen";
        }

        private static bool IsAnnouncementForTeacher(string target)
        {
            var value = NormalizeKey(target);

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (value.Contains("ogretmen"))
            {
                return true;
            }

            if (value.Contains("tum") ||
                value.Contains("herkes") ||
                value.Contains("genel") ||
                value.Contains("okul") ||
                value.Contains("all"))
            {
                return true;
            }

            return false;
        }

        private static string NormalizeTargetName(string target)
        {
            var value = NormalizeKey(target);

            if (value.Contains("ogretmen"))
            {
                return "Öğretmen";
            }

            if (value.Contains("tum") ||
                value.Contains("herkes") ||
                value.Contains("genel") ||
                value.Contains("okul") ||
                value.Contains("all"))
            {
                return "Tüm Okul";
            }

            return string.IsNullOrWhiteSpace(target) ? "Tüm Okul" : target;
        }

        private static bool IsDeletedOrInvalid(Dictionary<string, object> data)
        {
            if (data == null || data.Count == 0)
            {
                return true;
            }

            if (GetBool(data, "isDeleted", "IsDeleted", "deleted", "Deleted"))
            {
                return true;
            }

            if (GetBool(data, "isArchived", "IsArchived", "archived", "Archived"))
            {
                return true;
            }

            if (GetBool(data, "isHidden", "IsHidden", "hidden", "Hidden"))
            {
                return true;
            }

            if (GetBool(data, "isRemoved", "IsRemoved", "removed", "Removed"))
            {
                return true;
            }

            if (HasFalseBool(data, "isActive", "IsActive", "active", "Active", "enabled", "Enabled"))
            {
                return true;
            }

            if (HasAnyValue(data, "deletedAt", "DeletedAt", "removedAt", "RemovedAt", "archivedAt", "ArchivedAt"))
            {
                return true;
            }

            var status = NormalizeKey(GetString(data, "status", "Status"));

            if (status == "silindi" ||
                status == "deleted" ||
                status == "arsivlendi" ||
                status == "archived" ||
                status == "pasif" ||
                status == "inactive" ||
                status == "iptal" ||
                status == "cancelled" ||
                status == "canceled")
            {
                return true;
            }

            return false;
        }

        private static bool HasFalseBool(Dictionary<string, object> data, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!data.TryGetValue(key, out var value) || value == null)
                {
                    continue;
                }

                if (value is bool boolValue)
                {
                    return boolValue == false;
                }

                if (bool.TryParse(value.ToString(), out bool parsed))
                {
                    return parsed == false;
                }
            }

            return false;
        }

        private static bool HasAnyValue(Dictionary<string, object> data, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (data.TryGetValue(key, out var value) && value != null)
                {
                    if (!string.IsNullOrWhiteSpace(value.ToString()))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return "";
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

                    if (bool.TryParse(value.ToString(), out bool parsed))
                    {
                        return parsed;
                    }

                    if (int.TryParse(value.ToString(), out int intValue))
                    {
                        return intValue == 1;
                    }
                }
            }

            return false;
        }

        private static DateTime? GetDate(Dictionary<string, object> data, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!data.TryGetValue(key, out var value) || value == null)
                {
                    continue;
                }

                if (value is Timestamp timestamp)
                {
                    return timestamp.ToDateTime();
                }

                if (DateTime.TryParse(value.ToString(), out DateTime date))
                {
                    return date;
                }
            }

            return null;
        }

        private static string NormalizeKey(string value)
        {
            return (value ?? "")
                .Trim()
                .ToLowerInvariant()
                .Replace("ı", "i")
                .Replace("ğ", "g")
                .Replace("ü", "u")
                .Replace("ş", "s")
                .Replace("ö", "o")
                .Replace("ç", "c")
                .Replace(" ", "")
                .Replace("-", "")
                .Replace("_", "")
                .Replace("/", "")
                .Replace("\\", "")
                .Replace(".", "")
                .Replace(":", "")
                .Replace(";", "")
                .Replace(",", "");
        }
    }
}
