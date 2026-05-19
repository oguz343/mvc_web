using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using mvc_web.Models;
using mvc_web.Services;
using System.Text.RegularExpressions;

namespace mvc_web.Controllers
{
    [Route("Teacher/Submissions")]
    public class TeacherSubmissionsController : Controller
    {
        private readonly FirestoreDb _firestore;
        private readonly SessionService _session;

        public TeacherSubmissionsController(
            FirestoreDb firestore,
            SessionService session
        )
        {
            _firestore = firestore;
            _session = session;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            if (!_session.IsTeacher(HttpContext))
            {
                return RedirectToAction("Login", "Auth");
            }

            var teacherId = _session.GetUserId(HttpContext) ?? "";
            var teacherName = _session.GetName(HttpContext) ?? "Öğretmen";
            var teacherNo = _session.GetNumber(HttpContext) ?? "-";

            ViewBag.TeacherName = teacherName;
            ViewBag.TeacherNo = teacherNo;

            var submissions = new List<TeacherSubmissionViewModel>();

            submissions.AddRange(await LoadSubmissionsFromCollection(
                submissionCollection: "submissions",
                teacherId: teacherId,
                teacherName: teacherName,
                teacherNo: teacherNo
            ));

            submissions.AddRange(await LoadSubmissionsFromCollection(
                submissionCollection: "homework_submissions",
                teacherId: teacherId,
                teacherName: teacherName,
                teacherNo: teacherNo
            ));

            submissions = submissions
                .GroupBy(x => SubmissionGroupKey(x))
                .Select(x => x.First())
                .OrderByDescending(x => x.SubmittedAt ?? DateTime.MinValue)
                .ToList();

            return View(submissions);
        }

        private static string SubmissionGroupKey(TeacherSubmissionViewModel item)
        {
            var homeworkKey = NormalizeText(item.HomeworkId);
            var studentKey = OnlyDigits(item.StudentNo);

            if (!string.IsNullOrWhiteSpace(homeworkKey) && !string.IsNullOrWhiteSpace(studentKey))
            {
                return $"{homeworkKey}_{studentKey}";
            }

            return NormalizeText($"{item.SubmissionCollection}_{item.Id}");
        }

