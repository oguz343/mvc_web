using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using mvc_web.Services;
using mvc_web.Filters;
namespace mvc_web.Controllers;

[AdminOnly]
public class LessonsController : Controller
{
    private readonly FirestoreDb _firestore;
    private readonly DataIntegrityService _integrity;

    public LessonsController(FirestoreDb firestore)
    {
        _firestore = firestore;
        _integrity = new DataIntegrityService(firestore);
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        await _integrity.CleanupOrphanActiveLinksAsync();

        var lessons = await _integrity.LoadVisibleLessonsAsync();

        return View(lessons);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await _integrity.CleanupOrphanActiveLinksAsync();

        ViewBag.Teachers = await _integrity.LoadActiveTeachersAsync();
        ViewBag.Classes = await _integrity.LoadActiveClassesAsync();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(IFormCollection form)
    {
        var name = (form["name"].ToString() ?? "").Trim();
        var teacherNo = DataIntegrityService.OnlyDigits(form["teacherNo"].ToString() ?? "");

        var selectedClassNames = form["classNames"]
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => DataIntegrityService.NormalizeClassName(x ?? ""))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var legacyClassName = DataIntegrityService.NormalizeClassName(form["className"].ToString() ?? "");

        if (!string.IsNullOrWhiteSpace(legacyClassName) &&
            !selectedClassNames.Contains(legacyClassName))
        {
            selectedClassNames.Add(legacyClassName);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Ders ataması için ders adı boş bırakılamaz.";
            return RedirectToAction(nameof(Create));
        }

        if (!selectedClassNames.Any())
        {
            TempData["Error"] = "En az bir sınıf seçmelisiniz.";
            return RedirectToAction(nameof(Create));
        }

        if (string.IsNullOrWhiteSpace(teacherNo))
        {
            TempData["Error"] = "Öğretmen seçmelisiniz.";
            return RedirectToAction(nameof(Create));
        }

        await _integrity.CleanupOrphanActiveLinksAsync();

        var teachers = await _integrity.LoadActiveTeachersAsync();
        var classes = await _integrity.LoadActiveClassesAsync();

        var teacher = teachers.FirstOrDefault(x =>
            DataIntegrityService.OnlyDigits(x.Number) == teacherNo
        );

        var selectedClasses = classes
            .Where(x => selectedClassNames.Contains(DataIntegrityService.NormalizeClassName(x.Name)))
            .ToList();

        if (teacher == null)
        {
            TempData["Error"] = "Seçilen öğretmen aktif değil veya silinmiş.";
            return RedirectToAction(nameof(Create));
        }

        if (selectedClasses.Count != selectedClassNames.Count)
        {
            TempData["Error"] = "Seçilen sınıflardan biri aktif değil veya silinmiş.";
            return RedirectToAction(nameof(Create));
        }

        var createdCount = 0;
        var skippedCount = 0;

        foreach (var selectedClass in selectedClasses)
        {
            var className = DataIntegrityService.NormalizeClassName(selectedClass.Name);
            var exists = await _integrity.LessonExistsAsync(name, className, teacherNo);

            if (exists)
            {
                skippedCount++;
                continue;
            }

            var now = Timestamp.FromDateTime(DateTime.UtcNow);

            var data = new Dictionary<string, object?>
            {
                ["name"] = name,
                ["Name"] = name,
                ["lessonName"] = name,
                ["LessonName"] = name,
                ["title"] = name,
                ["Title"] = name,
                ["courseName"] = name,
                ["CourseName"] = name,

                ["className"] = selectedClass.Name,
                ["ClassName"] = selectedClass.Name,
                ["class"] = selectedClass.Name,
                ["Class"] = selectedClass.Name,
                ["targetClass"] = selectedClass.Name,
                ["TargetClass"] = selectedClass.Name,

                ["teacherId"] = teacher.Id,
                ["TeacherId"] = teacher.Id,
                ["teacherName"] = teacher.Name,
                ["TeacherName"] = teacher.Name,
                ["teacher"] = teacher.Name,
                ["Teacher"] = teacher.Name,
                ["teacherNo"] = teacher.Number,
                ["TeacherNo"] = teacher.Number,
                ["teacherNumber"] = teacher.Number,
                ["TeacherNumber"] = teacher.Number,

                ["branch"] = teacher.Branch,
                ["Branch"] = teacher.Branch,
                ["teacherBranch"] = teacher.Branch,
                ["TeacherBranch"] = teacher.Branch,

                ["isDeleted"] = false,
                ["IsDeleted"] = false,
                ["isActive"] = true,
                ["IsActive"] = true,
                ["createdAt"] = now,
                ["CreatedAt"] = now,
                ["updatedAt"] = now,
                ["UpdatedAt"] = now,
            };

            await _firestore.Collection("lessons").AddAsync(data);
            createdCount++;
        }

        if (createdCount == 0)
        {
            TempData["Error"] = "Seçtiğiniz ders, sınıf ve öğretmen eşleşmeleri zaten kayıtlı.";
            return RedirectToAction(nameof(Create));
        }

        TempData["Success"] = createdCount == 1
            ? "Ders ataması başarıyla eklendi."
            : $"{createdCount} ders ataması başarıyla eklendi.";

        if (skippedCount > 0)
        {
            TempData["Success"] += $" {skippedCount} mevcut atama tekrar eklenmedi.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        id = (id ?? "").Trim();

        if (string.IsNullOrWhiteSpace(id))
        {
            TempData["Error"] = "Ders ataması bulunamadı.";
            return RedirectToAction(nameof(Index));
        }

        await _integrity.CleanupOrphanActiveLinksAsync();

        var doc = await _firestore.Collection("lessons").Document(id).GetSnapshotAsync();

        if (!doc.Exists)
        {
            TempData["Error"] = "Ders ataması bulunamadı.";
            return RedirectToAction(nameof(Index));
        }

        var data = doc.ToDictionary();

        if (DataIntegrityService.IsDeleted(data))
        {
            TempData["Error"] = "Bu ders ataması silinmiş veya bağlantısı kopmuş.";
            return RedirectToAction(nameof(Index));
        }

        var model = new LessonEditPageModel
        {
            Id = doc.Id,
            Name = DataIntegrityService.FirstNonEmpty(
                DataIntegrityService.GetString(data, "name", "Name"),
                DataIntegrityService.GetString(data, "lessonName", "LessonName"),
                DataIntegrityService.GetString(data, "title", "Title"),
                DataIntegrityService.GetString(data, "courseName", "CourseName")
            ),
            ClassName = DataIntegrityService.NormalizeClassName(
                DataIntegrityService.FirstNonEmpty(
                    DataIntegrityService.GetString(data, "className", "ClassName"),
                    DataIntegrityService.GetString(data, "class", "Class"),
                    DataIntegrityService.GetString(data, "targetClass", "TargetClass")
                )
            ),
            TeacherNo = DataIntegrityService.OnlyDigits(
                DataIntegrityService.FirstNonEmpty(
                    DataIntegrityService.GetString(data, "teacherNo", "TeacherNo"),
                    DataIntegrityService.GetString(data, "teacherNumber", "TeacherNumber"),
                    DataIntegrityService.GetString(data, "number", "Number")
                )
            ),
            Teachers = await _integrity.LoadActiveTeachersAsync(),
            Classes = await _integrity.LoadActiveClassesAsync(),
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, string name, string className, string teacherNo)
    {
        id = (id ?? "").Trim();
        name = (name ?? "").Trim();
        className = DataIntegrityService.NormalizeClassName(className);
        teacherNo = DataIntegrityService.OnlyDigits(teacherNo);

        if (string.IsNullOrWhiteSpace(id))
        {
            TempData["Error"] = "Ders ataması bulunamadı.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Ders ataması için ders adı boş bırakılamaz.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        if (string.IsNullOrWhiteSpace(className))
        {
            TempData["Error"] = "Sınıf seçmelisiniz.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        if (string.IsNullOrWhiteSpace(teacherNo))
        {
            TempData["Error"] = "Öğretmen seçmelisiniz.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        await _integrity.CleanupOrphanActiveLinksAsync();

        var teachers = await _integrity.LoadActiveTeachersAsync();
        var classes = await _integrity.LoadActiveClassesAsync();

        var teacher = teachers.FirstOrDefault(x =>
            DataIntegrityService.OnlyDigits(x.Number) == teacherNo
        );

        var selectedClass = classes.FirstOrDefault(x =>
            DataIntegrityService.NormalizeClassName(x.Name) == className
        );

        if (teacher == null)
        {
            TempData["Error"] = "Seçilen öğretmen aktif değil veya silinmiş.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        if (selectedClass == null)
        {
            TempData["Error"] = "Seçilen sınıf aktif değil veya silinmiş.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var exists = await _integrity.LessonExistsAsync(name, className, teacherNo, id);

        if (exists)
        {
            TempData["Error"] = "Bu ders, sınıf ve öğretmen eşleşmesi zaten kayıtlı.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var update = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["Name"] = name,
            ["lessonName"] = name,
            ["LessonName"] = name,
            ["title"] = name,
            ["Title"] = name,
            ["courseName"] = name,
            ["CourseName"] = name,

            ["className"] = selectedClass.Name,
            ["ClassName"] = selectedClass.Name,
            ["class"] = selectedClass.Name,
            ["Class"] = selectedClass.Name,
            ["targetClass"] = selectedClass.Name,
            ["TargetClass"] = selectedClass.Name,

            ["teacherId"] = teacher.Id,
            ["TeacherId"] = teacher.Id,
            ["teacherName"] = teacher.Name,
            ["TeacherName"] = teacher.Name,
            ["teacher"] = teacher.Name,
            ["Teacher"] = teacher.Name,
            ["teacherNo"] = teacher.Number,
            ["TeacherNo"] = teacher.Number,
            ["teacherNumber"] = teacher.Number,
            ["TeacherNumber"] = teacher.Number,

            ["branch"] = teacher.Branch,
            ["Branch"] = teacher.Branch,
            ["teacherBranch"] = teacher.Branch,
            ["TeacherBranch"] = teacher.Branch,

            ["isDeleted"] = false,
            ["IsDeleted"] = false,
            ["isActive"] = true,
            ["IsActive"] = true,
            ["updatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
            ["UpdatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
        };

        await _firestore.Collection("lessons").Document(id).SetAsync(update, SetOptions.MergeAll);

        TempData["Success"] = "Ders ataması başarıyla güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        id = (id ?? "").Trim();

        if (string.IsNullOrWhiteSpace(id))
        {
            TempData["Error"] = "Ders ataması bulunamadı.";
            return RedirectToAction(nameof(Index));
        }

        await _integrity.SoftDeleteLessonAndItsActiveAssignmentsAsync(id);

        TempData["Success"] = "Ders ataması ve bağlı aktif ödevleri pasife alındı.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cleanup()
    {
        await _integrity.CleanupOrphanActiveLinksAsync();

        TempData["Success"] = "Bozuk bağlantılı ders ve aktif ödevler temizlendi.";
        return RedirectToAction(nameof(Index));
    }
}

public class LessonEditPageModel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string TeacherNo { get; set; } = "";
    public List<TeacherOption> Teachers { get; set; } = new();
    public List<ClassOption> Classes { get; set; } = new();
}
