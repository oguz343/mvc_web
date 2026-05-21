using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using mvc_web.Filters;
using mvc_web.Models;
using mvc_web.Services;
using System.Text.RegularExpressions;

namespace mvc_web.Controllers
{
    [AdminOnly]
    public class DashboardController : Controller
    {
        private readonly FirestoreDb _firestore;

        public DashboardController(FirestoreDb firestore)
        {
            _firestore = firestore;
        }

        public async Task<IActionResult> Index()
        {
            var usersTask = LoadUsers();
            var validClassesTask = LoadValidClasses();

            await Task.WhenAll(usersTask, validClassesTask);

            var users = usersTask.Result;
            var validClasses = validClassesTask.Result;

            ViewData["StudentCount"] = users.Count(x => x.Role == "Öğrenci");
            ViewData["TeacherCount"] = users.Count(x => x.Role == "Öğretmen");
            ViewData["ParentCount"] = users.Count(x => x.Role == "Veli");

            var lessonCountTask = CountLessons(validClasses);
            var announcementCountTask = CountAnnouncements();
            var submissionCountTask = CountSubmissions();
            var passwordRequestCountTask = CountPasswordRequests();

            await Task.WhenAll(
                lessonCountTask,
                announcementCountTask,
                submissionCountTask,
                passwordRequestCountTask
            );

            ViewData["ClassCount"] = validClasses.Count;
            ViewData["LessonCount"] = lessonCountTask.Result;
            ViewData["AnnouncementCount"] = announcementCountTask.Result;
            ViewData["SubmissionCount"] = submissionCountTask.Result;
            ViewData["PasswordRequestCount"] = passwordRequestCountTask.Result;

            ViewData["RecentUsers"] = users
                .Where(x => !string.IsNullOrWhiteSpace(x.Name) || !string.IsNullOrWhiteSpace(x.SchoolNo))
                .OrderByDescending(x => x.CreatedAt ?? DateTime.MinValue)
                .Take(6)
                .ToList();

            return View();
        }

        private async Task<List<UserViewModel>> LoadUsers()
        {
            var result = new List<UserViewModel>();

            try
            {
                var snapshot = await _firestore.Collection("users").GetSnapshotAsync();

                foreach (var doc in snapshot.Documents)
                {
                    var data = doc.ToDictionary();

                    if (IsDeletedOrFinished(data))
                    {
                        continue;
                    }

                    var roleKey = NormalizeKey(GetString(data, "role", "Role"));
                    var role = "";

                    if (roleKey == "ogrenci")
                    {
                        role = "Öğrenci";
                    }
                    else if (roleKey == "ogretmen")
                    {
                        role = "Öğretmen";
                    }
                    else if (roleKey == "veli")
                    {
                        role = "Veli";
                    }
                    else
                    {
                        continue;
                    }

                    var name = GetString(data, "name", "Name");
                    var number = OnlyDigits(GetString(data, "schoolNo", "SchoolNo", "number", "Number"));

                    if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(number))
                    {
                        continue;
                    }

                    result.Add(new UserViewModel
                    {
                        Id = doc.Id,
                        Name = name,
                        Tc = GetString(data, "tc", "Tc", "TC"),
                        SchoolNo = number,
                        Phone = GetString(data, "phone", "Phone"),
                        Role = role,
                        ClassName = NormalizeClassName(GetString(data, "className", "ClassName", "class", "Class")),
                        LinkedStudentNo = OnlyDigits(GetString(data, "linkedStudentNo", "LinkedStudentNo")),
                        Branch = GetString(data, "branch", "Branch"),
                        ActivationCode = GetString(data, "activationCode", "ActivationCode"),
                        MustChangePassword = GetBool(data, "mustChangePassword", "MustChangePassword"),
                        CreatedAt = GetDate(data, "createdAt", "CreatedAt")
                    });
                }
            }
            catch
            {
            }

            return result;
        }

