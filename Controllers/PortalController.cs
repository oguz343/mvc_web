using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using mvc_web.Models;
using mvc_web.Services;
using System.Text.RegularExpressions;

namespace mvc_web.Controllers
{
    public class PortalController : Controller
    {
        private readonly FirestoreDb _firestore;
        private readonly SessionService _session;
        private readonly IMemoryCache _cache;

        public PortalController(
            FirestoreDb firestore,
            SessionService session,
            IMemoryCache cache
        )
        {
            _firestore = firestore;
            _session = session;
            _cache = cache;
        }

        [HttpGet("/Portal")]
        public IActionResult Index()
        {
            if (!_session.IsStudent(HttpContext) && !_session.IsParent(HttpContext))
            {
                return RedirectToAction("Login", "Auth");
            }

            return RedirectToAction(nameof(Overview));
        }

        [HttpGet("/Portal/Overview")]
        public async Task<IActionResult> Overview()
        {
            var model = await BuildPortalViewModel();

            if (model == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            return View(model);
        }

        [HttpGet("/Portal/Homeworks")]
        public async Task<IActionResult> Homeworks()
        {
            var model = await BuildPortalViewModel();

            if (model == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            return View(model);
        }

        [HttpGet("/Portal/Announcements")]
        public async Task<IActionResult> Announcements()
        {
            var model = await BuildPortalViewModel();

            if (model == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            return View(model);
        }

        [HttpGet("/Portal/Grades")]
        public async Task<IActionResult> Grades()
        {
            var model = await BuildPortalViewModel();

            if (model == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            return View(model);
        }

        [HttpGet("/Portal/Calendar")]
        public async Task<IActionResult> Calendar()
        {
            var model = await BuildPortalViewModel();

            if (model == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            return View(model);
        }

        [HttpGet("/Portal/Profile")]
        public async Task<IActionResult> Profile()
        {
            var model = await BuildPortalViewModel();

            if (model == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            return View(model);
        }

        private async Task<PortalViewModel?> BuildPortalViewModel()
        {
            if (!_session.IsStudent(HttpContext) && !_session.IsParent(HttpContext))
            {
                return null;
            }

            var userId = _session.GetUserId(HttpContext) ?? "";
            var role = _session.GetRole(HttpContext) ?? "";
            var number = _session.GetNumber(HttpContext) ?? "";
            var sessionName = _session.GetName(HttpContext) ?? "Kullanıcı";

            var userDoc = await _firestore.Collection("users").Document(userId).GetSnapshotAsync();

            if (!userDoc.Exists)
            {
                _session.Logout(HttpContext);
                return null;
            }

            var userData = userDoc.ToDictionary();

            var model = new PortalViewModel
            {
                UserId = userDoc.Id,
                Name = GetString(userData, new[] { "name", "Name" }, sessionName),
                Role = role,
                Number = number,
                ClassName = NormalizeClassName(GetString(userData, "className", "ClassName", "class", "Class")),
                LinkedStudentNo = GetString(userData, "linkedStudentNo", "LinkedStudentNo"),
                LinkedStudentName = GetString(userData, "linkedStudentName", "LinkedStudentName")
            };

            var studentNumberForHomework = number;
            var studentClassForHomework = model.ClassName;

            if (role == "Veli")
            {
                var linkedNo = OnlyDigits(model.LinkedStudentNo);

                if (!string.IsNullOrWhiteSpace(linkedNo))
                {
                    var studentDoc = await FindUser("Öğrenci", linkedNo);

                    if (studentDoc != null)
                    {
                        var studentData = studentDoc.ToDictionary();

                        studentNumberForHomework = OnlyDigits(
                            GetString(studentData, "schoolNo", "number", "SchoolNo", "Number")
                        );

                        studentClassForHomework = NormalizeClassName(
                            GetString(studentData, "className", "ClassName", "class", "Class")
                        );

                        model.LinkedStudentName = GetString(studentData, new[] { "name", "Name" }, model.LinkedStudentName);
                    }
                }
            }

            var announcementsTask = LoadAnnouncements(role);
            var homeworksTask = LoadHomeworks(studentNumberForHomework, studentClassForHomework);

            await Task.WhenAll(announcementsTask, homeworksTask);

            model.Announcements = announcementsTask.Result;
            model.Homeworks = homeworksTask.Result;

            return model;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitHomework(
            string homeworkId,
            string homeworkCollection,
            string answerText,
            IFormFile? file
        )
        {
            if (!_session.IsStudent(HttpContext))
            {
                TempData["Error"] = "Ödevi sadece öğrenci hesabı teslim edebilir.";
                return RedirectToAction(nameof(Homeworks));
            }

            if (string.IsNullOrWhiteSpace(homeworkId))
            {
                TempData["Error"] = "Ödev bulunamadı.";
                return RedirectToAction(nameof(Homeworks));
            }

            homeworkCollection = NormalizeHomeworkCollection(homeworkCollection);

            if (string.IsNullOrWhiteSpace(homeworkCollection))
            {
                TempData["Error"] = "Geçersiz ödev kaynağı.";
                return RedirectToAction(nameof(Homeworks));
            }

            var userId = _session.GetUserId(HttpContext) ?? "";
            var studentName = _session.GetName(HttpContext) ?? "Öğrenci";
            var studentNumber = _session.GetNumber(HttpContext) ?? "";
            var studentDoc = await _firestore.Collection("users").Document(userId).GetSnapshotAsync();

            if (!studentDoc.Exists)
            {
                _session.Logout(HttpContext);
                return RedirectToAction("Login", "Auth");
            }

            var studentData = studentDoc.ToDictionary();
            var studentClass = NormalizeClassName(
                GetString(studentData, "className", "ClassName", "class", "Class")
            );

            var homeworkDoc = await _firestore
                .Collection(homeworkCollection)
                .Document(homeworkId)
                .GetSnapshotAsync();

            if (!homeworkDoc.Exists)
            {
                TempData["Error"] = "Ödev bulunamadı.";
                return RedirectToAction(nameof(Homeworks));
            }

            var homeworkData = homeworkDoc.ToDictionary();
            var classIds = await FindClassIdsByName(studentClass);

            if (string.IsNullOrWhiteSpace(studentClass) || !HomeworkMatchesClass(homeworkData, studentClass, classIds))
            {
                TempData["Error"] = "Bu ödeve teslim yapma yetkiniz yok.";
                return RedirectToAction(nameof(Homeworks));
            }

            string fileName = "";
            string fileUrl = "";

            if (file != null && file.Length > 0)
            {
                const long maxUploadBytes = 10 * 1024 * 1024;

                if (file.Length > maxUploadBytes)
                {
                    TempData["Error"] = "Dosya boyutu en fazla 10 MB olabilir.";
                    return RedirectToAction(nameof(Homeworks));
                }

                var allowedContentTypes = new Dictionary<string, string[]>
                {
                    [".pdf"] = new[] { "application/pdf" },
                    [".doc"] = new[] { "application/msword", "application/octet-stream" },
                    [".docx"] = new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/octet-stream" },
                    [".jpg"] = new[] { "image/jpeg" },
                    [".jpeg"] = new[] { "image/jpeg" },
                    [".png"] = new[] { "image/png" },
                    [".txt"] = new[] { "text/plain", "application/octet-stream" },
                    [".zip"] = new[] { "application/zip", "application/x-zip-compressed", "application/octet-stream" }
                };

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!allowedContentTypes.ContainsKey(extension))
                {
                    TempData["Error"] = "Dosya türü desteklenmiyor. PDF, Word, görsel, TXT veya ZIP yükleyin.";
                    return RedirectToAction(nameof(Homeworks));
                }

                var contentType = (file.ContentType ?? "").Trim().ToLowerInvariant();

                if (!string.IsNullOrWhiteSpace(contentType) &&
                    !allowedContentTypes[extension].Contains(contentType))
                {
                    TempData["Error"] = "Dosya içeriği seçilen uzantıyla uyumlu değil.";
                    return RedirectToAction(nameof(Homeworks));
                }

                var uploadsRoot = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "uploads",
                    "submissions"
                );

                Directory.CreateDirectory(uploadsRoot);

                fileName = $"{Guid.NewGuid():N}{extension}";
                var fullPath = Path.Combine(uploadsRoot, fileName);

                using var stream = new FileStream(fullPath, FileMode.Create);
                await file.CopyToAsync(stream);

                fileUrl = $"/uploads/submissions/{fileName}";
            }

            if (string.IsNullOrWhiteSpace(answerText) && string.IsNullOrWhiteSpace(fileUrl))
            {
                TempData["Error"] = "Teslim için açıklama yazın veya dosya yükleyin.";
                return RedirectToAction(nameof(Homeworks));
            }

            var studentNo = OnlyDigits(studentNumber);
            var canonicalSubmissionId = BuildSubmissionId(homeworkId, studentNo);
            var legacySubmissionId = $"{homeworkCollection}_{homeworkId}_{studentNumber}";
            var now = Timestamp.GetCurrentTimestamp();
            var title = GetString(homeworkData, new[] { "title", "Title", "name", "Name" }, "Ödev");
            var lessonName = FirstNonEmpty(
                GetString(homeworkData, "lessonName", "LessonName"),
                GetString(homeworkData, "lesson", "Lesson"),
                GetString(homeworkData, "courseName", "CourseName")
            );
            var className = FirstNonEmpty(
                NormalizeClassName(GetString(homeworkData, "className", "ClassName")),
                NormalizeClassName(GetString(homeworkData, "targetClass", "TargetClass"))
            );
            var teacherName = GetString(homeworkData, new[] { "teacherName", "TeacherName", "teacher", "Teacher" }, "");
            var teacherNo = GetString(homeworkData, "teacherNo", "TeacherNo", "teacherNumber", "TeacherNumber");
            var teacherId = GetString(homeworkData, "teacherId", "TeacherId", "teacherUid", "TeacherUid");
            var cleanAnswer = answerText?.Trim() ?? "";

            if (cleanAnswer.Length > 5000)
            {
                TempData["Error"] = "Teslim aÃ§Ä±klamasÄ± en fazla 5000 karakter olabilir.";
                return RedirectToAction(nameof(Homeworks));
            }

            var submissionData = new Dictionary<string, object>
            {
                { "homeworkCollection", homeworkCollection },
                { "homeworkId", homeworkId },
                { "assignmentId", homeworkId },
                { "assignmentTitle", title },
                { "homeworkTitle", title },
                { "title", title },
                { "name", title },
                { "lessonName", lessonName },
                { "lesson", lessonName },
                { "courseName", lessonName },
                { "className", className },
                { "class", className },
                { "targetClass", className },
                { "teacherId", teacherId },
                { "teacherName", teacherName },
                { "teacher", teacherName },
                { "teacherNo", teacherNo },
                { "studentId", userId },
                { "studentName", studentName },
                { "studentNo", studentNo },
                { "studentNumber", studentNo },
                { "schoolNo", studentNo },
                { "answerText", cleanAnswer },
                { "answer", cleanAnswer },
                { "content", cleanAnswer },
                { "text", cleanAnswer },
                { "answerLink", "" },
                { "link", "" },
                { "url", "" },
                { "fileName", fileName },
                { "fileUrl", fileUrl },
                { "status", "Teslim Edildi" },
                { "submittedAt", now },
                { "createdAt", now },
                { "updatedAt", now },
                { "isDeleted", false },
                { "isActive", true }
            };

            await _firestore
                .Collection("submissions")
                .Document(canonicalSubmissionId)
                .SetAsync(submissionData, SetOptions.MergeAll);

            await _firestore
                .Collection("homework_submissions")
                .Document(legacySubmissionId)
                .SetAsync(submissionData, SetOptions.MergeAll);

            TempData["Success"] = "Ödev teslim edildi.";
            return RedirectToAction(nameof(Homeworks));
        }

        private async Task<List<PortalAnnouncementItem>> LoadAnnouncements(string role)
        {
            var cacheKey = $"portal_announcements_{NormalizeKey(role)}";

            if (_cache.TryGetValue(cacheKey, out List<PortalAnnouncementItem>? cachedAnnouncements) &&
                cachedAnnouncements is not null)
            {
                return cachedAnnouncements.ToList();
            }

            var result = new List<PortalAnnouncementItem>();

            var snapshot = await _firestore
                .Collection("announcements")
                .OrderByDescending("createdAt")
                .Limit(30)
                .GetSnapshotAsync();

            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();

                if (!CanSeeAnnouncement(role, data))
                {
                    continue;
                }

                result.Add(new PortalAnnouncementItem
                {
                    Id = doc.Id,
                    Title = GetString(data, new[] { "title", "Title" }, "Duyuru"),
                    Content = GetString(data, "content", "Content", "message", "Message"),
                    Author = GetString(data, new[] { "author", "Author" }, "Admin"),
                    Target = GetString(data, new[] { "target", "Target", "targetRole", "TargetRole" }, "Tüm Okul"),
                    CreatedAt = GetDate(data, "createdAt", "CreatedAt")
                });
            }

            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(1));

            return result;
        }

        private async Task<List<PortalHomeworkItem>> LoadHomeworks(string studentNumber, string className)
        {
            var result = new List<PortalHomeworkItem>();

            className = NormalizeClassName(className);

            if (string.IsNullOrWhiteSpace(className))
            {
                return result;
            }

            var classIdsTask = FindClassIdsByName(className);
            var submissionDocsTask = LoadStudentSubmissionDocs(studentNumber);
            var collections = new[] { "homeworks", "assignments" };
            var addedKeys = new HashSet<string>();
            var classIds = await classIdsTask;
            var submissionDocs = await submissionDocsTask;

            var homeworkGroups = await Task.WhenAll(collections.Select(async collectionName => new
            {
                CollectionName = collectionName,
                Docs = await LoadHomeworkDocsForClass(collectionName, className, classIds)
            }));

            foreach (var group in homeworkGroups)
            {
                foreach (var doc in group.Docs)
                {
                    var data = doc.ToDictionary();

                    if (!HomeworkMatchesClass(data, className, classIds))
                    {
                        continue;
                    }

                    var title = GetString(data, new[] { "title", "Title" }, "Ödev");
                    var lessonName = FirstNonEmpty(
                        GetString(data, "lessonName", "LessonName"),
                        GetString(data, "lesson", "Lesson"),
                        GetString(data, "courseName", "CourseName")
                    );
                    var normalizedItemClass = FirstNonEmpty(
                        NormalizeClassName(GetString(data, "className", "ClassName")),
                        NormalizeClassName(GetString(data, "targetClass", "TargetClass")),
                        className
                    );
                    var key = NormalizeKey($"{title}_{lessonName}_{normalizedItemClass}");

                    if (addedKeys.Contains(key))
                    {
                        continue;
                    }

                    addedKeys.Add(key);

                    var submissionDoc = FindLoadedHomeworkSubmission(doc.Id, studentNumber, submissionDocs);

                    var item = new PortalHomeworkItem
                    {
                        Id = doc.Id,
                        CollectionName = group.CollectionName,
                        Title = title,
                        Description = GetString(data, "description", "Description", "content", "Content", "body", "Body"),
                        LessonName = lessonName,
                        ClassName = normalizedItemClass,
                        TeacherName = GetString(data, new[] { "teacherName", "TeacherName", "teacher", "Teacher" }, "Öğretmen"),
                        DueDate = GetDate(data, "dueDate", "DueDate", "deadline", "Deadline", "endDate", "EndDate"),
                        AttachmentFileName = GetString(data, "attachmentFileName", "AttachmentFileName", "materialFileName", "MaterialFileName", "fileName", "FileName"),
                        AttachmentFileUrl = FirstNonEmpty(
                            GetString(data, "attachmentFileUrl", "AttachmentFileUrl"),
                            GetString(data, "materialFileUrl", "MaterialFileUrl"),
                            GetString(data, "documentUrl", "DocumentUrl"),
                            GetString(data, "fileUrl", "FileUrl"),
                            GetString(data, "link", "Link"),
                            GetString(data, "url", "Url")
                        ),
                        IsSubmitted = submissionDoc != null && submissionDoc.Exists
                    };

                    if (submissionDoc != null && submissionDoc.Exists)
                    {
                        var sub = submissionDoc.ToDictionary();

                        item.SubmissionText = GetString(sub, "answerText", "AnswerText");
                        item.SubmissionFileName = GetString(sub, "fileName", "FileName");
                        item.SubmissionFileUrl = GetString(sub, "fileUrl", "FileUrl");
                        item.SubmittedAt = GetDate(sub, "submittedAt", "SubmittedAt");

                        item.Grade = GetString(sub, "grade", "Grade", "score", "Score");
                        item.Feedback = GetString(sub, "feedback", "Feedback", "comment", "Comment");
                        item.EvaluatedAt = GetDate(sub, "evaluatedAt", "EvaluatedAt");
                    }

                    result.Add(item);
                }
            }

            return result;
        }

        private async Task<List<DocumentSnapshot>> LoadHomeworkDocsForClass(
            string collectionName,
            string className,
            HashSet<string> classIds
        )
        {
            var result = new List<DocumentSnapshot>();
            var seen = new HashSet<string>();
            var collection = _firestore.Collection(collectionName);

            async Task<QuerySnapshot?> ReadQuery(Query query)
            {
                try
                {
                    return await query.GetSnapshotAsync();
                }
                catch
                {
                    return null;
                }
            }

            void AddSnapshot(QuerySnapshot? snapshot)
            {
                if (snapshot == null)
                {
                    return;
                }

                foreach (var doc in snapshot.Documents)
                {
                    if (seen.Add(doc.Id))
                    {
                        result.Add(doc);
                    }
                }
            }

            var queries = new List<Query>();

            foreach (var field in new[] { "className", "ClassName", "targetClass", "TargetClass", "class", "Class" })
            {
                queries.Add(collection.WhereEqualTo(field, className));
            }

            foreach (var chunk in ChunkValues(classIds.Where(x => !string.IsNullOrWhiteSpace(x)).ToList(), 10))
            {
                foreach (var field in new[] { "classId", "ClassId", "targetClassId", "TargetClassId" })
                {
                    queries.Add(collection.WhereIn(field, chunk.Cast<object>().ToArray()));
                }
            }

            foreach (var snapshot in await Task.WhenAll(queries.Select(ReadQuery)))
            {
                AddSnapshot(snapshot);
            }

            if (result.Count == 0)
            {
                AddSnapshot(await ReadQuery(collection.OrderByDescending("createdAt").Limit(250)));
            }

            return result
                .OrderByDescending(x => GetDate(x.ToDictionary(), "createdAt", "CreatedAt") ?? DateTime.MinValue)
                .ToList();
        }

        private async Task<List<DocumentSnapshot>> LoadStudentSubmissionDocs(string studentNumber)
        {
            var result = new List<DocumentSnapshot>();
            var seen = new HashSet<string>();
            var studentNo = OnlyDigits(studentNumber);

            if (string.IsNullOrWhiteSpace(studentNo))
            {
                return result;
            }

            async Task<(string CollectionName, QuerySnapshot? Snapshot)> ReadQuery(
                string collectionName,
                Query query
            )
            {
                try
                {
                    return (collectionName, await query.GetSnapshotAsync());
                }
                catch
                {
                    return (collectionName, null);
                }
            }

            var queries = new List<Task<(string CollectionName, QuerySnapshot? Snapshot)>>();

            foreach (var collectionName in new[] { "submissions", "homework_submissions" })
            {
                var collection = _firestore.Collection(collectionName);

                foreach (var field in new[] { "studentNo", "StudentNo", "studentNumber", "StudentNumber", "schoolNo", "SchoolNo", "number", "Number" })
                {
                    queries.Add(ReadQuery(collectionName, collection.WhereEqualTo(field, studentNo)));
                }
            }

            foreach (var (collectionName, snapshot) in await Task.WhenAll(queries))
            {
                if (snapshot == null)
                {
                    continue;
                }

                foreach (var doc in snapshot.Documents)
                {
                    if (seen.Add($"{collectionName}_{doc.Id}"))
                    {
                        result.Add(doc);
                    }
                }
            }

            return result;
        }

        private DocumentSnapshot? FindLoadedHomeworkSubmission(
            string homeworkId,
            string studentNumber,
            List<DocumentSnapshot> submissionDocs
        )
        {
            var studentNo = OnlyDigits(studentNumber);
            var canonicalSubmissionId = BuildSubmissionId(homeworkId, studentNo);
            var directKey = NormalizeKey($"{homeworkId}_{studentNo}");

            foreach (var doc in submissionDocs)
            {
                if (doc.Id == canonicalSubmissionId ||
                    doc.Id == $"homeworks_{homeworkId}_{studentNo}" ||
                    doc.Id == $"assignments_{homeworkId}_{studentNo}")
                {
                    return doc;
                }

                var data = doc.ToDictionary();
                var docHomeworkId = FirstNonEmpty(
                    GetString(data, "assignmentId", "AssignmentId"),
                    GetString(data, "homeworkId", "HomeworkId")
                );
                var docStudentNo = OnlyDigits(FirstNonEmpty(
                    GetString(data, "studentNo", "StudentNo"),
                    GetString(data, "studentNumber", "StudentNumber"),
                    GetString(data, "schoolNo", "SchoolNo"),
                    GetString(data, "number", "Number")
                ));

                if (NormalizeKey($"{docHomeworkId}_{docStudentNo}") == directKey)
                {
                    return doc;
                }
            }

            return null;
        }

        private async Task<DocumentSnapshot> FindHomeworkSubmission(string homeworkId, string studentNumber)
        {
            var canonicalSubmissionId = BuildSubmissionId(homeworkId, OnlyDigits(studentNumber));
            var canonicalDoc = await _firestore
                .Collection("submissions")
                .Document(canonicalSubmissionId)
                .GetSnapshotAsync();

            if (canonicalDoc.Exists)
            {
                return canonicalDoc;
            }

            foreach (var collectionName in new[] { "homeworks", "assignments" })
            {
                var submissionId = $"{collectionName}_{homeworkId}_{studentNumber}";
                var submissionDoc = await _firestore
                    .Collection("homework_submissions")
                    .Document(submissionId)
                    .GetSnapshotAsync();

                if (submissionDoc.Exists)
                {
                    return submissionDoc;
                }
            }

            var studentNo = OnlyDigits(studentNumber);
            var directKey = NormalizeKey($"{homeworkId}_{studentNo}");

            foreach (var submissionCollection in new[] { "submissions", "homework_submissions" })
            {
                var doc = await FindSubmissionByStudentQuery(
                    submissionCollection,
                    homeworkId,
                    studentNo,
                    directKey
                );

                if (doc != null)
                {
                    return doc;
                }
            }

            return await _firestore
                .Collection("homework_submissions")
                .Document("missing")
                .GetSnapshotAsync();
        }

        private async Task<DocumentSnapshot?> FindSubmissionByStudentQuery(
            string submissionCollection,
            string homeworkId,
            string studentNo,
            string directKey
        )
        {
            if (string.IsNullOrWhiteSpace(studentNo))
            {
                return null;
            }

            foreach (var studentField in new[] { "studentNo", "StudentNo", "studentNumber", "StudentNumber", "schoolNo", "SchoolNo" })
            {
                QuerySnapshot snapshot;

                try
                {
                    snapshot = await _firestore
                        .Collection(submissionCollection)
                        .WhereEqualTo(studentField, studentNo)
                        .Limit(20)
                        .GetSnapshotAsync();
                }
                catch
                {
                    continue;
                }

                foreach (var doc in snapshot.Documents)
                {
                    var data = doc.ToDictionary();
                    var docHomeworkId = FirstNonEmpty(
                        GetString(data, "assignmentId", "AssignmentId"),
                        GetString(data, "homeworkId", "HomeworkId")
                    );
                    var docStudentNo = OnlyDigits(FirstNonEmpty(
                        GetString(data, "studentNo", "StudentNo"),
                        GetString(data, "studentNumber", "StudentNumber"),
                        GetString(data, "schoolNo", "SchoolNo")
                    ));

                    if (NormalizeKey($"{docHomeworkId}_{docStudentNo}") == directKey)
                    {
                        return doc;
                    }
                }
            }

            return null;
        }

        private static string BuildSubmissionId(string assignmentId, string studentNo)
        {
            var assignmentKey = NormalizeKey(assignmentId);
            var studentKey = OnlyDigits(studentNo);

            if (!string.IsNullOrWhiteSpace(assignmentKey) && !string.IsNullOrWhiteSpace(studentKey))
            {
                return $"{assignmentKey}_{studentKey}";
            }

            return NormalizeKey($"{assignmentId}_{studentNo}");
        }

        private static string NormalizeHomeworkCollection(string collection)
        {
            collection = (collection ?? "").Trim().ToLowerInvariant();

            return collection switch
            {
                "" => "homeworks",
                "homeworks" => "homeworks",
                "assignments" => "assignments",
                _ => ""
            };
        }

        private async Task<HashSet<string>> FindClassIdsByName(string className)
        {
            var result = new HashSet<string>();
            var normalizedTarget = NormalizeClassName(className);
            var cacheKey = $"portal_class_ids_{NormalizeKey(normalizedTarget)}";

            if (_cache.TryGetValue(cacheKey, out HashSet<string>? cachedClassIds) &&
                cachedClassIds is not null)
            {
                return new HashSet<string>(cachedClassIds);
            }

            try
            {
                var snapshot = await _firestore.Collection("classes").GetSnapshotAsync();

                foreach (var doc in snapshot.Documents)
                {
                    var data = doc.ToDictionary();

                    var name = NormalizeClassName(
                        GetString(data, "name", "Name", "className", "ClassName")
                    );

                    var grade = GetString(data, "grade", "Grade", "level", "Level");
                    var branch = GetString(data, "branch", "Branch", "section", "Section");

                    var generatedName = NormalizeClassName($"{grade}-{branch}");

                    if (name == normalizedTarget || generatedName == normalizedTarget)
                    {
                        result.Add(doc.Id);
                    }
                }
            }
            catch
            {
            }

            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));

            return result;
        }

        private bool HomeworkMatchesClass(
            Dictionary<string, object> data,
            string className,
            HashSet<string> classIds
        )
        {
            var normalizedClass = NormalizeClassName(className);

            var directClassValues = new[]
            {
                GetString(data, "className", "ClassName"),
                GetString(data, "targetClass", "TargetClass"),
                GetString(data, "selectedClass", "SelectedClass"),
                GetString(data, "class", "Class"),
                GetString(data, "classText", "ClassText"),
                GetString(data, "targetClassName", "TargetClassName")
            };

            foreach (var value in directClassValues)
            {
                if (ClassTextMatches(value, normalizedClass))
                {
                    return true;
                }
            }

            var grade = GetString(data, "grade", "Grade", "level", "Level");
            var branch = GetString(data, "branch", "Branch", "section", "Section");

            if (!string.IsNullOrWhiteSpace(grade) && !string.IsNullOrWhiteSpace(branch))
            {
                if (NormalizeClassName($"{grade}-{branch}") == normalizedClass)
                {
                    return true;
                }
            }

            var directClassId = GetString(data, "classId", "ClassId", "targetClassId", "TargetClassId");

            if (!string.IsNullOrWhiteSpace(directClassId) && classIds.Contains(directClassId))
            {
                return true;
            }

            foreach (var key in new[]
            {
                "classes",
                "Classes",
                "targetClasses",
                "TargetClasses",
                "selectedClasses",
                "SelectedClasses",
                "classNames",
                "ClassNames"
            })
            {
                if (data.TryGetValue(key, out var value))
                {
                    if (ListContainsClass(value, normalizedClass))
                    {
                        return true;
                    }
                }
            }

            foreach (var key in new[]
            {
                "classIds",
                "ClassIds",
                "targetClassIds",
                "TargetClassIds",
                "selectedClassIds",
                "SelectedClassIds"
            })
            {
                if (data.TryGetValue(key, out var value))
                {
                    if (ListContainsClassId(value, classIds))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ListContainsClass(object value, string normalizedClass)
        {
            if (value is IEnumerable<object> objectList)
            {
                foreach (var item in objectList)
                {
                    if (ClassTextMatches(item?.ToString() ?? "", normalizedClass))
                    {
                        return true;
                    }
                }
            }

            if (value is IEnumerable<string> stringList)
            {
                foreach (var item in stringList)
                {
                    if (ClassTextMatches(item, normalizedClass))
                    {
                        return true;
                    }
                }
            }

            var text = value?.ToString() ?? "";

            if (text.Contains(","))
            {
                foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (ClassTextMatches(part, normalizedClass))
                    {
                        return true;
                    }
                }
            }

            return ClassTextMatches(text, normalizedClass);
        }

        private static bool ListContainsClassId(object value, HashSet<string> classIds)
        {
            if (!classIds.Any())
            {
                return false;
            }

            if (value is IEnumerable<object> objectList)
            {
                foreach (var item in objectList)
                {
                    var id = item?.ToString() ?? "";

                    if (classIds.Contains(id))
                    {
                        return true;
                    }
                }
            }

            if (value is IEnumerable<string> stringList)
            {
                foreach (var id in stringList)
                {
                    if (classIds.Contains(id))
                    {
                        return true;
                    }
                }
            }

            var text = value?.ToString() ?? "";

            if (classIds.Contains(text))
            {
                return true;
            }

            if (text.Contains(","))
            {
                foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (classIds.Contains(part.Trim()))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ClassTextMatches(string value, string normalizedClass)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (NormalizeClassName(value) == normalizedClass)
            {
                return true;
            }

            if (value.Contains(","))
            {
                foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (NormalizeClassName(part) == normalizedClass)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool CanSeeAnnouncement(string role, Dictionary<string, object> data)
        {
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddTarget(targets, GetString(data, "target", "Target"));
            AddTarget(targets, GetString(data, "targetRole", "TargetRole"));
            AddTarget(targets, GetString(data, "audience", "Audience"));

            foreach (var key in new[]
            {
                "targets", "Targets", "roles", "Roles", "selectedRoles", "SelectedRoles"
            })
            {
                if (data.TryGetValue(key, out var value) && value is IEnumerable<object> list)
                {
                    foreach (var item in list)
                    {
                        AddTarget(targets, item?.ToString() ?? "");
                    }
                }
            }

            if (!targets.Any())
            {
                return true;
            }

            if (targets.Contains("Tüm Okul") ||
                targets.Contains("Tüm Kullanıcılar") ||
                targets.Contains("Herkes"))
            {
                return true;
            }

            if (role == "Öğrenci" &&
                (targets.Contains("Öğrenci") || targets.Contains("Öğrenciler")))
            {
                return true;
            }

            if (role == "Veli" &&
                (targets.Contains("Veli") || targets.Contains("Veliler")))
            {
                return true;
            }

            return false;
        }

        private static void AddTarget(HashSet<string> targets, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            targets.Add(value.Trim());
        }

        private async Task<DocumentSnapshot?> FindUser(string role, string number)
        {
            foreach (var numberField in new[] { "schoolNo", "SchoolNo", "number", "Number", "studentNo", "StudentNo" })
            {
                try
                {
                    var targetedSnapshot = await _firestore
                        .Collection("users")
                        .WhereEqualTo(numberField, number)
                        .Limit(10)
                        .GetSnapshotAsync();

                    foreach (var doc in targetedSnapshot.Documents)
                    {
                        var data = doc.ToDictionary();
                        var currentRole = NormalizeKey(GetString(data, "role", "Role", "userRole", "UserRole"));
                        var currentNo = OnlyDigits(GetString(data, "schoolNo", "SchoolNo", "number", "Number", "studentNo", "StudentNo"));

                        if (currentRole == NormalizeKey(role) && currentNo == number)
                        {
                            return doc;
                        }
                    }
                }
                catch
                {
                }
            }

            var snapshot = await _firestore
                .Collection("users")
                .WhereEqualTo("role", role)
                .GetSnapshotAsync();

            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();

                var schoolNo = OnlyDigits(
                    GetString(data, "schoolNo", "SchoolNo", "number", "Number")
                );

                if (schoolNo == number)
                {
                    return doc;
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

        private static string GetString(Dictionary<string, object> data, params string[] keys)
        {
            return GetString(data, keys, "");
        }

        private static string GetString(
            Dictionary<string, object> data,
            string[] keys,
            string defaultValue
        )
        {
            foreach (var key in keys)
            {
                if (data.TryGetValue(key, out var value) && value != null)
                {
                    return value.ToString() ?? defaultValue;
                }
            }

            return defaultValue;
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

        private static IEnumerable<List<string>> ChunkValues(List<string> values, int size)
        {
            for (var i = 0; i < values.Count; i += size)
            {
                yield return values
                    .Skip(i)
                    .Take(size)
                    .ToList();
            }
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
                .Replace("ç", "c")
                .Replace("Ä±", "i")
                .Replace("ÄŸ", "g")
                .Replace("Ã¼", "u")
                .Replace("ÅŸ", "s")
                .Replace("Ã¶", "o")
                .Replace("Ã§", "c");

            return new string(value.Where(char.IsLetterOrDigit).ToArray());
        }
    }
}
