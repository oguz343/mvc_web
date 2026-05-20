using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using mvc_web.Models;
using mvc_web.Services;
using System.Text.RegularExpressions;

namespace mvc_web.Controllers;

public class TeacherController : Controller
{
    private readonly FirestoreDb _firestore;
    private readonly DataIntegrityService _integrity;

    public TeacherController(FirestoreDb firestore)
    {
        _firestore = firestore;
        _integrity = new DataIntegrityService(firestore);
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var teacherNumber = GetSessionValue(
            "UserNumber",
            "Number",
            "SchoolNo",
            "TeacherNo",
            "LoginNumber",
            "CurrentUserNumber"
        );

        var teacherName = GetSessionValue(
            "UserName",
            "Name",
            "TeacherName",
            "CurrentUserName"
        );

        var teacherProfile = await LoadTeacherProfile(teacherNumber, teacherName);

        if (teacherProfile.Count == 0)
        {
            return RedirectToAction("Login", "Auth");
        }

        var teacherNo = OnlyDigits(GetText(teacherProfile, "Number", "number", "TeacherNo", "teacherNo"));
        var realTeacherName = GetText(teacherProfile, "Name", "name", "TeacherName", "teacherName");
        var teacherBranch = GetText(teacherProfile, "Branch", "branch", "TeacherBranch", "teacherBranch");
        var teacherId = GetText(teacherProfile, "Id", "id", "TeacherId", "teacherId");

        var lessons = await LoadTeacherLessons(teacherNo, realTeacherName, teacherBranch, teacherId);
        var assignments = await LoadTeacherAssignments(teacherNo, realTeacherName, teacherBranch, teacherId, lessons);
        var submissions = await LoadTeacherSubmissions(teacherNo, realTeacherName, teacherBranch, teacherId, lessons, assignments);
        var announcements = await LoadTeacherAnnouncements();

        ViewBag.TeacherName = realTeacherName;
        ViewBag.TeacherNo = teacherNo;
        ViewBag.TeacherBranch = teacherBranch;
        ViewBag.Teacher = teacherProfile;

        ViewBag.Lessons = lessons;
        ViewBag.Assignments = assignments;
        ViewBag.Submissions = submissions;
        ViewBag.Announcements = announcements;

        ViewBag.LessonCount = lessons.Count;
        ViewBag.AssignmentCount = assignments.Count;
        ViewBag.SubmissionCount = submissions.Count;
        ViewBag.EvaluatedCount = submissions.Count(x => IsEvaluatedStatus(GetText(x, "Status", "status")));

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Assignments()
    {
        var teacherNumber = GetSessionValue(
            "UserNumber",
            "Number",
            "SchoolNo",
            "TeacherNo",
            "LoginNumber",
            "CurrentUserNumber"
        );

        var teacherName = GetSessionValue(
            "UserName",
            "Name",
            "TeacherName",
            "CurrentUserName"
        );

        var teacherProfile = await LoadTeacherProfile(teacherNumber, teacherName);

        if (teacherProfile.Count == 0)
        {
            return RedirectToAction("Login", "Auth");
        }

        var teacherNo = OnlyDigits(GetText(teacherProfile, "Number", "number", "TeacherNo", "teacherNo"));
        var realTeacherName = GetText(teacherProfile, "Name", "name", "TeacherName", "teacherName");
        var teacherBranch = GetText(teacherProfile, "Branch", "branch", "TeacherBranch", "teacherBranch");
        var teacherId = GetText(teacherProfile, "Id", "id", "TeacherId", "teacherId");

        var lessons = await LoadTeacherLessons(teacherNo, realTeacherName, teacherBranch, teacherId);
        var assignments = await LoadTeacherAssignments(teacherNo, realTeacherName, teacherBranch, teacherId, lessons);

        ViewBag.TeacherName = realTeacherName;
        ViewBag.TeacherNo = teacherNo;
        ViewBag.TeacherBranch = teacherBranch;
        ViewBag.Teacher = teacherProfile;

        ViewBag.Lessons = lessons;
        ViewBag.Assignments = assignments;

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> CreateAssignment()
    {
        var teacherNumber = GetSessionValue(
            "UserNumber",
            "Number",
            "SchoolNo",
            "TeacherNo",
            "LoginNumber",
            "CurrentUserNumber"
        );

        var teacherName = GetSessionValue(
            "UserName",
            "Name",
            "TeacherName",
            "CurrentUserName"
        );

        var teacherProfile = await LoadTeacherProfile(teacherNumber, teacherName);

        if (teacherProfile.Count == 0)
        {
            return RedirectToAction("Login", "Auth");
        }

        var teacherNo = OnlyDigits(GetText(teacherProfile, "Number", "number", "TeacherNo", "teacherNo"));
        var realTeacherName = GetText(teacherProfile, "Name", "name", "TeacherName", "teacherName");
        var teacherBranch = GetText(teacherProfile, "Branch", "branch", "TeacherBranch", "teacherBranch");
        var teacherId = GetText(teacherProfile, "Id", "id", "TeacherId", "teacherId");

        var lessons = await LoadTeacherLessons(teacherNo, realTeacherName, teacherBranch, teacherId);

        ViewBag.TeacherName = realTeacherName;
        ViewBag.TeacherNo = teacherNo;
        ViewBag.TeacherBranch = teacherBranch;
        ViewBag.Teacher = teacherProfile;
        ViewBag.Lessons = lessons;
        ViewBag.TeacherLessons = lessons;
        ViewBag.MyLessons = lessons;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAssignment(IFormCollection form)
    {
        var teacherNumber = GetSessionValue(
            "UserNumber",
            "Number",
            "SchoolNo",
            "TeacherNo",
            "LoginNumber",
            "CurrentUserNumber"
        );

        var teacherName = GetSessionValue(
            "UserName",
            "Name",
            "TeacherName",
            "CurrentUserName"
        );

        var teacherProfile = await LoadTeacherProfile(teacherNumber, teacherName);

        if (teacherProfile.Count == 0)
        {
            return RedirectToAction("Login", "Auth");
        }

        var teacherNo = OnlyDigits(GetText(teacherProfile, "Number", "number", "TeacherNo", "teacherNo"));
        var realTeacherName = GetText(teacherProfile, "Name", "name", "TeacherName", "teacherName");
        var teacherBranch = GetText(teacherProfile, "Branch", "branch", "TeacherBranch", "teacherBranch");
        var teacherId = GetText(teacherProfile, "Id", "id", "TeacherId", "teacherId");

        var lessons = await LoadTeacherLessons(teacherNo, realTeacherName, teacherBranch, teacherId);

        var selectedLessonIds = form["lessonIds"]
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x?.Trim() ?? "")
            .Distinct()
            .ToList();

        var legacyLessonId = (form["lessonId"].ToString() ?? "").Trim();

        if (!string.IsNullOrWhiteSpace(legacyLessonId) &&
            !selectedLessonIds.Contains(legacyLessonId))
        {
            selectedLessonIds.Add(legacyLessonId);
        }
        var title = (form["title"].ToString() ?? "").Trim();
        var description = (form["description"].ToString() ?? "").Trim();
        var fileType = (form["fileType"].ToString() ?? "").Trim();
        var dueDateRaw = (form["dueDate"].ToString() ?? "").Trim();

        if (!selectedLessonIds.Any())
        {
            TempData["Error"] = "Ders seçmelisiniz.";
            return RedirectToAction(nameof(CreateAssignment));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            TempData["Error"] = "Ödev başlığı boş bırakılamaz.";
            return RedirectToAction(nameof(CreateAssignment));
        }

        var selectedLessons = lessons
            .Where(x => selectedLessonIds.Contains(GetText(x, "Id", "id")))
            .ToList();

        if (selectedLessons.Count != selectedLessonIds.Count)
        {
            TempData["Error"] = "Seçilen ders aktif değil, silinmiş veya size atanmamış.";
            return RedirectToAction(nameof(CreateAssignment));
        }

        if (string.IsNullOrWhiteSpace(fileType))
        {
            fileType = "Metin / Link";
        }

        DateTime? dueDate = null;

        if (!string.IsNullOrWhiteSpace(dueDateRaw) && DateTime.TryParse(dueDateRaw, out var parsedDueDate))
        {
            dueDate = parsedDueDate;
        }

        var now = Timestamp.FromDateTime(DateTime.UtcNow);
        var createdCount = 0;

        foreach (var selectedLesson in selectedLessons)
        {
            var lessonId = GetText(selectedLesson, "Id", "id");
            var lessonName = GetText(selectedLesson, "Name", "name", "LessonName", "lessonName");
            var className = NormalizeClassName(GetText(selectedLesson, "ClassName", "className", "Class", "class"));
            var branch = GetText(selectedLesson, "Branch", "branch", "TeacherBranch", "teacherBranch");

        var data = new Dictionary<string, object?>
        {
            ["title"] = title,
            ["Title"] = title,
            ["name"] = title,
            ["Name"] = title,
            ["homeworkTitle"] = title,
            ["assignmentTitle"] = title,

            ["description"] = description,
            ["Description"] = description,
            ["content"] = description,
            ["Content"] = description,

            ["lessonId"] = lessonId,
            ["LessonId"] = lessonId,
            ["courseId"] = lessonId,
            ["CourseId"] = lessonId,

            ["lessonName"] = lessonName,
            ["LessonName"] = lessonName,
            ["lesson"] = lessonName,
            ["Lesson"] = lessonName,
            ["courseName"] = lessonName,
            ["CourseName"] = lessonName,

            ["className"] = className,
            ["ClassName"] = className,
            ["class"] = className,
            ["Class"] = className,
            ["targetClass"] = className,
            ["TargetClass"] = className,

            ["teacherId"] = teacherId,
            ["TeacherId"] = teacherId,
            ["teacherName"] = realTeacherName,
            ["TeacherName"] = realTeacherName,
            ["teacher"] = realTeacherName,
            ["Teacher"] = realTeacherName,
            ["teacherNo"] = teacherNo,
            ["TeacherNo"] = teacherNo,
            ["teacherNumber"] = teacherNo,
            ["TeacherNumber"] = teacherNo,

            ["branch"] = string.IsNullOrWhiteSpace(branch) ? teacherBranch : branch,
            ["Branch"] = string.IsNullOrWhiteSpace(branch) ? teacherBranch : branch,
            ["teacherBranch"] = string.IsNullOrWhiteSpace(branch) ? teacherBranch : branch,
            ["TeacherBranch"] = string.IsNullOrWhiteSpace(branch) ? teacherBranch : branch,

            ["fileType"] = fileType,
            ["FileType"] = fileType,
            ["type"] = fileType,
            ["Type"] = fileType,

            ["status"] = "Aktif",
            ["Status"] = "Aktif",
            ["isDeleted"] = false,
            ["IsDeleted"] = false,
            ["isActive"] = true,
            ["IsActive"] = true,

            ["createdAt"] = now,
            ["CreatedAt"] = now,
            ["updatedAt"] = now,
            ["UpdatedAt"] = now,
        };

        if (dueDate.HasValue)
        {
            var dueTimestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(dueDate.Value, DateTimeKind.Utc));

            data["dueDate"] = dueTimestamp;
            data["DueDate"] = dueTimestamp;
            data["deadline"] = dueTimestamp;
            data["Deadline"] = dueTimestamp;
            data["endDate"] = dueTimestamp;
            data["EndDate"] = dueTimestamp;
        }

        var homeworkRef = await _firestore.Collection("homeworks").AddAsync(data);

        data["id"] = homeworkRef.Id;
        data["Id"] = homeworkRef.Id;

        await _firestore.Collection("assignments").Document(homeworkRef.Id).SetAsync(data, SetOptions.MergeAll);
            createdCount++;
        }

        TempData["Success"] = "Ödev başarıyla oluşturuldu.";
        if (createdCount > 1)
        {
            TempData["Success"] = $"{createdCount} sınıf/ders ataması için ödev başarıyla oluşturuldu.";
        }

        return RedirectToAction(nameof(Assignments));
    }

    [HttpGet]
    public async Task<IActionResult> EditAssignment(string id)
    {
        id = (id ?? "").Trim();

        if (string.IsNullOrWhiteSpace(id))
        {
            TempData["Error"] = "Ödev bulunamadı.";
            return RedirectToAction(nameof(Assignments));
        }

        var teacherNumber = GetSessionValue(
            "UserNumber",
            "Number",
            "SchoolNo",
            "TeacherNo",
            "LoginNumber",
            "CurrentUserNumber"
        );

        var teacherName = GetSessionValue(
            "UserName",
            "Name",
            "TeacherName",
            "CurrentUserName"
        );

        var teacherProfile = await LoadTeacherProfile(teacherNumber, teacherName);

        if (teacherProfile.Count == 0)
        {
            return RedirectToAction("Login", "Auth");
        }

        var teacherNo = OnlyDigits(GetText(teacherProfile, "Number", "number", "TeacherNo", "teacherNo"));
        var realTeacherName = GetText(teacherProfile, "Name", "name", "TeacherName", "teacherName");
        var teacherBranch = GetText(teacherProfile, "Branch", "branch", "TeacherBranch", "teacherBranch");
        var teacherId = GetText(teacherProfile, "Id", "id", "TeacherId", "teacherId");

        var lessons = await LoadTeacherLessons(teacherNo, realTeacherName, teacherBranch, teacherId);
        var assignments = await LoadTeacherAssignments(teacherNo, realTeacherName, teacherBranch, teacherId, lessons);

        var assignment = assignments.FirstOrDefault(x =>
            GetText(x, "Id", "id") == id
        );

        if (assignment == null)
        {
            TempData["Error"] = "Bu ödev aktif değil, silinmiş veya size ait değil.";
            return RedirectToAction(nameof(Assignments));
        }

        ViewBag.TeacherName = realTeacherName;
        ViewBag.TeacherNo = teacherNo;
        ViewBag.TeacherBranch = teacherBranch;
        ViewBag.Teacher = teacherProfile;
        ViewBag.Lessons = lessons;
        ViewBag.TeacherLessons = BuildLessonOptions(lessons);
        ViewBag.Assignment = assignment;

        return View(BuildAssignmentViewModel(assignment));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAssignment(IFormCollection form)
    {
        var id = FirstNonEmpty(form["id"].ToString(), form["Id"].ToString());
        var lessonId = FirstNonEmpty(form["lessonId"].ToString(), form["LessonId"].ToString());
        var title = (form["title"].ToString() ?? "").Trim();
        var description = (form["description"].ToString() ?? "").Trim();
        var fileType = FirstNonEmpty(form["fileType"].ToString(), form["Type"].ToString());
        var dueDateRaw = FirstNonEmpty(form["dueDate"].ToString(), form["DueDate"].ToString());
        var status = FirstNonEmpty(form["status"].ToString(), form["Status"].ToString(), "Aktif");
        Dictionary<string, object>? assignment = null;
        List<Dictionary<string, object>> lessons;
        string teacherNo;
        string realTeacherName;
        string teacherBranch;
        string teacherId;

        if (string.IsNullOrWhiteSpace(id))
        {
            TempData["Error"] = "Ödev bulunamadı.";
            return RedirectToAction(nameof(Assignments));
        }

        var teacherProfile = await LoadCurrentTeacherProfile();

        if (teacherProfile.Count == 0)
        {
            return RedirectToAction("Login", "Auth");
        }

        teacherNo = OnlyDigits(GetText(teacherProfile, "Number", "number", "TeacherNo", "teacherNo"));
        realTeacherName = GetText(teacherProfile, "Name", "name", "TeacherName", "teacherName");
        teacherBranch = GetText(teacherProfile, "Branch", "branch", "TeacherBranch", "teacherBranch");
        teacherId = GetText(teacherProfile, "Id", "id", "TeacherId", "teacherId");
        lessons = await LoadTeacherLessons(teacherNo, realTeacherName, teacherBranch, teacherId);
        assignment = await LoadOwnedAssignment(id, teacherNo, realTeacherName, teacherId, lessons);

        if (assignment == null)
        {
            TempData["Error"] = "Bu ödev aktif değil, silinmiş veya size ait değil.";
            return RedirectToAction(nameof(Assignments));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            TempData["Error"] = "Ödev başlığı boş bırakılamaz.";
            return RedirectToAction(nameof(EditAssignment), new { id });
        }

        var update = new Dictionary<string, object?>
        {
            ["title"] = title,
            ["Title"] = title,
            ["name"] = title,
            ["Name"] = title,
            ["homeworkTitle"] = title,
            ["assignmentTitle"] = title,

            ["description"] = description,
            ["Description"] = description,
            ["content"] = description,
            ["Content"] = description,

            ["fileType"] = string.IsNullOrWhiteSpace(fileType) ? "Metin / Link" : fileType,
            ["FileType"] = string.IsNullOrWhiteSpace(fileType) ? "Metin / Link" : fileType,
            ["type"] = string.IsNullOrWhiteSpace(fileType) ? "Metin / Link" : fileType,
            ["Type"] = string.IsNullOrWhiteSpace(fileType) ? "Metin / Link" : fileType,

            ["status"] = status,
            ["Status"] = status,

            ["updatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
            ["UpdatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
        };

        if (!string.IsNullOrWhiteSpace(lessonId))
        {
            var selectedLesson = lessons.FirstOrDefault(x => GetText(x, "Id", "id") == lessonId);

            if (selectedLesson == null)
            {
                TempData["Error"] = "Seçilen ders aktif değil, silinmiş veya size atanmamış.";
                return RedirectToAction(nameof(EditAssignment), new { id });
            }

            if (selectedLesson.Count > 0)
            {
                var lessonName = FirstNonEmpty(GetText(selectedLesson, "name", "Name"), GetText(selectedLesson, "lessonName", "LessonName"), "Ders");
                var className = NormalizeClassName(FirstNonEmpty(GetText(selectedLesson, "className", "ClassName"), GetText(selectedLesson, "class", "Class")));

                update["lessonId"] = lessonId;
                update["LessonId"] = lessonId;
                update["courseId"] = lessonId;
                update["CourseId"] = lessonId;
                update["lessonName"] = lessonName;
                update["LessonName"] = lessonName;
                update["lesson"] = lessonName;
                update["Lesson"] = lessonName;
                update["courseName"] = lessonName;
                update["CourseName"] = lessonName;
                update["className"] = className;
                update["ClassName"] = className;
                update["class"] = className;
                update["Class"] = className;
                update["targetClass"] = className;
                update["TargetClass"] = className;
            }
        }

        if (!string.IsNullOrWhiteSpace(dueDateRaw) && DateTime.TryParse(dueDateRaw, out var parsedDueDate))
        {
            var dueTimestamp = Timestamp.FromDateTime(DateTime.SpecifyKind(parsedDueDate, DateTimeKind.Utc));

            update["dueDate"] = dueTimestamp;
            update["DueDate"] = dueTimestamp;
            update["deadline"] = dueTimestamp;
            update["Deadline"] = dueTimestamp;
            update["endDate"] = dueTimestamp;
            update["EndDate"] = dueTimestamp;
        }

        await UpdateAssignmentEverywhere(id, update);

        TempData["Success"] = "Ödev güncellendi.";
        return RedirectToAction(nameof(Assignments));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAssignment(string id)
    {
        id = (id ?? "").Trim();

        if (string.IsNullOrWhiteSpace(id))
        {
            TempData["Error"] = "Ödev bulunamadı.";
            return RedirectToAction(nameof(Assignments));
        }

        var teacherProfile = await LoadCurrentTeacherProfile();

        if (teacherProfile.Count == 0)
        {
            return RedirectToAction("Login", "Auth");
        }

        var teacherNo = OnlyDigits(GetText(teacherProfile, "Number", "number", "TeacherNo", "teacherNo"));
        var realTeacherName = GetText(teacherProfile, "Name", "name", "TeacherName", "teacherName");
        var teacherBranch = GetText(teacherProfile, "Branch", "branch", "TeacherBranch", "teacherBranch");
        var teacherId = GetText(teacherProfile, "Id", "id", "TeacherId", "teacherId");
        var lessons = await LoadTeacherLessons(teacherNo, realTeacherName, teacherBranch, teacherId);
        var assignment = await LoadOwnedAssignment(id, teacherNo, realTeacherName, teacherId, lessons);

        if (assignment == null)
        {
            TempData["Error"] = "Bu ödev aktif değil, silinmiş veya size ait değil.";
            return RedirectToAction(nameof(Assignments));
        }

        var update = new Dictionary<string, object?>
        {
            ["isDeleted"] = true,
            ["IsDeleted"] = true,
            ["isActive"] = false,
            ["IsActive"] = false,
            ["status"] = "Silindi",
            ["Status"] = "Silindi",
            ["deletedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
            ["DeletedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
            ["updatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
            ["UpdatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
        };

        await UpdateAssignmentEverywhere(id, update);

        TempData["Success"] = "Ödev silindi.";
        return RedirectToAction(nameof(Assignments));
    }

    [HttpGet]
    public async Task<IActionResult> Submissions()
    {
        var teacherNumber = GetSessionValue(
            "UserNumber",
            "Number",
            "SchoolNo",
            "TeacherNo",
            "LoginNumber",
            "CurrentUserNumber"
        );

        var teacherName = GetSessionValue(
            "UserName",
            "Name",
            "TeacherName",
            "CurrentUserName"
        );

        var teacherProfile = await LoadTeacherProfile(teacherNumber, teacherName);

        if (teacherProfile.Count == 0)
        {
            return RedirectToAction("Login", "Auth");
        }

        var teacherNo = OnlyDigits(GetText(teacherProfile, "Number", "number", "TeacherNo", "teacherNo"));
        var realTeacherName = GetText(teacherProfile, "Name", "name", "TeacherName", "teacherName");
        var teacherBranch = GetText(teacherProfile, "Branch", "branch", "TeacherBranch", "teacherBranch");
        var teacherId = GetText(teacherProfile, "Id", "id", "TeacherId", "teacherId");

        var lessons = await LoadTeacherLessons(teacherNo, realTeacherName, teacherBranch, teacherId);
        var assignments = await LoadTeacherAssignments(teacherNo, realTeacherName, teacherBranch, teacherId, lessons);
        var submissions = await LoadTeacherSubmissions(teacherNo, realTeacherName, teacherBranch, teacherId, lessons, assignments);

        ViewBag.TeacherName = realTeacherName;
        ViewBag.TeacherNo = teacherNo;
        ViewBag.TeacherBranch = teacherBranch;
        ViewBag.Teacher = teacherProfile;

        ViewBag.Lessons = lessons;
        ViewBag.Assignments = assignments;
        ViewBag.Submissions = submissions;

        return View(BuildSubmissionViewModels(submissions));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EvaluateSubmission(string submissionId, string score, string feedback)
    {
        submissionId = (submissionId ?? "").Trim();
        score = (score ?? "").Trim();
        feedback = (feedback ?? "").Trim();

        if (string.IsNullOrWhiteSpace(submissionId))
        {
            TempData["Error"] = "Teslim bulunamadı.";
            return RedirectToAction(nameof(Submissions));
        }

        var teacherProfile = await LoadCurrentTeacherProfile();

        if (teacherProfile.Count == 0)
        {
            return RedirectToAction("Login", "Auth");
        }

        var teacherNo = OnlyDigits(GetText(teacherProfile, "Number", "number", "TeacherNo", "teacherNo"));
        var realTeacherName = GetText(teacherProfile, "Name", "name", "TeacherName", "teacherName");
        var teacherBranch = GetText(teacherProfile, "Branch", "branch", "TeacherBranch", "teacherBranch");
        var teacherId = GetText(teacherProfile, "Id", "id", "TeacherId", "teacherId");
        var lessons = await LoadTeacherLessons(teacherNo, realTeacherName, teacherBranch, teacherId);
        if (!await SubmissionBelongsToTeacher(submissionId, teacherNo, realTeacherName, teacherId, lessons))
        {
            TempData["Error"] = "Bu teslim size ait bir ödeve bağlı değil.";
            return RedirectToAction(nameof(Submissions));
        }

        if (string.IsNullOrWhiteSpace(score) && string.IsNullOrWhiteSpace(feedback))
        {
            TempData["Error"] = "Not veya geri dönüş alanlarından en az biri doldurulmalı.";
            return RedirectToAction(nameof(Submissions));
        }

        var update = new Dictionary<string, object?>
        {
            ["score"] = score,
            ["Score"] = score,
            ["grade"] = score,
            ["Grade"] = score,
            ["point"] = score,
            ["Point"] = score,
            ["not"] = score,
            ["Not"] = score,

            ["feedback"] = feedback,
            ["Feedback"] = feedback,
            ["comment"] = feedback,
            ["Comment"] = feedback,
            ["geriDonus"] = feedback,
            ["GeriDonus"] = feedback,

            ["status"] = "Değerlendirildi",
            ["Status"] = "Değerlendirildi",

            ["evaluatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
            ["EvaluatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
            ["updatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
            ["UpdatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
        };

        await UpdateSubmissionEverywhere(submissionId, update);

        TempData["Success"] = "Teslim değerlendirildi.";
        return RedirectToAction(nameof(Submissions));
    }

    [HttpGet]
    public async Task<IActionResult> Announcements()
    {
        var teacherNumber = GetSessionValue(
            "UserNumber",
            "Number",
            "SchoolNo",
            "TeacherNo",
            "LoginNumber",
            "CurrentUserNumber"
        );

        var teacherName = GetSessionValue(
            "UserName",
            "Name",
            "TeacherName",
            "CurrentUserName"
        );

        var teacherProfile = await LoadTeacherProfile(teacherNumber, teacherName);

        if (teacherProfile.Count == 0)
        {
            return RedirectToAction("Login", "Auth");
        }

        var teacherNo = OnlyDigits(GetText(teacherProfile, "Number", "number", "TeacherNo", "teacherNo"));
        var realTeacherName = GetText(teacherProfile, "Name", "name", "TeacherName", "teacherName");
        var teacherBranch = GetText(teacherProfile, "Branch", "branch", "TeacherBranch");

        var announcements = await LoadTeacherAnnouncements();

        ViewBag.TeacherName = realTeacherName;
        ViewBag.TeacherNo = teacherNo;
        ViewBag.TeacherBranch = teacherBranch;
        ViewBag.Teacher = teacherProfile;
        ViewBag.Announcements = announcements;

        return View();
    }

    private async Task<Dictionary<string, object>> LoadCurrentTeacherProfile()
    {
        var teacherNumber = GetSessionValue(
            "UserNumber",
            "Number",
            "SchoolNo",
            "TeacherNo",
            "LoginNumber",
            "CurrentUserNumber"
        );

        var teacherName = GetSessionValue(
            "UserName",
            "Name",
            "TeacherName",
            "CurrentUserName"
        );

        return await LoadTeacherProfile(teacherNumber, teacherName);
    }

    private async Task<Dictionary<string, object>?> LoadOwnedAssignment(
        string assignmentId,
        string teacherNumber,
        string teacherName,
        string teacherId,
        List<Dictionary<string, object>> lessons
    )
    {
        foreach (var collection in new[] { "homeworks", "assignments" })
        {
            var doc = await _firestore.Collection(collection).Document(assignmentId).GetSnapshotAsync();

            if (!doc.Exists)
            {
                continue;
            }

            var data = doc.ToDictionary();
            data["Id"] = doc.Id;
            data["id"] = doc.Id;

            if (AssignmentBelongsToTeacher(data, teacherNumber, teacherName, teacherId, lessons))
            {
                return data;
            }
        }

        return null;
    }

    private async Task<bool> SubmissionBelongsToTeacher(
        string submissionId,
        string teacherNumber,
        string teacherName,
        string teacherId,
        List<Dictionary<string, object>> lessons
    )
    {
        foreach (var collection in new[] { "submissions", "homework_submissions" })
        {
            var doc = await _firestore.Collection(collection).Document(submissionId).GetSnapshotAsync();

            if (!doc.Exists)
            {
                continue;
            }

            var data = doc.ToDictionary();
            var assignmentId = FirstNonEmpty(
                GetText(data, "assignmentId", "AssignmentId"),
                GetText(data, "homeworkId", "HomeworkId")
            );

            if (string.IsNullOrWhiteSpace(assignmentId))
            {
                return false;
            }

            var assignment = await LoadOwnedAssignment(assignmentId, teacherNumber, teacherName, teacherId, lessons);

            return assignment != null;
        }

        return false;
    }

    private static bool AssignmentBelongsToTeacher(
        Dictionary<string, object> data,
        string teacherNumber,
        string teacherName,
        string teacherId,
        List<Dictionary<string, object>> lessons
    )
    {
        if (IsDeleted(data))
        {
            return false;
        }

        var lessonId = FirstNonEmpty(
            GetText(data, "lessonId", "LessonId"),
            GetText(data, "courseId", "CourseId")
        );

        var lessonName = FirstNonEmpty(
            GetText(data, "lessonName", "LessonName"),
            GetText(data, "lesson", "Lesson"),
            GetText(data, "courseName", "CourseName")
        );

        var className = NormalizeClassName(FirstNonEmpty(
            GetText(data, "className", "ClassName"),
            GetText(data, "class", "Class"),
            GetText(data, "targetClass", "TargetClass")
        ));

        var lessonKeys = lessons
            .Select(x => NormalizeKey($"{GetText(x, "Name", "name", "LessonName", "lessonName")}_{NormalizeClassName(GetText(x, "ClassName", "className", "Class", "class"))}"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet();

        var lessonIds = lessons
            .Select(x => GetText(x, "Id", "id"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet();

        var sameLesson =
            (!string.IsNullOrWhiteSpace(lessonId) && lessonIds.Contains(lessonId)) ||
            lessonKeys.Contains(NormalizeKey($"{lessonName}_{className}"));

        var docTeacherId = NormalizeKey(FirstNonEmpty(
            GetText(data, "teacherId", "TeacherId"),
            GetText(data, "teacherUid", "TeacherUid"),
            GetText(data, "userId", "UserId"),
            GetText(data, "createdById", "CreatedById"),
            GetText(data, "ownerId", "OwnerId")
        ));

        var docTeacherNo = OnlyDigits(FirstNonEmpty(
            GetText(data, "teacherNo", "TeacherNo"),
            GetText(data, "teacherNumber", "TeacherNumber"),
            GetText(data, "number", "Number"),
            GetText(data, "createdByNumber", "CreatedByNumber")
        ));

        var docTeacherName = NormalizeKey(FirstNonEmpty(
            GetText(data, "teacherName", "TeacherName"),
            GetText(data, "teacher", "Teacher"),
            GetText(data, "createdByName", "CreatedByName")
        ));

        var hasTeacherIdentity =
            !string.IsNullOrWhiteSpace(docTeacherId) ||
            !string.IsNullOrWhiteSpace(docTeacherNo) ||
            !string.IsNullOrWhiteSpace(docTeacherName);

        if (!hasTeacherIdentity)
        {
            return false;
        }

        var sameTeacher =
            (!string.IsNullOrWhiteSpace(teacherId) && docTeacherId == NormalizeKey(teacherId)) ||
            (!string.IsNullOrWhiteSpace(teacherNumber) && docTeacherNo == OnlyDigits(teacherNumber)) ||
            (!string.IsNullOrWhiteSpace(teacherName) && docTeacherName == NormalizeKey(teacherName));

        return sameTeacher && sameLesson;
    }

    private async Task<Dictionary<string, object>> LoadTeacherProfile(string teacherNumber, string teacherName)
    {
        var result = new Dictionary<string, object>();

        var numberKey = OnlyDigits(teacherNumber);
        var nameKey = NormalizeKey(teacherName);

        var snapshot = await _firestore.Collection("users").GetSnapshotAsync();

        foreach (var doc in snapshot.Documents)
        {
            var data = doc.ToDictionary();

            if (IsDeleted(data))
            {
                continue;
            }

            var role = NormalizeKey(GetText(data, "role", "Role", "userRole", "UserRole"));

            if (role != "ogretmen")
            {
                continue;
            }

            var currentNumber = OnlyDigits(FirstNonEmpty(
                GetText(data, "number", "Number"),
                GetText(data, "schoolNo", "SchoolNo"),
                GetText(data, "teacherNo", "TeacherNo"),
                GetText(data, "teacherNumber", "TeacherNumber")
            ));

            var currentName = FirstNonEmpty(
                GetText(data, "name", "Name"),
                GetText(data, "fullName", "FullName"),
                GetText(data, "userName", "UserName"),
                GetText(data, "teacherName", "TeacherName"),
                "Öğretmen"
            );

            var currentNameKey = NormalizeKey(currentName);

            var matchedByNumber =
                !string.IsNullOrWhiteSpace(numberKey) &&
                currentNumber == numberKey;

            var matchedByName =
                !string.IsNullOrWhiteSpace(nameKey) &&
                currentNameKey == nameKey;

            if (!matchedByNumber && !matchedByName)
            {
                continue;
            }

            data["Id"] = doc.Id;
            data["id"] = doc.Id;
            data["Name"] = currentName;
            data["name"] = currentName;
            data["TeacherName"] = currentName;
            data["teacherName"] = currentName;
            data["Number"] = currentNumber;
            data["number"] = currentNumber;
            data["TeacherNo"] = currentNumber;
            data["teacherNo"] = currentNumber;
            data["Branch"] = FirstNonEmpty(GetText(data, "branch", "Branch"), GetText(data, "teacherBranch", "TeacherBranch"));
            data["branch"] = data["Branch"];

            result = data;
            break;
        }

        return result;
    }

    private async Task<List<Dictionary<string, object>>> LoadTeacherLessons(
        string teacherNumber,
        string teacherName,
        string teacherBranch,
        string teacherId = ""
    )
    {
        var result = new List<Dictionary<string, object>>();
        var seen = new HashSet<string>();

        var activeTeachers = await _integrity.LoadActiveTeachersAsync();
        var activeClasses = await _integrity.LoadActiveClassesAsync();

        var activeTeacherIds = activeTeachers
            .Select(x => DataIntegrityService.NormalizeKey(x.Id))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet();

        var activeTeacherNos = activeTeachers
            .Select(x => DataIntegrityService.OnlyDigits(x.Number))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet();

        var activeTeacherNames = activeTeachers
            .Select(x => DataIntegrityService.NormalizeKey(x.Name))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet();

        var activeClassNames = activeClasses
            .Select(x => DataIntegrityService.NormalizeClassName(x.Name))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet();

        var teacherNumberKey = OnlyDigits(teacherNumber);
        var teacherNameKey = NormalizeKey(teacherName);
        var teacherBranchKey = NormalizeKey(teacherBranch);
        var teacherIdKey = NormalizeKey(teacherId);

        var snapshot = await _firestore.Collection("lessons").GetSnapshotAsync();

        foreach (var doc in snapshot.Documents)
        {
            var data = doc.ToDictionary();

            if (IsDeleted(data))
            {
                continue;
            }

            var lessonName = FirstNonEmpty(
                GetText(data, "name", "Name"),
                GetText(data, "lessonName", "LessonName"),
                GetText(data, "title", "Title"),
                GetText(data, "courseName", "CourseName"),
                GetText(data, "lesson", "Lesson"),
                "Ders"
            );

            var className = NormalizeClassName(FirstNonEmpty(
                GetText(data, "className", "ClassName"),
                GetText(data, "class", "Class"),
                GetText(data, "targetClass", "TargetClass"),
                GetText(data, "schoolClass", "SchoolClass")
            ));

            var docTeacherId = NormalizeKey(FirstNonEmpty(
                GetText(data, "teacherId", "TeacherId"),
                GetText(data, "teacherUid", "TeacherUid"),
                GetText(data, "userId", "UserId"),
                GetText(data, "teacherDocId", "TeacherDocId")
            ));

            var docTeacherNo = OnlyDigits(FirstNonEmpty(
                GetText(data, "teacherNo", "TeacherNo"),
                GetText(data, "teacherNumber", "TeacherNumber"),
                GetText(data, "teacherSchoolNo", "TeacherSchoolNo"),
                GetText(data, "number", "Number"),
                GetText(data, "schoolNo", "SchoolNo")
            ));

            var docTeacherNameRaw = FirstNonEmpty(
                GetText(data, "teacherName", "TeacherName"),
                GetText(data, "teacherFullName", "TeacherFullName"),
                GetText(data, "teacher", "Teacher")
            );

            var docTeacherName = NormalizeKey(docTeacherNameRaw);

            var docBranchRaw = FirstNonEmpty(
                GetText(data, "branch", "Branch"),
                GetText(data, "teacherBranch", "TeacherBranch"),
                GetText(data, "lessonBranch", "LessonBranch"),
                GetText(data, "subject", "Subject")
            );

            var docBranch = NormalizeKey(docBranchRaw);

            var lessonTeacherStillActive =
                (!string.IsNullOrWhiteSpace(docTeacherId) && activeTeacherIds.Contains(docTeacherId)) ||
                (!string.IsNullOrWhiteSpace(docTeacherNo) && activeTeacherNos.Contains(docTeacherNo)) ||
                (!string.IsNullOrWhiteSpace(docTeacherName) && activeTeacherNames.Contains(docTeacherName));

            var lessonClassStillActive =
                !string.IsNullOrWhiteSpace(className) &&
                activeClassNames.Contains(className);

            if (!lessonTeacherStillActive || !lessonClassStillActive)
            {
                continue;
            }

            var hasTeacherIdentity =
                !string.IsNullOrWhiteSpace(docTeacherId) ||
                !string.IsNullOrWhiteSpace(docTeacherNo) ||
                !string.IsNullOrWhiteSpace(docTeacherName);

            var sameTeacher =
                (!string.IsNullOrWhiteSpace(teacherIdKey) && docTeacherId == teacherIdKey) ||
                (!string.IsNullOrWhiteSpace(teacherNumberKey) && docTeacherNo == teacherNumberKey) ||
                (!string.IsNullOrWhiteSpace(teacherNameKey) && docTeacherName == teacherNameKey) ||
                (!hasTeacherIdentity && !string.IsNullOrWhiteSpace(teacherBranchKey) && docBranch == teacherBranchKey);

            if (!sameTeacher)
            {
                continue;
            }

            var key = NormalizeKey($"{lessonName}_{className}_{docTeacherNo}_{docTeacherNameRaw}");

            if (seen.Contains(key))
            {
                continue;
            }

            seen.Add(key);

            data["Id"] = doc.Id;
            data["id"] = doc.Id;
            data["Name"] = lessonName;
            data["name"] = lessonName;
            data["LessonName"] = lessonName;
            data["lessonName"] = lessonName;
            data["ClassName"] = className;
            data["className"] = className;
            data["Class"] = className;
            data["class"] = className;
            data["TeacherId"] = docTeacherId;
            data["teacherId"] = docTeacherId;
            data["TeacherName"] = string.IsNullOrWhiteSpace(docTeacherNameRaw) ? teacherName : docTeacherNameRaw;
            data["teacherName"] = data["TeacherName"];
            data["TeacherNo"] = string.IsNullOrWhiteSpace(docTeacherNo) ? teacherNumberKey : docTeacherNo;
            data["teacherNo"] = data["TeacherNo"];
            data["TeacherNumber"] = data["TeacherNo"];
            data["teacherNumber"] = data["TeacherNo"];
            data["Branch"] = string.IsNullOrWhiteSpace(docBranchRaw) ? teacherBranch : docBranchRaw;
            data["branch"] = data["Branch"];
            data["TeacherBranch"] = data["Branch"];
            data["teacherBranch"] = data["Branch"];

            result.Add(data);
        }

        return result
            .OrderBy(x => GetText(x, "ClassName", "className"))
            .ThenBy(x => GetText(x, "Name", "name"))
            .ToList();
    }

    private async Task<List<Dictionary<string, object>>> LoadTeacherAssignments(
        string teacherNumber,
        string teacherName,
        string teacherBranch,
        string teacherId,
        List<Dictionary<string, object>> lessons
    )
    {
        var result = new List<Dictionary<string, object>>();
        var seen = new HashSet<string>();

        var lessonIds = lessons
            .Select(x => GetText(x, "Id", "id"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet();

        var lessonKeys = lessons
            .Select(x => NormalizeKey($"{GetText(x, "Name", "name", "LessonName", "lessonName")}_{NormalizeClassName(GetText(x, "ClassName", "className", "Class", "class"))}"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet();

        var teacherNumberKey = OnlyDigits(teacherNumber);
        var teacherNameKey = NormalizeKey(teacherName);
        var teacherIdKey = NormalizeKey(teacherId);

        var collections = new[] { "homeworks", "assignments" };

        foreach (var collection in collections)
        {
            var snapshot = await _firestore.Collection(collection).GetSnapshotAsync();

            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();

                if (IsDeleted(data))
                {
                    continue;
                }

                var lessonId = FirstNonEmpty(
                    GetText(data, "lessonId", "LessonId"),
                    GetText(data, "courseId", "CourseId")
                );

                var title = FirstNonEmpty(
                    GetText(data, "title", "Title"),
                    GetText(data, "name", "Name"),
                    GetText(data, "homeworkTitle"),
                    GetText(data, "assignmentTitle"),
                    "Ödev"
                );

                var lessonName = FirstNonEmpty(
                    GetText(data, "lessonName", "LessonName"),
                    GetText(data, "lesson", "Lesson"),
                    GetText(data, "courseName", "CourseName")
                );

                var className = NormalizeClassName(FirstNonEmpty(
                    GetText(data, "className", "ClassName"),
                    GetText(data, "class", "Class"),
                    GetText(data, "targetClass", "TargetClass")
                ));

                var docTeacherId = NormalizeKey(FirstNonEmpty(
                    GetText(data, "teacherId", "TeacherId"),
                    GetText(data, "teacherUid", "TeacherUid"),
                    GetText(data, "userId", "UserId")
                ));

                var docTeacherNo = OnlyDigits(FirstNonEmpty(
                    GetText(data, "teacherNo", "TeacherNo"),
                    GetText(data, "teacherNumber", "TeacherNumber"),
                    GetText(data, "number", "Number")
                ));

                var docTeacherName = NormalizeKey(FirstNonEmpty(
                    GetText(data, "teacherName", "TeacherName"),
                    GetText(data, "teacher", "Teacher")
                ));

                var assignmentLessonKey = NormalizeKey($"{lessonName}_{className}");

                var sameLesson =
                    (!string.IsNullOrWhiteSpace(lessonId) && lessonIds.Contains(lessonId)) ||
                    lessonKeys.Contains(assignmentLessonKey);

                var sameTeacher =
                    (!string.IsNullOrWhiteSpace(teacherIdKey) && docTeacherId == teacherIdKey) ||
                    (!string.IsNullOrWhiteSpace(teacherNumberKey) && docTeacherNo == teacherNumberKey) ||
                    (!string.IsNullOrWhiteSpace(teacherNameKey) && docTeacherName == teacherNameKey);

                if (!AssignmentBelongsToTeacher(data, teacherNumber, teacherName, teacherId, lessons))
                {
                    continue;
                }

                if (!sameLesson && !sameTeacher)
                {
                    continue;
                }

                if (!lessonKeys.Contains(assignmentLessonKey) && !lessonIds.Contains(lessonId))
                {
                    continue;
                }

                var key = NormalizeKey($"{title}_{lessonName}_{className}");

                if (seen.Contains(key))
                {
                    continue;
                }

                seen.Add(key);

                data["Id"] = doc.Id;
                data["id"] = doc.Id;
                data["Title"] = title;
                data["title"] = title;
                data["Name"] = title;
                data["name"] = title;
                data["Description"] = FirstNonEmpty(GetText(data, "description", "Description"), GetText(data, "content", "Content"));
                data["description"] = data["Description"];
                data["LessonId"] = lessonId;
                data["lessonId"] = lessonId;
                data["LessonName"] = lessonName;
                data["lessonName"] = lessonName;
                data["ClassName"] = className;
                data["className"] = className;
                data["TeacherName"] = teacherName;
                data["teacherName"] = teacherName;
                data["TeacherNo"] = teacherNumber;
                data["teacherNo"] = teacherNumber;
                data["Branch"] = teacherBranch;
                data["branch"] = teacherBranch;
                data["FileType"] = FirstNonEmpty(GetText(data, "fileType", "FileType"), GetText(data, "type", "Type"), "Metin / Link");
                data["fileType"] = data["FileType"];

                result.Add(data);
            }
        }

        return result
            .OrderByDescending(x => GetDate(x, "createdAt", "CreatedAt") ?? DateTime.MinValue)
            .ToList();
    }

    private async Task<List<Dictionary<string, object>>> LoadTeacherSubmissions(
        string teacherNumber,
        string teacherName,
        string teacherBranch,
        string teacherId,
        List<Dictionary<string, object>> lessons,
        List<Dictionary<string, object>> assignments
    )
    {
        var result = new List<Dictionary<string, object>>();
        var seen = new HashSet<string>();

        var lessonKeys = lessons
            .Select(x => NormalizeKey($"{GetText(x, "Name", "name", "LessonName", "lessonName")}_{NormalizeClassName(GetText(x, "ClassName", "className", "Class", "class"))}"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet();

        var assignmentIds = assignments
            .Select(x => GetText(x, "Id", "id"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet();

        var assignmentKeys = assignments
            .Select(x => NormalizeKey($"{GetText(x, "Title", "title", "Name", "name")}_{GetText(x, "LessonName", "lessonName")}_{NormalizeClassName(GetText(x, "ClassName", "className"))}"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet();

        var collections = new[] { "submissions", "homework_submissions" };

        foreach (var collection in collections)
        {
            var docs = await LoadTeacherSubmissionDocs(
                collection,
                teacherId,
                teacherNumber,
                assignmentIds
            );

            foreach (var doc in docs)
            {
                var data = doc.ToDictionary();

                if (IsDeleted(data))
                {
                    continue;
                }

                var assignmentId = FirstNonEmpty(
                    GetText(data, "assignmentId", "AssignmentId"),
                    GetText(data, "homeworkId", "HomeworkId")
                );

                var title = FirstNonEmpty(
                    GetText(data, "title", "Title"),
                    GetText(data, "assignmentTitle"),
                    GetText(data, "homeworkTitle"),
                    "Ödev"
                );

                var lessonName = FirstNonEmpty(
                    GetText(data, "lessonName", "LessonName"),
                    GetText(data, "lesson", "Lesson"),
                    GetText(data, "courseName", "CourseName")
                );

                var className = NormalizeClassName(FirstNonEmpty(
                    GetText(data, "className", "ClassName"),
                    GetText(data, "class", "Class"),
                    GetText(data, "targetClass", "TargetClass")
                ));

                var studentNo = OnlyDigits(FirstNonEmpty(
                    GetText(data, "studentNo", "StudentNo"),
                    GetText(data, "studentNumber", "StudentNumber"),
                    GetText(data, "number", "Number")
                ));

                var submissionLessonKey = NormalizeKey($"{lessonName}_{className}");
                var submissionAssignmentKey = NormalizeKey($"{title}_{lessonName}_{className}");

                var sameAssignment =
                    (!string.IsNullOrWhiteSpace(assignmentId) && assignmentIds.Contains(assignmentId)) ||
                    assignmentKeys.Contains(submissionAssignmentKey);

                var sameLesson = lessonKeys.Contains(submissionLessonKey);

                if (!sameAssignment && !sameLesson)
                {
                    continue;
                }

                var key = NormalizeKey($"{assignmentId}_{studentNo}_{title}_{lessonName}_{className}");

                var item = new Dictionary<string, object>(data)
                {
                    ["Id"] = doc.Id,
                    ["id"] = doc.Id,
                    ["AssignmentId"] = assignmentId,
                    ["assignmentId"] = assignmentId,
                    ["Title"] = title,
                    ["title"] = title,
                    ["LessonName"] = lessonName,
                    ["lessonName"] = lessonName,
                    ["ClassName"] = className,
                    ["className"] = className,
                    ["StudentName"] = FirstNonEmpty(GetText(data, "studentName", "StudentName"), GetText(data, "name", "Name"), "-"),
                    ["studentName"] = FirstNonEmpty(GetText(data, "studentName", "StudentName"), GetText(data, "name", "Name"), "-"),
                    ["StudentNo"] = studentNo,
                    ["studentNo"] = studentNo,
                    ["Answer"] = FirstNonEmpty(GetText(data, "answer", "Answer"), GetText(data, "content", "Content"), GetText(data, "text", "Text"), "-"),
                    ["answer"] = FirstNonEmpty(GetText(data, "answer", "Answer"), GetText(data, "content", "Content"), GetText(data, "text", "Text"), "-"),
                    ["Link"] = FirstNonEmpty(GetText(data, "link", "Link"), GetText(data, "fileUrl", "FileUrl"), GetText(data, "url", "Url")),
                    ["link"] = FirstNonEmpty(GetText(data, "link", "Link"), GetText(data, "fileUrl", "FileUrl"), GetText(data, "url", "Url")),
                    ["Score"] = FirstNonEmpty(GetText(data, "score", "Score"), GetText(data, "grade", "Grade"), GetText(data, "point", "Point"), GetText(data, "not", "Not")),
                    ["score"] = FirstNonEmpty(GetText(data, "score", "Score"), GetText(data, "grade", "Grade"), GetText(data, "point", "Point"), GetText(data, "not", "Not")),
                    ["Feedback"] = FirstNonEmpty(GetText(data, "feedback", "Feedback"), GetText(data, "comment", "Comment"), GetText(data, "geriDonus", "GeriDonus")),
                    ["feedback"] = FirstNonEmpty(GetText(data, "feedback", "Feedback"), GetText(data, "comment", "Comment"), GetText(data, "geriDonus", "GeriDonus")),
                    ["Status"] = FirstNonEmpty(GetText(data, "status", "Status"), "Bekliyor"),
                    ["status"] = FirstNonEmpty(GetText(data, "status", "Status"), "Bekliyor"),
                };

                if (seen.Contains(key))
                {
                    var index = result.FindIndex(x =>
                        NormalizeKey($"{GetText(x, "AssignmentId", "assignmentId")}_{GetText(x, "StudentNo", "studentNo")}_{GetText(x, "Title", "title")}_{GetText(x, "LessonName", "lessonName")}_{GetText(x, "ClassName", "className")}") == key
                    );

                    if (index >= 0 && IsEvaluatedStatus(GetText(item, "Status", "status")))
                    {
                        result[index] = item;
                    }

                    continue;
                }

                seen.Add(key);
                result.Add(item);
            }
        }

        return result
            .OrderByDescending(x => GetDate(x, "submittedAt", "SubmittedAt", "createdAt", "CreatedAt") ?? DateTime.MinValue)
            .ToList();
    }

    private async Task<List<DocumentSnapshot>> LoadTeacherSubmissionDocs(
        string collection,
        string teacherId,
        string teacherNumber,
        HashSet<string> assignmentIds
    )
    {
        var result = new List<DocumentSnapshot>();
        var seen = new HashSet<string>();
        var teacherNo = OnlyDigits(teacherNumber);
        var collectionRef = _firestore.Collection(collection);

        async Task AddQuery(Query query)
        {
            try
            {
                var snapshot = await query.GetSnapshotAsync();

                foreach (var doc in snapshot.Documents)
                {
                    if (seen.Add(doc.Id))
                    {
                        result.Add(doc);
                    }
                }
            }
            catch
            {
            }
        }

        if (!string.IsNullOrWhiteSpace(teacherId))
        {
            await AddQuery(collectionRef.WhereEqualTo("teacherId", teacherId));
            await AddQuery(collectionRef.WhereEqualTo("TeacherId", teacherId));
        }

        if (!string.IsNullOrWhiteSpace(teacherNo))
        {
            foreach (var field in new[] { "teacherNo", "TeacherNo", "teacherNumber", "TeacherNumber" })
            {
                await AddQuery(collectionRef.WhereEqualTo(field, teacherNo));
            }
        }

        foreach (var assignmentId in assignmentIds.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            foreach (var field in new[] { "assignmentId", "AssignmentId", "homeworkId", "HomeworkId" })
            {
                await AddQuery(collectionRef.WhereEqualTo(field, assignmentId));
            }
        }

        return result;
    }

    private async Task<List<Dictionary<string, object>>> LoadTeacherAnnouncements()
    {
        var result = new List<Dictionary<string, object>>();

        var snapshot = await _firestore.Collection("announcements").GetSnapshotAsync();

        foreach (var doc in snapshot.Documents)
        {
            var data = doc.ToDictionary();

            if (IsDeleted(data))
            {
                continue;
            }

            var target = FirstNonEmpty(
                GetText(data, "target", "Target"),
                GetText(data, "targetRole", "TargetRole"),
                GetText(data, "audience", "Audience"),
                "Tüm Okul"
            );

            var targetKey = NormalizeKey(target);

            if (!IsTeacherAnnouncementTarget(targetKey))
            {
                continue;
            }

            var title = FirstNonEmpty(
                GetText(data, "title", "Title"),
                GetText(data, "name", "Name"),
                "Duyuru"
            );

            var content = FirstNonEmpty(
                GetText(data, "content", "Content"),
                GetText(data, "message", "Message"),
                GetText(data, "description", "Description")
            );

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            data["Id"] = doc.Id;
            data["id"] = doc.Id;
            data["Title"] = title;
            data["title"] = title;
            data["Content"] = content;
            data["content"] = content;
            data["Target"] = target;
            data["target"] = target;

            result.Add(data);
        }

        return result
            .OrderByDescending(x => GetDate(x, "createdAt", "CreatedAt", "publishedAt", "PublishedAt") ?? DateTime.MinValue)
            .ToList();
    }

    private static bool IsTeacherAnnouncementTarget(string targetKey)
    {
        if (string.IsNullOrWhiteSpace(targetKey))
        {
            return true;
        }

        return targetKey.Contains("tum") ||
               targetKey.Contains("herkes") ||
               targetKey.Contains("genel") ||
               targetKey.Contains("okul") ||
               targetKey.Contains("all") ||
               targetKey.Contains("ogretmen") ||
               targetKey.Contains("teacher");
    }

    private async Task UpdateAssignmentEverywhere(string assignmentId, Dictionary<string, object?> update)
    {
        var collections = new[] { "homeworks", "assignments" };

        foreach (var collection in collections)
        {
            var directDoc = await _firestore.Collection(collection).Document(assignmentId).GetSnapshotAsync();

            if (directDoc.Exists)
            {
                await directDoc.Reference.SetAsync(update, SetOptions.MergeAll);
            }
        }
    }

    private static AssignmentViewModel BuildAssignmentViewModel(Dictionary<string, object> data)
    {
        return new AssignmentViewModel
        {
            Id = GetText(data, "Id", "id"),
            Title = FirstNonEmpty(GetText(data, "Title", "title"), GetText(data, "Name", "name")),
            LessonId = GetText(data, "LessonId", "lessonId"),
            Lesson = GetText(data, "LessonName", "lessonName", "Lesson", "lesson"),
            ClassName = GetText(data, "ClassName", "className", "Class", "class"),
            TeacherId = GetText(data, "TeacherId", "teacherId"),
            TeacherName = GetText(data, "TeacherName", "teacherName"),
            TeacherBranch = GetText(data, "TeacherBranch", "teacherBranch", "Branch", "branch"),
            DueDate = FormatDateInput(GetDate(data, "DueDate", "dueDate", "Deadline", "deadline", "EndDate", "endDate")),
            Type = FirstNonEmpty(GetText(data, "FileType", "fileType"), GetText(data, "Type", "type"), "Metin / Link"),
            Status = FirstNonEmpty(GetText(data, "Status", "status"), "Aktif"),
            Description = FirstNonEmpty(GetText(data, "Description", "description"), GetText(data, "Content", "content"))
        };
    }

    private static List<TeacherLessonOptionViewModel> BuildLessonOptions(List<Dictionary<string, object>> lessons)
    {
        return lessons
            .Select(x => new TeacherLessonOptionViewModel
            {
                Id = GetText(x, "Id", "id"),
                Name = FirstNonEmpty(GetText(x, "Name", "name"), GetText(x, "LessonName", "lessonName"), "Ders"),
                ClassName = FirstNonEmpty(GetText(x, "ClassName", "className"), GetText(x, "Class", "class"), "-")
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .OrderBy(x => x.ClassName)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private static List<SubmissionViewModel> BuildSubmissionViewModels(List<Dictionary<string, object>> submissions)
    {
        return submissions.Select(x => new SubmissionViewModel
        {
            Id = GetText(x, "Id", "id"),
            AssignmentId = GetText(x, "AssignmentId", "assignmentId"),
            AssignmentTitle = FirstNonEmpty(GetText(x, "Title", "title"), GetText(x, "AssignmentTitle", "assignmentTitle"), "Ödev"),
            Lesson = FirstNonEmpty(GetText(x, "LessonName", "lessonName"), GetText(x, "Lesson", "lesson"), "-"),
            ClassName = FirstNonEmpty(GetText(x, "ClassName", "className"), "-"),
            StudentNo = FirstNonEmpty(GetText(x, "StudentNo", "studentNo"), "-"),
            Answer = FirstNonEmpty(GetText(x, "Answer", "answer"), GetText(x, "Content", "content")),
            Link = GetText(x, "Link", "link"),
            Status = FirstNonEmpty(GetText(x, "Status", "status"), "Bekliyor"),
            Grade = GetText(x, "Score", "score", "Grade", "grade"),
            Feedback = GetText(x, "Feedback", "feedback")
        }).ToList();
    }

    private static string FormatDateInput(DateTime? date)
    {
        return date?.ToString("yyyy-MM-dd") ?? "";
    }

    private async Task UpdateSubmissionEverywhere(string submissionId, Dictionary<string, object?> update)
    {
        var collections = new[] { "submissions", "homework_submissions" };

        foreach (var collection in collections)
        {
            var directDoc = await _firestore.Collection(collection).Document(submissionId).GetSnapshotAsync();

            if (directDoc.Exists)
            {
                await directDoc.Reference.SetAsync(update, SetOptions.MergeAll);
            }
        }
    }

    private string GetSessionValue(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = HttpContext.Session.GetString(key);

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return "";
    }

    private static bool IsDeleted(Dictionary<string, object> data)
    {
        var deleted = GetText(data, "isDeleted", "IsDeleted", "deleted", "Deleted").Trim().ToLowerInvariant();
        var active = GetText(data, "isActive", "IsActive", "active", "Active").Trim().ToLowerInvariant();

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

    private static bool IsEvaluatedStatus(string status)
    {
        var key = NormalizeKey(status);

        return key.Contains("degerlendirildi") ||
               key.Contains("notlandi") ||
               key.Contains("notlandı") ||
               key.Contains("graded") ||
               key.Contains("evaluated");
    }

    private static string GetText(Dictionary<string, object> data, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!data.TryGetValue(key, out var value) || value == null)
            {
                continue;
            }

            if (value is Timestamp timestamp)
            {
                return timestamp.ToDateTime().ToLocalTime().ToString("dd.MM.yyyy HH:mm");
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
                return timestamp.ToDateTime().ToLocalTime();
            }

            if (value is DateTime dateTime)
            {
                return dateTime.ToLocalTime();
            }

            if (DateTime.TryParse(value.ToString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static string OnlyDigits(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static string NormalizeClassName(string value)
    {
        var original = (value ?? "").Trim();

        if (string.IsNullOrWhiteSpace(original))
        {
            return "";
        }

        var text = original
            .ToUpperInvariant()
            .Replace("SINIF", "")
            .Replace("SİNİF", "")
            .Replace("ŞUBE", "")
            .Replace("SUBE", "")
            .Replace("_", "-")
            .Replace("/", "-")
            .Replace("\\", "-")
            .Replace(".", "-");

        text = Regex.Replace(text, @"\s+", "");

        var match = Regex.Match(text, @"(9|10|11|12)[^\dA-Z]*([A-Z])");

        if (match.Success)
        {
            return $"{match.Groups[1].Value}-{match.Groups[2].Value}";
        }

        match = Regex.Match(text, @"([A-Z])[^\dA-Z]*(9|10|11|12)");

        if (match.Success)
        {
            return $"{match.Groups[2].Value}-{match.Groups[1].Value}";
        }

        return original.ToUpperInvariant();
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
            .Replace("İ", "i")
            .Replace("Ğ", "g")
            .Replace("Ü", "u")
            .Replace("Ş", "s")
            .Replace("Ö", "o")
            .Replace("Ç", "c");

        return new string(value.Where(char.IsLetterOrDigit).ToArray());
    }
}