        private async Task<HashSet<string>> LoadValidClasses()
        {
            var keys = new HashSet<string>();

            try
            {
                var snapshot = await _firestore.Collection("classes").GetSnapshotAsync();

                foreach (var doc in snapshot.Documents)
                {
                    var data = doc.ToDictionary();

                    if (IsDeletedOrFinished(data))
                    {
                        continue;
                    }

                    var grade = GetString(data, "grade", "Grade", "level", "Level");
                    var branch = GetString(data, "branch", "Branch", "section", "Section");

                    var name = FirstNonEmpty(
                        GetString(data, "name", "Name"),
                        GetString(data, "className", "ClassName"),
                        $"{grade}-{branch}"
                    );

                    var className = NormalizeClassName(name);

                    if (string.IsNullOrWhiteSpace(className))
                    {
                        continue;
                    }

                    keys.Add(className);
                }
            }
            catch
            {
            }

            return keys;
        }

        private async Task<int> CountLessons(HashSet<string> validClasses)
        {
            var keys = new HashSet<string>();

            try
            {
                var snapshot = await _firestore.Collection("lessons").GetSnapshotAsync();

                foreach (var doc in snapshot.Documents)
                {
                    var data = doc.ToDictionary();

                    if (IsDeletedOrFinished(data))
                    {
                        continue;
                    }

                    var lessonName = FirstNonEmpty(
                        GetString(data, "name", "Name"),
                        GetString(data, "lessonName", "LessonName"),
                        GetString(data, "title", "Title")
                    );

                    if (string.IsNullOrWhiteSpace(lessonName))
                    {
                        continue;
                    }

                    var className = NormalizeClassName(FirstNonEmpty(
                        GetString(data, "className", "ClassName"),
                        GetString(data, "class", "Class"),
                        GetString(data, "targetClass", "TargetClass"),
                        GetString(data, "selectedClass", "SelectedClass")
                    ));

                    if (string.IsNullOrWhiteSpace(className))
                    {
                        continue;
                    }

                    if (!validClasses.Contains(className))
                    {
                        continue;
                    }

                    /*
                     * Kritik düzeltme:
                     * Aynı ders aynı sınıfta kaç doküman olursa olsun 1 sayılır.
                     * Öğretmen adı/id key'e eklenmiyor.
                     */
                    var key = NormalizeKey($"{lessonName}_{className}");

                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    keys.Add(key);
                }
            }
            catch
            {
            }

            return keys.Count;
        }

        private async Task<int> CountAnnouncements()
        {
            var keys = new HashSet<string>();

            try
            {
                var snapshot = await _firestore.Collection("announcements").GetSnapshotAsync();

                foreach (var doc in snapshot.Documents)
                {
                    var data = doc.ToDictionary();

                    if (IsDeletedOrFinished(data))
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

                    var target = FirstNonEmpty(
                        GetString(data, "target", "Target"),
                        GetString(data, "targetRole", "TargetRole"),
                        GetString(data, "audience", "Audience")
                    );

                    var author = FirstNonEmpty(
                        GetString(data, "author", "Author"),
                        GetString(data, "createdBy", "CreatedBy"),
                        GetString(data, "publisher", "Publisher")
                    );

                    if (string.IsNullOrWhiteSpace(target) &&
                        string.IsNullOrWhiteSpace(author))
                    {
                        continue;
                    }

                    var key = NormalizeKey($"{title}_{content}_{target}_{author}");

                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    keys.Add(key);
                }
            }
            catch
            {
            }

            return keys.Count;
        }

