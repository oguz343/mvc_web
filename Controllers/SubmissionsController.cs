using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using mvc_web.Filters;
using mvc_web.Models;
using mvc_web.Services;

namespace mvc_web.Controllers;

[AdminOnly]
public class SubmissionsController : Controller
{
    private readonly FirestoreService _firestore;
    private readonly SessionService _session;

    public SubmissionsController(
        FirestoreService firestore,
        SessionService session
    )
    {
        _firestore = firestore;
        _session = session;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Teslimler";
        ViewData["PageTitle"] = "Ödev Teslimleri";
        ViewData["PageSubtitle"] = "Öğrencilerin gönderdiği ödev teslimlerini görüntüleyin.";

        var submissions = new List<(SubmissionViewModel Item, DateTime SortDate)>();
        var seen = new HashSet<string>();

        foreach (var collectionName in new[] { "submissions", "homework_submissions" })
        {
            var snapshot = await _firestore.Db.Collection(collectionName).GetSnapshotAsync();

            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();

                if (IsDeleted(data))
                {
                    continue;
                }

                var assignmentId = FirstValue(data, "assignmentId", "AssignmentId", "homeworkId", "HomeworkId");
                var studentNo = OnlyDigits(FirstValue(data, "studentNo", "StudentNo", "studentNumber", "StudentNumber", "schoolNo", "SchoolNo"));
                var key = !string.IsNullOrWhiteSpace(assignmentId) && !string.IsNullOrWhiteSpace(studentNo)
                    ? NormalizeKey($"{assignmentId}_{studentNo}")
                    : NormalizeKey(doc.Id);

                if (seen.Contains(key))
                {
                    continue;
                }

                seen.Add(key);
                submissions.Add((new SubmissionViewModel
                {
                    Id = doc.Id,
                    AssignmentId = assignmentId,
                    AssignmentTitle = FirstValueOrDefault(data, "-", "assignmentTitle", "AssignmentTitle", "homeworkTitle", "HomeworkTitle", "title", "Title"),
                    Lesson = FirstValueOrDefault(data, "-", "lesson", "Lesson", "lessonName", "LessonName", "courseName", "CourseName"),
                    ClassName = FirstValueOrDefault(data, "-", "className", "ClassName", "class", "Class", "targetClass", "TargetClass"),
                    TeacherId = FirstValue(data, "teacherId", "TeacherId"),
                    TeacherName = FirstValueOrDefault(data, "-", "teacherName", "TeacherName", "teacher", "Teacher"),
                    StudentNo = string.IsNullOrWhiteSpace(studentNo) ? "-" : studentNo,
                    Answer = FirstValue(data, "answer", "Answer", "answerText", "AnswerText", "content", "Content", "text", "Text"),
                    Link = FirstValue(data, "link", "Link", "answerLink", "AnswerLink", "fileUrl", "FileUrl", "url", "Url"),
                    Status = FirstValueOrDefault(data, "Teslim Edildi", "status", "Status"),
                    Grade = FirstValue(data, "grade", "Grade", "score", "Score", "point", "Point"),
                    Feedback = FirstValue(data, "feedback", "Feedback", "comment", "Comment")
                }, GetDate(data, "submittedAt", "SubmittedAt", "createdAt", "CreatedAt", "updatedAt", "UpdatedAt") ?? DateTime.MinValue));
            }
        }

        return View(submissions
            .OrderByDescending(x => x.SortDate)
            .Select(x => x.Item)
            .ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return RedirectToAction("Index");
        }

        await DeleteSubmissionEverywhere(id);

