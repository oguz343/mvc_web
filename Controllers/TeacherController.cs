using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace mvc_web.Controllers
{
    public class TeacherController : Controller
    {
        private readonly FirestoreDb _firestore;

        public TeacherController(FirestoreDb firestore)
        {
            _firestore = firestore;
        }

        public async Task<IActionResult> Index()
        {
            if (!IsTeacherLoggedIn())
            {
                return RedirectToAction("Login", "Auth");
            }

            var teacherNumber =
                HttpContext.Session.GetString("Number") ??
                HttpContext.Session.GetString("UserNumber") ??
                "";

            var teacherName =
                HttpContext.Session.GetString("Name") ??
                HttpContext.Session.GetString("UserName") ??
                "";

            var teacherBranch =
                HttpContext.Session.GetString("Branch") ??
                HttpContext.Session.GetString("TeacherBranch") ??
                "";

            var teacherProfile = await LoadTeacherProfile(teacherNumber, teacherName);

            if (string.IsNullOrWhiteSpace(teacherName))
            {
                teacherName = GetString(teacherProfile, "name", "Name", "userName", "UserName");
            }

            if (string.IsNullOrWhiteSpace(teacherNumber))
            {
                teacherNumber = OnlyDigits(FirstNonEmpty(
                    GetString(teacherProfile, "schoolNo", "SchoolNo"),
                    GetString(teacherProfile, "number", "Number")
                ));
            }

            if (string.IsNullOrWhiteSpace(teacherBranch))
            {
                teacherBranch = GetString(teacherProfile, "branch", "Branch", "teacherBranch", "TeacherBranch");
            }

            var lessons = await LoadTeacherLessons(teacherNumber, teacherName, teacherBranch);
            var assignments = await LoadTeacherAssignments(teacherNumber, teacherName, teacherBranch, lessons);
            var submissions = await LoadTeacherSubmissions(teacherNumber, teacherName, teacherBranch, assignments, lessons);

            ViewData["TeacherName"] = teacherName;
            ViewData["TeacherNo"] = teacherNumber;
            ViewData["TeacherBranch"] = teacherBranch;

            ViewData["Lessons"] = lessons;
            ViewData["TeacherLessons"] = lessons;
            ViewData["MyLessons"] = lessons;

            ViewData["Assignments"] = assignments;
            ViewData["Homeworks"] = assignments;
            ViewData["TeacherAssignments"] = assignments;
            ViewData["MyAssignments"] = assignments;

            ViewData["Submissions"] = submissions.Take(5).ToList();
            ViewData["RecentSubmissions"] = submissions.Take(5).ToList();
            ViewData["LastSubmissions"] = submissions.Take(5).ToList();

            ViewData["LessonCount"] = lessons.Count;
            ViewData["AssignmentCount"] = assignments.Count;
            ViewData["SubmissionCount"] = submissions.Count;
            ViewData["EvaluatedCount"] = submissions.Count(x => IsEvaluated(x));

            return View();
        }

        public async Task<IActionResult> Assignments()
        {
            if (!IsTeacherLoggedIn())
            {
                return RedirectToAction("Login", "Auth");
            }

            var teacherNumber =
                HttpContext.Session.GetString("Number") ??
                HttpContext.Session.GetString("UserNumber") ??
                "";

            var teacherName =
                HttpContext.Session.GetString("Name") ??
                HttpContext.Session.GetString("UserName") ??
                "";

            var teacherBranch =
                HttpContext.Session.GetString("Branch") ??
                HttpContext.Session.GetString("TeacherBranch") ??
                "";

            var teacherProfile = await LoadTeacherProfile(teacherNumber, teacherName);

            if (string.IsNullOrWhiteSpace(teacherName))
            {
                teacherName = GetString(teacherProfile, "name", "Name", "userName", "UserName");
            }

            if (string.IsNullOrWhiteSpace(teacherNumber))
            {
                teacherNumber = OnlyDigits(FirstNonEmpty(
                    GetString(teacherProfile, "schoolNo", "SchoolNo"),
                    GetString(teacherProfile, "number", "Number")
                ));
            }

            if (string.IsNullOrWhiteSpace(teacherBranch))
            {
                teacherBranch = GetString(teacherProfile, "branch", "Branch", "teacherBranch", "TeacherBranch");
            }

            var lessons = await LoadTeacherLessons(teacherNumber, teacherName, teacherBranch);
            var assignments = await LoadTeacherAssignments(teacherNumber, teacherName, teacherBranch, lessons);

            ViewData["TeacherName"] = teacherName;
            ViewData["TeacherNo"] = teacherNumber;
            ViewData["TeacherBranch"] = teacherBranch;

            ViewData["Lessons"] = lessons;
            ViewData["Assignments"] = assignments;
            ViewData["Homeworks"] = assignments;
            ViewData["TeacherAssignments"] = assignments;
            ViewData["MyAssignments"] = assignments;

            return View(assignments);
        }

        [HttpGet]
        public async Task<IActionResult> CreateAssignment()
        {
            if (!IsTeacherLoggedIn())
            {
                return RedirectToAction("Login", "Auth");
            }

            var teacherNumber =
                HttpContext.Session.GetString("Number") ??
                HttpContext.Session.GetString("UserNumber") ??
                "";

            var teacherName =
                HttpContext.Session.GetString("Name") ??
                HttpContext.Session.GetString("UserName") ??
                "";

            var teacherBranch =
                HttpContext.Session.GetString("Branch") ??
                HttpContext.Session.GetString("TeacherBranch") ??
                "";

            var teacherProfile = await LoadTeacherProfile(teacherNumber, teacherName);

            if (string.IsNullOrWhiteSpace(teacherName))
            {
                teacherName = GetString(teacherProfile, "name", "Name", "userName", "UserName");
            }

            if (string.IsNullOrWhiteSpace(teacherNumber))
            {
                teacherNumber = OnlyDigits(FirstNonEmpty(
                    GetString(teacherProfile, "schoolNo", "SchoolNo"),
                    GetString(teacherProfile, "number", "Number")
                ));
            }

            if (string.IsNullOrWhiteSpace(teacherBranch))
            {
                teacherBranch = GetString(teacherProfile, "branch", "Branch", "teacherBranch", "TeacherBranch");
            }

            var lessons = await LoadTeacherLessons(teacherNumber, teacherName, teacherBranch);

            ViewData["Lessons"] = lessons;
            ViewData["TeacherLessons"] = lessons;
            ViewData["TeacherName"] = teacherName;
            ViewData["TeacherNo"] = teacherNumber;
            ViewData["TeacherBranch"] = teacherBranch;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAssignment(IFormCollection form)
        {
            if (!IsTeacherLoggedIn())
            {
                return RedirectToAction("Login", "Auth");
            }

            var teacherNumber =
                HttpContext.Session.GetString("Number") ??
                HttpContext.Session.GetString("UserNumber") ??
                "";

            var teacherName =
                HttpContext.Session.GetString("Name") ??
                HttpContext.Session.GetString("UserName") ??
                "";

            var teacherBranch =
                HttpContext.Session.GetString("Branch") ??
                HttpContext.Session.GetString("TeacherBranch") ??
                "";

            var teacherProfile = await LoadTeacherProfile(teacherNumber, teacherName);

            if (string.IsNullOrWhiteSpace(teacherName))
            {
                teacherName = GetString(teacherProfile, "name", "Name", "userName", "UserName");
            }

            if (string.IsNullOrWhiteSpace(teacherNumber))
            {
                teacherNumber = OnlyDigits(FirstNonEmpty(
                    GetString(teacherProfile, "schoolNo", "SchoolNo"),
                    GetString(teacherProfile, "number", "Number")
                ));
            }

            if (string.IsNullOrWhiteSpace(teacherBranch))
            {
                teacherBranch = GetString(teacherProfile, "branch", "Branch", "teacherBranch", "TeacherBranch");
            }

            var lessonId = FormValue(form, "lessonId", "LessonId", "lesson", "Lesson");
            var selectedLesson = await FindLessonById(lessonId);

            var title = FirstNonEmpty(
                FormValue(form, "title", "Title"),
                FormValue(form, "name", "Name"),
                FormValue(form, "assignmentTitle", "AssignmentTitle"),
                FormValue(form, "homeworkTitle", "HomeworkTitle")
            );

            var description = FirstNonEmpty(
                FormValue(form, "description", "Description"),
                FormValue(form, "content", "Content"),
                FormValue(form, "text", "Text")
            );

            var lessonName = FirstNonEmpty(
                GetString(selectedLesson, "name", "Name", "lessonName", "LessonName", "title", "Title"),
                FormValue(form, "lessonName", "LessonName"),
                FormValue(form, "courseName", "CourseName"),
                FormValue(form, "course", "Course")
            );

            var className = NormalizeClassName(FirstNonEmpty(
                GetString(selectedLesson, "className", "ClassName", "class", "Class", "targetClass", "TargetClass"),
                FormValue(form, "className", "ClassName"),
                FormValue(form, "class", "Class"),
                FormValue(form, "targetClass", "TargetClass")
            ));

            var dueDateText = FirstNonEmpty(
                FormValue(form, "dueDate", "DueDate"),
                FormValue(form, "deadline", "Deadline"),
                FormValue(form, "endDate", "EndDate")
            );

            var fileType = FirstNonEmpty(
                FormValue(form, "fileType", "FileType"),
                FormValue(form, "type", "Type"),
                FormValue(form, "submissionType", "SubmissionType"),
                "Dosya"
            );

            if (string.IsNullOrWhiteSpace(title))
            {
                TempData["Error"] = "Ödev başlığı boş bırakılamaz.";
                return RedirectToAction(nameof(CreateAssignment));
            }

            if (string.IsNullOrWhiteSpace(lessonName))
            {
                lessonName = "Ders";
            }

            if (string.IsNullOrWhiteSpace(className))
            {
                className = "-";
            }

            var now = Timestamp.GetCurrentTimestamp();

            var data = new Dictionary<string, object>
            {
                { "title", title },
                { "name", title },
                { "homeworkTitle", title },
                { "assignmentTitle", title },

                { "description", description },
                { "content", description },
                { "text", description },

                { "lessonId", lessonId },
                { "lessonName", lessonName },
                { "lesson", lessonName },
                { "courseName", lessonName },
                { "course", lessonName },

                { "className", className },
                { "class", className },
                { "targetClass", className },

                { "teacherName", teacherName },
                { "teacher", teacherName },
                { "teacherNo", teacherNumber },
                { "teacherNumber", teacherNumber },
                { "branch", teacherBranch },
                { "teacherBranch", teacherBranch },

                { "fileType", fileType },
                { "type", fileType },
                { "submissionType", fileType },

                { "status", "Aktif" },
                { "createdAt", now },
                { "updatedAt", now },
                { "isDeleted", false },
                { "isActive", true }
            };

            if (DateTime.TryParse(dueDateText, out DateTime dueDate))
            {
                data["dueDate"] = Timestamp.FromDateTime(DateTime.SpecifyKind(dueDate, DateTimeKind.Utc));
                data["deadline"] = Timestamp.FromDateTime(DateTime.SpecifyKind(dueDate, DateTimeKind.Utc));
                data["endDate"] = Timestamp.FromDateTime(DateTime.SpecifyKind(dueDate, DateTimeKind.Utc));
            }
            else
            {
                data["dueDateText"] = dueDateText;
            }

            var docRef = await _firestore.Collection("homeworks").AddAsync(data);

            data["id"] = docRef.Id;
            data["Id"] = docRef.Id;

            await _firestore.Collection("assignments").Document(docRef.Id).SetAsync(data, SetOptions.MergeAll);

            TempData["Success"] = "Ödev oluşturuldu.";
            return RedirectToAction(nameof(Assignments));
        }

        [HttpGet]
        public async Task<IActionResult> EditAssignment(string id)
        {
            if (!IsTeacherLoggedIn())
            {
                return RedirectToAction("Login", "Auth");
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Ödev bulunamadı.";
                return RedirectToAction(nameof(Assignments));
            }

            var found = await FindDocumentInCollections(id, "homeworks", "assignments");

            if (found.Doc == null || !found.Doc.Exists)
            {
                TempData["Error"] = "Ödev bulunamadı.";
                return RedirectToAction(nameof(Assignments));
            }

            var data = found.Doc.ToDictionary();
            data["Id"] = found.Doc.Id;

            return View(data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAssignment(string id, IFormCollection form)
        {
            if (!IsTeacherLoggedIn())
            {
                return RedirectToAction("Login", "Auth");
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Ödev bulunamadı.";
                return RedirectToAction(nameof(Assignments));
            }

            var title = FirstNonEmpty(
                FormValue(form, "title", "Title"),
                FormValue(form, "name", "Name"),
                FormValue(form, "assignmentTitle", "AssignmentTitle"),
                FormValue(form, "homeworkTitle", "HomeworkTitle")
            );

            var description = FirstNonEmpty(
                FormValue(form, "description", "Description"),
                FormValue(form, "content", "Content"),
                FormValue(form, "text", "Text")
            );

            var lessonName = FirstNonEmpty(
                FormValue(form, "lessonName", "LessonName"),
                FormValue(form, "lesson", "Lesson"),
                FormValue(form, "courseName", "CourseName"),
                FormValue(form, "course", "Course")
            );

            var className = NormalizeClassName(FirstNonEmpty(
                FormValue(form, "className", "ClassName"),
                FormValue(form, "class", "Class"),
                FormValue(form, "targetClass", "TargetClass")
            ));

            var dueDateText = FirstNonEmpty(
                FormValue(form, "dueDate", "DueDate"),
                FormValue(form, "deadline", "Deadline"),
                FormValue(form, "endDate", "EndDate")
            );

            var fileType = FirstNonEmpty(
                FormValue(form, "fileType", "FileType"),
                FormValue(form, "type", "Type"),
                FormValue(form, "submissionType", "SubmissionType")
            );

            var update = new Dictionary<string, object>
            {
                { "updatedAt", Timestamp.GetCurrentTimestamp() }
            };

            if (!string.IsNullOrWhiteSpace(title))
            {
                update["title"] = title;
                update["name"] = title;
                update["homeworkTitle"] = title;
                update["assignmentTitle"] = title;
            }

            update["description"] = description;
            update["content"] = description;
            update["text"] = description;

            if (!string.IsNullOrWhiteSpace(lessonName))
            {
                update["lessonName"] = lessonName;
                update["lesson"] = lessonName;
                update["courseName"] = lessonName;
                update["course"] = lessonName;
            }

            if (!string.IsNullOrWhiteSpace(className))
            {
                update["className"] = className;
                update["class"] = className;
                update["targetClass"] = className;
            }

            if (!string.IsNullOrWhiteSpace(fileType))
            {
                update["fileType"] = fileType;
                update["type"] = fileType;
                update["submissionType"] = fileType;
            }

            if (DateTime.TryParse(dueDateText, out DateTime dueDate))
            {
                update["dueDate"] = Timestamp.FromDateTime(DateTime.SpecifyKind(dueDate, DateTimeKind.Utc));
                update["deadline"] = Timestamp.FromDateTime(DateTime.SpecifyKind(dueDate, DateTimeKind.Utc));
                update["endDate"] = Timestamp.FromDateTime(DateTime.SpecifyKind(dueDate, DateTimeKind.Utc));
            }

            await UpdateDocumentInCollections(id, update, "homeworks", "assignments");

            TempData["Success"] = "Ödev güncellendi.";
            return RedirectToAction(nameof(Assignments));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAssignment(string id)
        {
            if (!IsTeacherLoggedIn())
            {
                return RedirectToAction("Login", "Auth");
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Ödev bulunamadı.";
                return RedirectToAction(nameof(Assignments));
            }

            var update = new Dictionary<string, object>
            {
                { "isDeleted", true },
                { "deleted", true },
                { "status", "Silindi" },
                { "deletedAt", Timestamp.GetCurrentTimestamp() },
                { "updatedAt", Timestamp.GetCurrentTimestamp() }
            };

            await UpdateDocumentInCollections(id, update, "homeworks", "assignments");

            TempData["Success"] = "Ödev silindi.";
            return RedirectToAction(nameof(Assignments));
        }

        [HttpGet]
        public async Task<IActionResult> Submissions(string assignmentId = "")
        {
            if (!IsTeacherLoggedIn())
            {
                return RedirectToAction("Login", "Auth");
            }

            var teacherNumber =
                HttpContext.Session.GetString("Number") ??
                HttpContext.Session.GetString("UserNumber") ??
                "";

            var teacherName =
                HttpContext.Session.GetString("Name") ??
                HttpContext.Session.GetString("UserName") ??
                "";

            var teacherBranch =
                HttpContext.Session.GetString("Branch") ??
                HttpContext.Session.GetString("TeacherBranch") ??
                "";

            var teacherProfile = await LoadTeacherProfile(teacherNumber, teacherName);

            if (string.IsNullOrWhiteSpace(teacherName))
            {
                teacherName = GetString(teacherProfile, "name", "Name", "userName", "UserName");
            }

            if (string.IsNullOrWhiteSpace(teacherNumber))
            {
                teacherNumber = OnlyDigits(FirstNonEmpty(
                    GetString(teacherProfile, "schoolNo", "SchoolNo"),
                    GetString(teacherProfile, "number", "Number")
                ));
            }

            if (string.IsNullOrWhiteSpace(teacherBranch))
            {
                teacherBranch = GetString(teacherProfile, "branch", "Branch", "teacherBranch", "TeacherBranch");
            }

            var lessons = await LoadTeacherLessons(teacherNumber, teacherName, teacherBranch);
            var assignments = await LoadTeacherAssignments(teacherNumber, teacherName, teacherBranch, lessons);
            var submissions = await LoadTeacherSubmissions(teacherNumber, teacherName, teacherBranch, assignments, lessons);

            if (!string.IsNullOrWhiteSpace(assignmentId))
            {
                submissions = submissions
                    .Where(x =>
                        GetText(x, "HomeworkId") == assignmentId ||
                        GetText(x, "AssignmentId") == assignmentId ||
                        GetText(x, "homeworkId") == assignmentId ||
                        GetText(x, "assignmentId") == assignmentId)
                    .ToList();
            }

            ViewData["Submissions"] = submissions;
            ViewData["RecentSubmissions"] = submissions;
            ViewData["TotalSubmissionCount"] = submissions.Count;
            ViewData["EvaluatedCount"] = submissions.Count(x => IsEvaluated(x));
            ViewData["PendingCount"] = submissions.Count(x => !IsEvaluated(x));

            return View(submissions);
        }

        [HttpGet]
        public async Task<IActionResult> EvaluateSubmission(string id)
        {
            if (!IsTeacherLoggedIn())
            {
                return RedirectToAction("Login", "Auth");
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Teslim bulunamadı.";
                return RedirectToAction(nameof(Submissions));
            }

            var found = await FindDocumentInCollections(id, "homework_submissions", "submissions");

            if (found.Doc == null || !found.Doc.Exists)
            {
                TempData["Error"] = "Teslim bulunamadı.";
                return RedirectToAction(nameof(Submissions));
            }

            var data = found.Doc.ToDictionary();
            data["Id"] = found.Doc.Id;

            return View(data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EvaluateSubmission(string id, IFormCollection form)
        {
            if (!IsTeacherLoggedIn())
            {
                return RedirectToAction("Login", "Auth");
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Teslim bulunamadı.";
                return RedirectToAction(nameof(Submissions));
            }

            var score = FirstNonEmpty(
                FormValue(form, "score", "Score"),
                FormValue(form, "grade", "Grade"),
                FormValue(form, "point", "Point"),
                FormValue(form, "not", "Not")
            );

            var feedback = FirstNonEmpty(
                FormValue(form, "feedback", "Feedback"),
                FormValue(form, "comment", "Comment"),
                FormValue(form, "geriDonus", "GeriDonus")
            );

            var update = new Dictionary<string, object>
            {
                { "score", score },
                { "grade", score },
                { "point", score },
                { "not", score },
                { "feedback", feedback },
                { "comment", feedback },
                { "geriDonus", feedback },
                { "status", "Değerlendirildi" },
                { "evaluatedAt", Timestamp.GetCurrentTimestamp() },
                { "updatedAt", Timestamp.GetCurrentTimestamp() }
            };

            await UpdateDocumentInCollections(id, update, "homework_submissions", "submissions");

            TempData["Success"] = "Teslim değerlendirildi.";
            return RedirectToAction(nameof(Submissions));
        }

        private bool IsTeacherLoggedIn()
        {
            var role =
                HttpContext.Session.GetString("Role") ??
                HttpContext.Session.GetString("UserRole") ??
                "";

            var roleKey = NormalizeKey(role);

            return roleKey == "ogretmen" || roleKey == "teacher";
        }

        private async Task<Dictionary<string, object>> LoadTeacherProfile(string teacherNumber, string teacherName)
        {
            try
            {
                var snapshot = await _firestore.Collection("users").GetSnapshotAsync();

                var numberKey = OnlyDigits(teacherNumber);
                var nameKey = NormalizeKey(teacherName);

                foreach (var doc in snapshot.Documents)
                {
                    var data = doc.ToDictionary();

                    if (IsDeleted(data))
                    {
                        continue;
                    }

                    var role = NormalizeKey(GetString(data, "role", "Role"));

                    if (role != "ogretmen" && role != "teacher")
                    {
                        continue;
                    }

                    var docNumber = OnlyDigits(FirstNonEmpty(
                        GetString(data, "schoolNo", "SchoolNo"),
                        GetString(data, "number", "Number")
                    ));

                    var docName = NormalizeKey(GetString(data, "name", "Name"));

                    if ((!string.IsNullOrWhiteSpace(numberKey) && docNumber == numberKey) ||
                        (!string.IsNullOrWhiteSpace(nameKey) && docName == nameKey))
                    {
                        data["Id"] = doc.Id;
                        return data;
                    }
                }
            }
            catch
            {
            }

            return new Dictionary<string, object>();
        }

        private async Task<List<Dictionary<string, object>>> LoadTeacherLessons(
            string teacherNumber,
            string teacherName,
            string teacherBranch
        )
        {
            var result = new List<Dictionary<string, object>>();

            try
            {
                var snapshot = await _firestore.Collection("lessons").GetSnapshotAsync();

                foreach (var doc in snapshot.Documents)
                {
                    var data = doc.ToDictionary();

                    if (IsDeleted(data))
                    {
                        continue;
                    }

                    var docTeacherNo = OnlyDigits(FirstNonEmpty(
                        GetString(data, "teacherNo", "TeacherNo"),
                        GetString(data, "teacherNumber", "TeacherNumber"),
                        GetString(data, "number", "Number")
                    ));

                    var docTeacherName = NormalizeKey(FirstNonEmpty(
                        GetString(data, "teacherName", "TeacherName"),
                        GetString(data, "teacher", "Teacher")
                    ));

                    var docBranch = NormalizeKey(FirstNonEmpty(
                        GetString(data, "branch", "Branch"),
                        GetString(data, "teacherBranch", "TeacherBranch")
                    ));

                    var sameTeacher =
                        (!string.IsNullOrWhiteSpace(teacherNumber) && docTeacherNo == OnlyDigits(teacherNumber)) ||
                        (!string.IsNullOrWhiteSpace(teacherName) && docTeacherName == NormalizeKey(teacherName)) ||
                        (!string.IsNullOrWhiteSpace(teacherBranch) && docBranch == NormalizeKey(teacherBranch));

                    if (!sameTeacher)
                    {
                        continue;
                    }

                    var lessonName = FirstNonEmpty(
                        GetString(data, "name", "Name"),
                        GetString(data, "lessonName", "LessonName"),
                        GetString(data, "title", "Title"),
                        "Ders"
                    );

                    var className = NormalizeClassName(FirstNonEmpty(
                        GetString(data, "className", "ClassName"),
                        GetString(data, "class", "Class"),
                        GetString(data, "targetClass", "TargetClass")
                    ));

                    data["Id"] = doc.Id;
                    data["Name"] = lessonName;
                    data["LessonName"] = lessonName;
                    data["Title"] = lessonName;
                    data["ClassName"] = string.IsNullOrWhiteSpace(className) ? "-" : className;
                    data["Branch"] = FirstNonEmpty(
                        GetString(data, "branch", "Branch"),
                        GetString(data, "teacherBranch", "TeacherBranch"),
                        teacherBranch
                    );

                    result.Add(data);
                }
            }
            catch
            {
            }

            return result
                .GroupBy(x => NormalizeKey($"{GetText(x, "Name")}_{GetText(x, "ClassName")}"))
                .Select(x => x.First())
                .ToList();
        }

        private async Task<List<Dictionary<string, object>>> LoadTeacherAssignments(
            string teacherNumber,
            string teacherName,
            string teacherBranch,
            List<Dictionary<string, object>> teacherLessons
        )
        {
            var result = new List<Dictionary<string, object>>();

            var lessonKeys = teacherLessons
                .Select(x => NormalizeKey($"{GetText(x, "Name")}_{GetText(x, "ClassName")}"))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet();

            var collections = new[] { "homeworks", "assignments" };

            foreach (var collection in collections)
            {
                try
                {
                    var snapshot = await _firestore.Collection(collection).GetSnapshotAsync();

                    foreach (var doc in snapshot.Documents)
                    {
                        var data = doc.ToDictionary();

                        if (IsDeleted(data))
                        {
                            continue;
                        }

                        var docTeacherNo = OnlyDigits(FirstNonEmpty(
                            GetString(data, "teacherNo", "TeacherNo"),
                            GetString(data, "teacherNumber", "TeacherNumber"),
                            GetString(data, "number", "Number")
                        ));

                        var docTeacherName = NormalizeKey(FirstNonEmpty(
                            GetString(data, "teacherName", "TeacherName"),
                            GetString(data, "teacher", "Teacher")
                        ));

                        var docBranch = NormalizeKey(FirstNonEmpty(
                            GetString(data, "branch", "Branch"),
                            GetString(data, "teacherBranch", "TeacherBranch")
                        ));

                        var lessonName = FirstNonEmpty(
                            GetString(data, "lessonName", "LessonName"),
                            GetString(data, "lesson", "Lesson"),
                            GetString(data, "courseName", "CourseName"),
                            GetString(data, "course", "Course")
                        );

                        var className = NormalizeClassName(FirstNonEmpty(
                            GetString(data, "className", "ClassName"),
                            GetString(data, "class", "Class"),
                            GetString(data, "targetClass", "TargetClass")
                        ));

                        var lessonKey = NormalizeKey($"{lessonName}_{className}");

                        var sameTeacher =
                            (!string.IsNullOrWhiteSpace(teacherNumber) && docTeacherNo == OnlyDigits(teacherNumber)) ||
                            (!string.IsNullOrWhiteSpace(teacherName) && docTeacherName == NormalizeKey(teacherName)) ||
                            (!string.IsNullOrWhiteSpace(teacherBranch) && docBranch == NormalizeKey(teacherBranch)) ||
                            lessonKeys.Contains(lessonKey);

                        if (!sameTeacher)
                        {
                            continue;
                        }

                        var title = FirstNonEmpty(
                            GetString(data, "title", "Title"),
                            GetString(data, "name", "Name"),
                            GetString(data, "homeworkTitle", "HomeworkTitle"),
                            GetString(data, "assignmentTitle", "AssignmentTitle"),
                            "Ödev"
                        );

                        data["Id"] = doc.Id;
                        data["Title"] = title;
                        data["Name"] = title;
                        data["HomeworkTitle"] = title;
                        data["AssignmentTitle"] = title;
                        data["LessonName"] = string.IsNullOrWhiteSpace(lessonName) ? "-" : lessonName;
                        data["ClassName"] = string.IsNullOrWhiteSpace(className) ? "-" : className;

                        result.Add(data);
                    }
                }
                catch
                {
                }
            }

            return result
                .GroupBy(x => NormalizeKey($"{GetText(x, "Title")}_{GetText(x, "LessonName")}_{GetText(x, "ClassName")}"))
                .Select(x => x.First())
                .ToList();
        }

        private async Task<List<Dictionary<string, object>>> LoadTeacherSubmissions(
            string teacherNumber,
            string teacherName,
            string teacherBranch,
            List<Dictionary<string, object>> assignments,
            List<Dictionary<string, object>> lessons
        )
        {
            var result = new List<Dictionary<string, object>>();

            var assignmentIds = assignments
                .Select(x => GetText(x, "Id"))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet();

            var assignmentKeys = assignments
                .Select(x => NormalizeKey($"{GetText(x, "Title")}_{GetText(x, "LessonName")}_{GetText(x, "ClassName")}"))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet();

            var lessonKeys = lessons
                .Select(x => NormalizeKey($"{GetText(x, "Name")}_{GetText(x, "ClassName")}"))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet();

            var collections = new[] { "homework_submissions", "submissions" };

            foreach (var collection in collections)
            {
                try
                {
                    var snapshot = await _firestore.Collection(collection).GetSnapshotAsync();

                    foreach (var doc in snapshot.Documents)
                    {
                        var data = doc.ToDictionary();

                        if (IsDeleted(data))
                        {
                            continue;
                        }

                        var homeworkId = FirstNonEmpty(
                            GetString(data, "homeworkId", "HomeworkId"),
                            GetString(data, "assignmentId", "AssignmentId")
                        );

                        var title = FirstNonEmpty(
                            GetString(data, "assignmentTitle", "AssignmentTitle"),
                            GetString(data, "homeworkTitle", "HomeworkTitle"),
                            GetString(data, "title", "Title"),
                            GetString(data, "name", "Name"),
                            "Ödev"
                        );

                        var lessonName = FirstNonEmpty(
                            GetString(data, "lessonName", "LessonName"),
                            GetString(data, "lesson", "Lesson"),
                            GetString(data, "courseName", "CourseName"),
                            GetString(data, "course", "Course")
                        );

                        var className = NormalizeClassName(FirstNonEmpty(
                            GetString(data, "className", "ClassName"),
                            GetString(data, "class", "Class"),
                            GetString(data, "targetClass", "TargetClass")
                        ));

                        var docTeacherNo = OnlyDigits(FirstNonEmpty(
                            GetString(data, "teacherNo", "TeacherNo"),
                            GetString(data, "teacherNumber", "TeacherNumber"),
                            GetString(data, "number", "Number")
                        ));

                        var docTeacherName = NormalizeKey(FirstNonEmpty(
                            GetString(data, "teacherName", "TeacherName"),
                            GetString(data, "teacher", "Teacher")
                        ));

                        var docBranch = NormalizeKey(FirstNonEmpty(
                            GetString(data, "branch", "Branch"),
                            GetString(data, "teacherBranch", "TeacherBranch")
                        ));

                        var assignmentKey = NormalizeKey($"{title}_{lessonName}_{className}");
                        var lessonKey = NormalizeKey($"{lessonName}_{className}");

                        var sameTeacher =
                            (!string.IsNullOrWhiteSpace(homeworkId) && assignmentIds.Contains(homeworkId)) ||
                            assignmentKeys.Contains(assignmentKey) ||
                            lessonKeys.Contains(lessonKey) ||
                            (!string.IsNullOrWhiteSpace(teacherNumber) && docTeacherNo == OnlyDigits(teacherNumber)) ||
                            (!string.IsNullOrWhiteSpace(teacherName) && docTeacherName == NormalizeKey(teacherName)) ||
                            (!string.IsNullOrWhiteSpace(teacherBranch) && docBranch == NormalizeKey(teacherBranch));

                        if (!sameTeacher)
                        {
                            continue;
                        }

                        var studentNo = OnlyDigits(FirstNonEmpty(
                            GetString(data, "studentNo", "StudentNo"),
                            GetString(data, "studentNumber", "StudentNumber"),
                            GetString(data, "schoolNo", "SchoolNo"),
                            GetString(data, "number", "Number")
                        ));

                        var answer = FirstNonEmpty(
                            GetString(data, "answerText", "AnswerText"),
                            GetString(data, "answer", "Answer"),
                            GetString(data, "content", "Content"),
                            GetString(data, "text", "Text"),
                            "-"
                        );

                        var link = FirstNonEmpty(
                            GetString(data, "answerLink", "AnswerLink"),
                            GetString(data, "fileUrl", "FileUrl"),
                            GetString(data, "submissionFileUrl", "SubmissionFileUrl"),
                            GetString(data, "link", "Link"),
                            GetString(data, "url", "Url")
                        );

                        var score = FirstNonEmpty(
                            GetString(data, "score", "Score"),
                            GetString(data, "grade", "Grade"),
                            GetString(data, "point", "Point"),
                            GetString(data, "not", "Not")
                        );

                        var feedback = FirstNonEmpty(
                            GetString(data, "feedback", "Feedback"),
                            GetString(data, "comment", "Comment"),
                            GetString(data, "geriDonus", "GeriDonus")
                        );

                        var status = FirstNonEmpty(
                            GetString(data, "status", "Status"),
                            !string.IsNullOrWhiteSpace(score) || !string.IsNullOrWhiteSpace(feedback)
                                ? "Değerlendirildi"
                                : "Bekliyor"
                        );

                        data["Id"] = doc.Id;
                        data["HomeworkId"] = homeworkId;
                        data["AssignmentId"] = homeworkId;
                        data["Title"] = title;
                        data["AssignmentTitle"] = title;
                        data["HomeworkTitle"] = title;
                        data["LessonName"] = string.IsNullOrWhiteSpace(lessonName) ? "-" : lessonName;
                        data["ClassName"] = string.IsNullOrWhiteSpace(className) ? "-" : className;
                        data["StudentNo"] = studentNo;
                        data["StudentNumber"] = studentNo;
                        data["SchoolNo"] = studentNo;
                        data["Answer"] = answer;
                        data["AnswerText"] = answer;
                        data["AnswerLink"] = link;
                        data["FileUrl"] = link;
                        data["Score"] = score;
                        data["Grade"] = score;
                        data["Feedback"] = feedback;
                        data["Status"] = status;

                        result.Add(data);
                    }
                }
                catch
                {
                }
            }

            return result
                .GroupBy(x => NormalizeKey($"{GetText(x, "HomeworkId")}_{GetText(x, "StudentNo")}_{GetText(x, "Title")}"))
                .Select(x => x.First())
                .OrderByDescending(x => GetDate(x, "createdAt", "CreatedAt", "submittedAt", "SubmittedAt") ?? DateTime.MinValue)
                .ToList();
        }

        private async Task<Dictionary<string, object>> FindLessonById(string lessonId)
        {
            if (string.IsNullOrWhiteSpace(lessonId))
            {
                return new Dictionary<string, object>();
            }

            try
            {
                var doc = await _firestore.Collection("lessons").Document(lessonId).GetSnapshotAsync();

                if (doc.Exists)
                {
                    var data = doc.ToDictionary();
                    data["Id"] = doc.Id;
                    return data;
                }
            }
            catch
            {
            }

            return new Dictionary<string, object>();
        }

        private async Task<(DocumentSnapshot? Doc, string CollectionName)> FindDocumentInCollections(string id, params string[] collections)
        {
            foreach (var collection in collections)
            {
                try
                {
                    var doc = await _firestore.Collection(collection).Document(id).GetSnapshotAsync();

                    if (doc.Exists)
                    {
                        return (doc, collection);
                    }
                }
                catch
                {
                }
            }

            return (null, "");
        }

        private async Task UpdateDocumentInCollections(string id, Dictionary<string, object> update, params string[] collections)
        {
            foreach (var collection in collections)
            {
                try
                {
                    var docRef = _firestore.Collection(collection).Document(id);
                    var doc = await docRef.GetSnapshotAsync();

                    if (doc.Exists)
                    {
                        await docRef.SetAsync(update, SetOptions.MergeAll);
                    }
                }
                catch
                {
                }
            }
        }

        private static bool IsDeleted(Dictionary<string, object> data)
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

        private static bool IsEvaluated(Dictionary<string, object> data)
        {
            var status = NormalizeKey(GetString(data, "status", "Status"));
            var score = FirstNonEmpty(
                GetString(data, "score", "Score"),
                GetString(data, "grade", "Grade"),
                GetString(data, "point", "Point"),
                GetString(data, "not", "Not")
            );

            var feedback = FirstNonEmpty(
                GetString(data, "feedback", "Feedback"),
                GetString(data, "comment", "Comment"),
                GetString(data, "geriDonus", "GeriDonus")
            );

            return status.Contains("deger") ||
                   status.Contains("evaluated") ||
                   !string.IsNullOrWhiteSpace(score) ||
                   !string.IsNullOrWhiteSpace(feedback);
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

        private static string FormValue(IFormCollection form, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (form.ContainsKey(key))
                {
                    var value = form[key].ToString();

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }

            return "";
        }

        private static string GetText(Dictionary<string, object> data, params string[] keys)
        {
            return GetString(data, keys);
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

            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", "");

            var match = System.Text.RegularExpressions.Regex.Match(text, @"(9|10|11|12)[^\dA-F]*([A-F])");

            if (match.Success)
            {
                return $"{match.Groups[1].Value}-{match.Groups[2].Value}";
            }

            match = System.Text.RegularExpressions.Regex.Match(text, @"([A-F])[^\dA-F]*(9|10|11|12)");

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
    }
}