        [HttpPost("Evaluate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Evaluate(
            string submissionCollection,
            string submissionId,
            string grade,
            string feedback
        )
        {
            if (!_session.IsTeacher(HttpContext))
            {
                return RedirectToAction("Login", "Auth");
            }

            if (string.IsNullOrWhiteSpace(submissionId))
            {
                TempData["Error"] = "Teslim bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            submissionCollection = string.IsNullOrWhiteSpace(submissionCollection)
                ? "homework_submissions"
                : submissionCollection.Trim();

            if (submissionCollection != "submissions" && submissionCollection != "homework_submissions")
            {
                TempData["Error"] = "Teslim bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            grade = CleanText(grade);
            feedback = CleanText(feedback);

            if (string.IsNullOrWhiteSpace(grade))
            {
                TempData["Error"] = "Not boş bırakılamaz.";
                return RedirectToAction(nameof(Index));
            }

            var teacherId = _session.GetUserId(HttpContext) ?? "";
            var teacherName = _session.GetName(HttpContext) ?? "";
            var teacherNo = _session.GetNumber(HttpContext) ?? "";
            var submissionDoc = await _firestore
                .Collection(submissionCollection)
                .Document(submissionId.Trim())
                .GetSnapshotAsync();

            if (!submissionDoc.Exists)
            {
                TempData["Error"] = "Teslim bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            var submissionData = submissionDoc.ToDictionary();
            var homeworkId = FirstNonEmpty(
                GetString(submissionData, "homeworkId", "HomeworkId"),
                GetString(submissionData, "assignmentId", "AssignmentId")
            );

            var homeworkCollection = FirstNonEmpty(
                GetString(submissionData, "homeworkCollection", "HomeworkCollection"),
                GetString(submissionData, "assignmentCollection", "AssignmentCollection"),
                "homeworks"
            );

            var homeworkDoc = await FindHomeworkDoc(homeworkCollection, homeworkId);

            if (homeworkDoc == null ||
                !HomeworkBelongsToTeacher(homeworkDoc.ToDictionary(), teacherId, teacherName, teacherNo))
            {
                TempData["Error"] = "Bu teslim size ait bir ödeve bağlı değil.";
                return RedirectToAction(nameof(Index));
            }

            var now = Timestamp.GetCurrentTimestamp();

            await _firestore
                .Collection(submissionCollection)
                .Document(submissionId.Trim())
                .SetAsync(
                    new Dictionary<string, object>
                    {
                        { "grade", grade },
                        { "Grade", grade },
                        { "feedback", feedback },
                        { "Feedback", feedback },
                        { "status", "Değerlendirildi" },
                        { "Status", "Değerlendirildi" },
                        { "evaluatedAt", now },
                        { "EvaluatedAt", now },
                        { "updatedAt", now },
                        { "UpdatedAt", now }
                    },
                    SetOptions.MergeAll
                );

            TempData["Success"] = "Teslim değerlendirildi.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<List<TeacherSubmissionViewModel>> LoadSubmissionsFromCollection(
            string submissionCollection,
            string teacherId,
            string teacherName,
            string teacherNo
        )
        {
            var result = new List<TeacherSubmissionViewModel>();

            QuerySnapshot snapshot;

            try
            {
                snapshot = await _firestore
                    .Collection(submissionCollection)
                    .GetSnapshotAsync();
            }
            catch
            {
                return result;
            }

            foreach (var submissionDoc in snapshot.Documents)
            {
                var submissionData = submissionDoc.ToDictionary();

                var homeworkId = FirstNonEmpty(
                    GetString(submissionData, "homeworkId", "HomeworkId"),
                    GetString(submissionData, "assignmentId", "AssignmentId")
                );

                if (string.IsNullOrWhiteSpace(homeworkId))
                {
                    continue;
                }

                var homeworkCollection = FirstNonEmpty(
                    GetString(submissionData, "homeworkCollection", "HomeworkCollection"),
                    GetString(submissionData, "assignmentCollection", "AssignmentCollection"),
                    "homeworks"
                );

                var homeworkDoc = await FindHomeworkDoc(homeworkCollection, homeworkId);

                if (homeworkDoc == null)
                {
                    continue;
                }

                var homeworkData = homeworkDoc.ToDictionary();

                if (!HomeworkBelongsToTeacher(homeworkData, teacherId, teacherName, teacherNo))
                {
                    continue;
                }

                var item = new TeacherSubmissionViewModel
                {
                    Id = submissionDoc.Id,
                    SubmissionCollection = submissionCollection,

                    HomeworkId = homeworkDoc.Id,
                    HomeworkCollection = homeworkCollection,

                    HomeworkTitle = FirstNonEmpty(
                        GetString(homeworkData, "title", "Title"),
                        GetString(homeworkData, "name", "Name"),
                        "Ödev"
                    ),

                    LessonName = FirstNonEmpty(
                        GetString(homeworkData, "lessonName", "LessonName"),
                        GetString(homeworkData, "lesson", "Lesson"),
                        GetString(homeworkData, "courseName", "CourseName"),
                        "-"
                    ),

                    ClassName = NormalizeClassName(FirstNonEmpty(
                        GetString(homeworkData, "className", "ClassName"),
                        GetString(homeworkData, "targetClass", "TargetClass"),
                        GetString(homeworkData, "class", "Class")
                    )),

                    StudentId = GetString(submissionData, "studentId", "StudentId"),

                    StudentName = FirstNonEmpty(
                        GetString(submissionData, "studentName", "StudentName"),
                        GetString(submissionData, "name", "Name"),
                        "-"
                    ),

                    StudentNo = OnlyDigits(FirstNonEmpty(
                        GetString(submissionData, "studentNo", "StudentNo"),
                        GetString(submissionData, "studentNumber", "StudentNumber"),
                        GetString(submissionData, "schoolNo", "SchoolNo"),
                        GetString(submissionData, "number", "Number")
                    )),

                    AnswerText = FirstNonEmpty(
                        GetString(submissionData, "answerText", "AnswerText"),
                        GetString(submissionData, "answer", "Answer"),
                        GetString(submissionData, "content", "Content"),
                        GetString(submissionData, "text", "Text")
                    ),

                    FileName = GetString(submissionData, "fileName", "FileName"),

                    FileUrl = FirstNonEmpty(
                        GetString(submissionData, "fileUrl", "FileUrl"),
                        GetString(submissionData, "link", "Link"),
                        GetString(submissionData, "url", "Url")
                    ),

                    Status = FirstNonEmpty(
                        GetString(submissionData, "status", "Status"),
                        "Bekliyor"
                    ),

                    Grade = FirstNonEmpty(
                        GetString(submissionData, "grade", "Grade"),
                        GetString(submissionData, "score", "Score")
                    ),

                    Feedback = FirstNonEmpty(
                        GetString(submissionData, "feedback", "Feedback"),
                        GetString(submissionData, "comment", "Comment")
                    ),

                    SubmittedAt = GetDate(submissionData, "submittedAt", "SubmittedAt", "createdAt", "CreatedAt"),
                    EvaluatedAt = GetDate(submissionData, "evaluatedAt", "EvaluatedAt")
                };

                result.Add(item);
            }

            return result;
        }

        private async Task<DocumentSnapshot?> FindHomeworkDoc(string homeworkCollection, string homeworkId)
        {
            var collections = new List<string>();

            if (!string.IsNullOrWhiteSpace(homeworkCollection))
            {
                collections.Add(homeworkCollection);
            }

            collections.Add("homeworks");
            collections.Add("assignments");

            foreach (var collection in collections.Distinct())
            {
                try
                {
                    var doc = await _firestore
                        .Collection(collection)
                        .Document(homeworkId)
                        .GetSnapshotAsync();

                    if (doc.Exists)
                    {
                        return doc;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private bool HomeworkBelongsToTeacher(
            Dictionary<string, object> homeworkData,
            string teacherId,
            string teacherName,
            string teacherNo
        )
        {
            var homeworkTeacherId = FirstNonEmpty(
                GetString(homeworkData, "teacherId", "TeacherId"),
                GetString(homeworkData, "createdById", "CreatedById"),
                GetString(homeworkData, "ownerId", "OwnerId")
            );

            var homeworkTeacherName = FirstNonEmpty(
                GetString(homeworkData, "teacherName", "TeacherName"),
                GetString(homeworkData, "teacher", "Teacher"),
                GetString(homeworkData, "createdByName", "CreatedByName")
            );

            var homeworkTeacherNo = OnlyDigits(FirstNonEmpty(
                GetString(homeworkData, "teacherNo", "TeacherNo"),
                GetString(homeworkData, "teacherNumber", "TeacherNumber"),
                GetString(homeworkData, "createdByNumber", "CreatedByNumber")
            ));

            var hasAnyTeacherInfo =
                !string.IsNullOrWhiteSpace(homeworkTeacherId) ||
                !string.IsNullOrWhiteSpace(homeworkTeacherName) ||
                !string.IsNullOrWhiteSpace(homeworkTeacherNo);

            if (!hasAnyTeacherInfo)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(homeworkTeacherId) &&
                homeworkTeacherId == teacherId)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(homeworkTeacherNo) &&
                homeworkTeacherNo == OnlyDigits(teacherNo))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(homeworkTeacherName) &&
                NormalizeText(homeworkTeacherName) == NormalizeText(teacherName))
            {
                return true;
            }

            return false;
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

        private static string CleanText(string value)
        {
            return (value ?? "")
                .Replace("\u00A0", " ")
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
        }

        private static string OnlyDigits(string value)
        {
            return new string((value ?? "").Where(char.IsDigit).ToArray());
        }

        private static string NormalizeText(string value)
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
                .Replace(" ", "");
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

            return value ?? "";
        }
    }
}