        TempData["Success"] = "Teslim silindi.";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public IActionResult BackfillLegacy()
    {
        ViewData["Title"] = "Legacy Teslim Backfill";
        ViewData["PageTitle"] = "Legacy Teslim Backfill";
        ViewData["PageSubtitle"] = "homework_submissions kayıtlarını canonical submissions koleksiyonuna güvenli şekilde merge eder.";

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BackfillLegacy(string confirmation)
    {
        ViewData["Title"] = "Legacy Teslim Backfill";
        ViewData["PageTitle"] = "Legacy Teslim Backfill";
        ViewData["PageSubtitle"] = "homework_submissions kayıtlarını canonical submissions koleksiyonuna güvenli şekilde merge eder.";

        if (!string.Equals((confirmation ?? "").Trim(), "BACKFILL", StringComparison.Ordinal))
        {
            TempData["Error"] = "Backfill çalıştırmak için onay alanına BACKFILL yazmalısınız.";
            return View();
        }

        var report = await BackfillLegacySubmissions();
        ViewBag.Report = report;
        TempData["Success"] = $"Backfill tamamlandı. Merge: {report.Merged}, Skip: {report.Skipped}, Hata: {report.Errors}.";

        return View();
    }

    private async Task<BackfillReport> BackfillLegacySubmissions()
    {
        var report = new BackfillReport();
        var snapshot = await _firestore.Db.Collection("homework_submissions").GetSnapshotAsync();

        foreach (var legacyDoc in snapshot.Documents)
        {
            report.Scanned++;

            try
            {
                var legacy = legacyDoc.ToDictionary();
                var assignmentId = FirstValue(legacy, "assignmentId", "AssignmentId", "homeworkId", "HomeworkId");
                var studentNo = OnlyDigits(FirstValue(legacy, "studentNo", "StudentNo", "studentNumber", "StudentNumber", "schoolNo", "SchoolNo"));
                var canonicalId = BuildSubmissionId(assignmentId, studentNo);

                if (string.IsNullOrWhiteSpace(canonicalId))
                {
                    report.Errors++;
                    report.Messages.Add($"Hata: {legacyDoc.Id} assignment/student bilgisi eksik.");
                    continue;
                }

                var canonicalRef = _firestore.Db.Collection("submissions").Document(canonicalId);
                var canonicalDoc = await canonicalRef.GetSnapshotAsync();
                var canonical = canonicalDoc.Exists
                    ? canonicalDoc.ToDictionary()
                    : new Dictionary<string, object>();
                var merged = canonicalDoc.Exists
                    ? MergeSubmissionData(canonical, legacy)
                    : new Dictionary<string, object>(legacy);

                merged["id"] = canonicalId;
                merged["Id"] = canonicalId;
                merged["assignmentId"] = assignmentId;
                merged["AssignmentId"] = assignmentId;
                merged["homeworkId"] = assignmentId;
                merged["HomeworkId"] = assignmentId;
                merged["studentNo"] = studentNo;
                merged["StudentNo"] = studentNo;
                merged["studentNumber"] = studentNo;
                merged["StudentNumber"] = studentNo;
                merged["legacySubmissionId"] = legacyDoc.Id;
                merged["LegacySubmissionId"] = legacyDoc.Id;
                merged["backfilledFrom"] = "homework_submissions";
                merged["BackfilledFrom"] = "homework_submissions";

                if (canonicalDoc.Exists &&
                    HasBackfillMarker(canonical, legacyDoc.Id) &&
                    !HasMeaningfulChanges(canonical, merged))
                {
                    report.Skipped++;
                    continue;
                }

                var now = Timestamp.GetCurrentTimestamp();
                merged["backfilledAt"] = now;
                merged["BackfilledAt"] = now;

                await canonicalRef.SetAsync(merged, SetOptions.MergeAll);
                report.Merged++;
            }
            catch (Exception ex)
            {
                report.Errors++;
                report.Messages.Add($"Hata: {legacyDoc.Id} - {ex.Message}");
            }
        }

        return report;
    }

    private static Dictionary<string, object> MergeSubmissionData(
        Dictionary<string, object> canonical,
        Dictionary<string, object> legacy
    )
    {
        var merged = new Dictionary<string, object>(canonical);

        foreach (var item in legacy)
        {
            if (!merged.TryGetValue(item.Key, out var current) || IsEmptyValue(current))
            {
                merged[item.Key] = item.Value;
            }
        }

        foreach (var key in new[] { "score", "Score", "grade", "Grade", "point", "Point", "feedback", "Feedback", "comment", "Comment" })
        {
            if (legacy.TryGetValue(key, out var legacyValue) && !IsEmptyValue(legacyValue) &&
                (!merged.TryGetValue(key, out var currentValue) || IsEmptyValue(currentValue)))
            {
                merged[key] = legacyValue;
            }
        }

        var legacyStatus = FirstValue(legacy, "status", "Status");
        var mergedStatus = FirstValue(merged, "status", "Status");

        if (IsEvaluatedStatus(legacyStatus) && !IsEvaluatedStatus(mergedStatus))
        {
            merged["status"] = "Değerlendirildi";
            merged["Status"] = "Değerlendirildi";
        }

        SetLatestDate(merged, legacy, "submittedAt", "SubmittedAt");
        SetLatestDate(merged, legacy, "updatedAt", "UpdatedAt");
        SetLatestDate(merged, legacy, "evaluatedAt", "EvaluatedAt");

        return merged;
    }

    private static bool HasBackfillMarker(Dictionary<string, object> data, string legacySubmissionId)
    {
        var source = FirstValue(data, "backfilledFrom", "BackfilledFrom");
        var id = FirstValue(data, "legacySubmissionId", "LegacySubmissionId");
        var hasDate = GetDate(data, "backfilledAt", "BackfilledAt").HasValue;

        return hasDate &&
               source == "homework_submissions" &&
               string.Equals(id, legacySubmissionId, StringComparison.Ordinal);
    }

    private static bool HasMeaningfulChanges(
        Dictionary<string, object> current,
        Dictionary<string, object> next
    )
    {
        foreach (var item in next)
        {
            if (IsBackfillTimestampKey(item.Key))
            {
                continue;
            }

            if (!current.TryGetValue(item.Key, out var currentValue))
            {
                return true;
            }

            if (!ValuesEqual(currentValue, item.Value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsBackfillTimestampKey(string key)
    {
        return string.Equals(key, "backfilledAt", StringComparison.Ordinal) ||
               string.Equals(key, "BackfilledAt", StringComparison.Ordinal);
    }

    private static bool ValuesEqual(object? left, object? right)
    {
        if (left == null && right == null)
        {
            return true;
        }

        if (left == null || right == null)
        {
            return false;
        }

        if (left is Timestamp leftTimestamp && right is Timestamp rightTimestamp)
        {
            return leftTimestamp.ToDateTime() == rightTimestamp.ToDateTime();
        }

        return string.Equals(left.ToString(), right.ToString(), StringComparison.Ordinal);
    }

    private async Task DeleteSubmissionEverywhere(string id)
    {
        var target = await FindSubmissionData(id);

        if (target == null)
        {
            foreach (var collectionName in new[] { "submissions", "homework_submissions" })
            {
                await _firestore.Db.Collection(collectionName).Document(id).DeleteAsync();
            }

            return;
        }

        var assignmentId = FirstValue(target, "assignmentId", "AssignmentId", "homeworkId", "HomeworkId");
        var studentNo = OnlyDigits(FirstValue(target, "studentNo", "StudentNo", "studentNumber", "StudentNumber", "schoolNo", "SchoolNo"));
        var targetKey = NormalizeKey($"{assignmentId}_{studentNo}");

        foreach (var collectionName in new[] { "submissions", "homework_submissions" })
        {
            var snapshot = await _firestore.Db.Collection(collectionName).GetSnapshotAsync();

            foreach (var doc in snapshot.Documents)
            {
                if (doc.Id == id)
                {
                    await doc.Reference.DeleteAsync();
                    continue;
                }

                var data = doc.ToDictionary();
                var docAssignmentId = FirstValue(data, "assignmentId", "AssignmentId", "homeworkId", "HomeworkId");
                var docStudentNo = OnlyDigits(FirstValue(data, "studentNo", "StudentNo", "studentNumber", "StudentNumber", "schoolNo", "SchoolNo"));

                if (!string.IsNullOrWhiteSpace(targetKey) &&
                    NormalizeKey($"{docAssignmentId}_{docStudentNo}") == targetKey)
                {
                    await doc.Reference.DeleteAsync();
                }
            }
        }
    }

    private async Task<Dictionary<string, object>?> FindSubmissionData(string id)
    {
        foreach (var collectionName in new[] { "submissions", "homework_submissions" })
        {
            var doc = await _firestore.Db.Collection(collectionName).Document(id).GetSnapshotAsync();

            if (doc.Exists)
            {
                return doc.ToDictionary();
            }
        }

        return null;
    }

    private static string FirstValue(
        Dictionary<string, object> data,
        params string[] keys
    )
    {
        return FirstValueOrDefault(data, "", keys);
    }

    private static string FirstValueOrDefault(
        Dictionary<string, object> data,
        string defaultValue,
        params string[] keys
    )
    {
        foreach (var key in keys)
        {
            if (data.TryGetValue(key, out var value) && value != null)
            {
                var text = value.ToString();

                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return defaultValue;
    }

    private static string OnlyDigits(string value)
    {
        return new string((value ?? "").Where(char.IsDigit).ToArray());
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

        value = value
            .Replace("ı", "i")
            .Replace("ğ", "g")
            .Replace("ü", "u")
            .Replace("ş", "s")
            .Replace("ö", "o")
            .Replace("ç", "c");

        return new string(value.Where(char.IsLetterOrDigit).ToArray());
    }

    private static string BuildSubmissionId(string assignmentId, string studentNo)
    {
        var assignmentKey = NormalizeKey(assignmentId);
        var studentKey = OnlyDigits(studentNo);

        if (string.IsNullOrWhiteSpace(assignmentKey) || string.IsNullOrWhiteSpace(studentKey))
        {
            return "";
        }

        return $"{assignmentKey}_{studentKey}";
    }

    private static bool IsEmptyValue(object? value)
    {
        return value == null || string.IsNullOrWhiteSpace(value.ToString());
    }

    private static bool IsEvaluatedStatus(string status)
    {
        var key = NormalizeKey(status);

        return key.Contains("degerlendirildi") ||
               key.Contains("notlandi") ||
               key.Contains("graded") ||
               key.Contains("evaluated");
    }

    private static void SetLatestDate(
        Dictionary<string, object> target,
        Dictionary<string, object> source,
        string lowerKey,
        string upperKey
    )
    {
        var targetDate = GetDate(target, lowerKey, upperKey);
        var sourceDate = GetDate(source, lowerKey, upperKey);

        if (!sourceDate.HasValue || (targetDate.HasValue && targetDate.Value >= sourceDate.Value))
        {
            return;
        }

        var timestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(sourceDate.Value, DateTimeKind.Utc));
        target[lowerKey] = timestamp;
        target[upperKey] = timestamp;
    }

    private static bool IsDeleted(Dictionary<string, object> data)
    {
        var deleted = FirstValue(data, "isDeleted", "IsDeleted", "deleted", "Deleted").Trim().ToLowerInvariant();
        var active = FirstValue(data, "isActive", "IsActive", "active", "Active").Trim().ToLowerInvariant();
        var status = NormalizeKey(FirstValue(data, "status", "Status"));

        return deleted is "true" or "1" or "evet" or "yes" ||
               active is "false" or "0" or "hayir" or "no" ||
               status is "silindi" or "deleted" or "pasif" or "inactive";
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

            if (DateTime.TryParse(value.ToString(), out var date))
            {
                return date;
            }
        }

        return null;
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

    public sealed class BackfillReport
    {
        public int Scanned { get; set; }
        public int Merged { get; set; }
        public int Skipped { get; set; }
        public int Errors { get; set; }
        public List<string> Messages { get; } = new();
    }
}