        private async Task<int> CountSubmissions()
        {
            var keys = new HashSet<string>();

            try
            {
                var snapshot = await _firestore.Collection("homework_submissions").GetSnapshotAsync();

                foreach (var doc in snapshot.Documents)
                {
                    var data = doc.ToDictionary();

                    if (IsDeletedOrFinished(data))
                    {
                        continue;
                    }

                    var homeworkId = FirstNonEmpty(
                        GetString(data, "homeworkId", "HomeworkId"),
                        GetString(data, "assignmentId", "AssignmentId")
                    );

                    var studentNo = OnlyDigits(FirstNonEmpty(
                        GetString(data, "studentNo", "StudentNo"),
                        GetString(data, "studentNumber", "StudentNumber"),
                        GetString(data, "schoolNo", "SchoolNo"),
                        GetString(data, "number", "Number")
                    ));

                    var studentId = GetString(data, "studentId", "StudentId");

                    var answer = FirstNonEmpty(
                        GetString(data, "answerText", "AnswerText"),
                        GetString(data, "answer", "Answer"),
                        GetString(data, "content", "Content"),
                        GetString(data, "text", "Text"),
                        GetString(data, "fileUrl", "FileUrl"),
                        GetString(data, "link", "Link")
                    );

                    if (string.IsNullOrWhiteSpace(homeworkId) &&
                        string.IsNullOrWhiteSpace(studentNo) &&
                        string.IsNullOrWhiteSpace(studentId) &&
                        string.IsNullOrWhiteSpace(answer))
                    {
                        continue;
                    }

                    var key = NormalizeKey($"{homeworkId}_{studentNo}_{studentId}");

                    if (string.IsNullOrWhiteSpace(key))
                    {
                        key = NormalizeKey(doc.Id);
                    }

                    keys.Add(key);
                }
            }
            catch
            {
            }

            return keys.Count;
        }

        private async Task<int> CountPasswordRequests()
        {
            var keys = new HashSet<string>();

            try
            {
                var snapshot = await _firestore.Collection("passwordRequests").GetSnapshotAsync();

                foreach (var doc in snapshot.Documents)
                {
                    var data = doc.ToDictionary();

                    if (IsDeletedHard(data))
                    {
                        continue;
                    }

                    var status = NormalizeKey(GetString(data, "status", "Status"));

                    if (status != "bekliyor" &&
                        status != "pending" &&
                        status != "onaybekliyor" &&
                        status != "waiting")
                    {
                        continue;
                    }

                    var role = GetString(data, "role", "Role");

                    var number = OnlyDigits(FirstNonEmpty(
                        GetString(data, "number", "Number"),
                        GetString(data, "schoolNo", "SchoolNo"),
                        GetString(data, "studentNo", "StudentNo")
                    ));

                    var userId = GetString(data, "userId", "UserId");
                    var name = GetString(data, "name", "Name");

                    if (string.IsNullOrWhiteSpace(userId) &&
                        string.IsNullOrWhiteSpace(number) &&
                        string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var key = NormalizeKey(FirstNonEmpty(
                        $"{role}_{number}",
                        userId,
                        $"{name}_{role}",
                        doc.Id
                    ));

                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    keys.Add(key);
                }
            }
            catch
            {
            }

            return keys.Count;
        }

        private static bool IsDeletedOrFinished(Dictionary<string, object> data)
        {
            if (IsDeletedHard(data))
            {
                return true;
            }

            var status = NormalizeKey(GetString(data, "status", "Status"));

            if (status == "silindi" ||
                status == "deleted" ||
                status == "arsivlendi" ||
                status == "archived" ||
                status == "pasif" ||
                status == "passive" ||
                status == "inactive" ||
                status == "iptal" ||
                status == "cancelled" ||
                status == "canceled" ||
                status == "reddedildi" ||
                status == "rejected" ||
                status == "red" ||
                status == "onaylandi" ||
                status == "approved" ||
                status == "tamamlandi" ||
                status == "completed")
            {
                return true;
            }

            return false;
        }

        private static bool IsDeletedHard(Dictionary<string, object> data)
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

        private static string OnlyDigits(string value)
        {
            return new string((value ?? "").Where(char.IsDigit).ToArray());
        }

        private static string NormalizeClassName(string value)
        {
            var text = (value ?? "")
                .Trim()
                .ToUpperInvariant()
                .Replace("SINIF", "")
                .Replace("ŞUBE", "")
                .Replace("SUBE", "")
                .Replace("_", "-")
                .Replace("/", "-")
                .Replace("\\", "-")
                .Replace(".", "-");

            text = Regex.Replace(text, @"\s+", "");

            var match = Regex.Match(text, @"(9|10|11|12)[^\dA-F]*([A-F])");

            if (match.Success)
            {
                return $"{match.Groups[1].Value}-{match.Groups[2].Value}";
            }

            match = Regex.Match(text, @"([A-F])[^\dA-F]*(9|10|11|12)");

            if (match.Success)
            {
                return $"{match.Groups[2].Value}-{match.Groups[1].Value}";
            }

            return "";
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
    }
}
