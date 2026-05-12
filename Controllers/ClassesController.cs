using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using mvc_web.Models;
using System.Text.RegularExpressions;

namespace mvc_web.Controllers
{
    public class ClassesController : Controller
    {
        private readonly FirestoreDb _firestore;

        public ClassesController(FirestoreDb firestore)
        {
            _firestore = firestore;
        }

        public async Task<IActionResult> Index()
        {
            var snapshot = await _firestore
                .Collection("classes")
                .OrderBy("grade")
                .GetSnapshotAsync();

            var classes = new List<ClassViewModel>();

            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();

                var name = NormalizeClassName(
                    GetString(data, "name", "Name", "className", "ClassName")
                );

                var gradeText = GetString(data, "grade", "Grade", "level", "Level");
                var branch = GetString(data, "branch", "Branch", "section", "Section");

                if (string.IsNullOrWhiteSpace(name))
                {
                    name = NormalizeClassName($"{gradeText}-{branch}");
                }

                var parsed = ParseClassName(name);

                var model = new ClassViewModel
                {
                    Id = doc.Id,
                    Name = name,
                    Grade = parsed.grade,
                    Branch = parsed.branch,
                    TeacherId = GetString(data, "teacherId", "TeacherId"),
                    TeacherName = GetString(data, "teacherName", "TeacherName"),
                    Capacity = GetInt(data, 30, "capacity", "Capacity"),
                    StudentCount = await CountStudentsByClass(name)
                };

                classes.Add(model);
            }

            return View(classes);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new ClassViewModel
            {
                Grade = 9,
                Branch = "A",
                Name = "9-A",
                Capacity = 30,
                Teachers = await GetTeachers()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClassViewModel model)
        {
            ModelState.Clear();

            model.Branch = CleanText(model.Branch).ToUpperInvariant();
            model.Name = NormalizeClassName($"{model.Grade}-{model.Branch}");

            if (model.Grade < 9 || model.Grade > 12)
            {
                ModelState.AddModelError(nameof(model.Grade), "Sınıf seviyesi 9, 10, 11 veya 12 olmalıdır.");
            }

            if (!Regex.IsMatch(model.Branch ?? "", "^[A-F]$"))
            {
                ModelState.AddModelError(nameof(model.Branch), "Şube A, B, C, D, E veya F olmalıdır.");
            }

            if (model.Capacity <= 0)
            {
                ModelState.AddModelError(nameof(model.Capacity), "Kapasite 1 veya daha büyük olmalıdır.");
            }

            if (await ClassExists(model.Name))
            {
                ModelState.AddModelError(nameof(model.Name), "Bu sınıf zaten kayıtlı.");
            }

            if (!ModelState.IsValid)
            {
                model.Teachers = await GetTeachers();
                return View(model);
            }

            var teacherName = await GetTeacherName(model.TeacherId);
            var now = Timestamp.GetCurrentTimestamp();

            await _firestore.Collection("classes").AddAsync(new Dictionary<string, object>
            {
                { "name", model.Name },
                { "className", model.Name },
                { "grade", model.Grade },
                { "branch", model.Branch },
                { "teacherId", model.TeacherId ?? "" },
                { "teacherName", teacherName },
                { "capacity", model.Capacity },
                { "studentCount", await CountStudentsByClass(model.Name) },
                { "createdAt", now },
                { "updatedAt", now }
            });

            TempData["Success"] = "Sınıf oluşturuldu.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Sınıf bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            var doc = await _firestore.Collection("classes").Document(id).GetSnapshotAsync();

            if (!doc.Exists)
            {
                TempData["Error"] = "Sınıf bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            var data = doc.ToDictionary();

            var name = NormalizeClassName(
                GetString(data, "name", "Name", "className", "ClassName")
            );

            if (string.IsNullOrWhiteSpace(name))
            {
                name = NormalizeClassName(
                    $"{GetString(data, "grade", "Grade")}-{GetString(data, "branch", "Branch")}"
                );
            }

            var parsed = ParseClassName(name);

            var model = new ClassViewModel
            {
                Id = doc.Id,
                Name = name,
                Grade = parsed.grade,
                Branch = parsed.branch,
                TeacherId = GetString(data, "teacherId", "TeacherId"),
                TeacherName = GetString(data, "teacherName", "TeacherName"),
                Capacity = GetInt(data, 30, "capacity", "Capacity"),
                StudentCount = await CountStudentsByClass(name),
                Teachers = await GetTeachers()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ClassViewModel model)
        {
            ModelState.Clear();

            if (string.IsNullOrWhiteSpace(model.Id))
            {
                TempData["Error"] = "Sınıf bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            model.Branch = CleanText(model.Branch).ToUpperInvariant();
            model.Name = NormalizeClassName($"{model.Grade}-{model.Branch}");

            if (model.Grade < 9 || model.Grade > 12)
            {
                ModelState.AddModelError(nameof(model.Grade), "Sınıf seviyesi 9, 10, 11 veya 12 olmalıdır.");
            }

            if (!Regex.IsMatch(model.Branch ?? "", "^[A-F]$"))
            {
                ModelState.AddModelError(nameof(model.Branch), "Şube A, B, C, D, E veya F olmalıdır.");
            }

            if (model.Capacity <= 0)
            {
                ModelState.AddModelError(nameof(model.Capacity), "Kapasite 1 veya daha büyük olmalıdır.");
            }

            if (await ClassExists(model.Name, model.Id))
            {
                ModelState.AddModelError(nameof(model.Name), "Bu sınıf başka bir kayıtta zaten var.");
            }

            if (!ModelState.IsValid)
            {
                model.Teachers = await GetTeachers();
                model.StudentCount = await CountStudentsByClass(model.Name);
                return View(model);
            }

            var teacherName = await GetTeacherName(model.TeacherId);
            var studentCount = await CountStudentsByClass(model.Name);

            await _firestore.Collection("classes").Document(model.Id).UpdateAsync(new Dictionary<string, object>
            {
                { "name", model.Name },
                { "className", model.Name },
                { "grade", model.Grade },
                { "branch", model.Branch },
                { "teacherId", model.TeacherId ?? "" },
                { "teacherName", teacherName },
                { "capacity", model.Capacity },
                { "studentCount", studentCount },
                { "updatedAt", Timestamp.GetCurrentTimestamp() }
            });

            TempData["Success"] = "Sınıf güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Sınıf bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            await _firestore.Collection("classes").Document(id).DeleteAsync();

            TempData["Success"] = "Sınıf silindi.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<List<SelectListItem>> GetTeachers()
        {
            var result = new List<SelectListItem>
            {
                new SelectListItem
                {
                    Value = "",
                    Text = "Öğretmen seç"
                }
            };

            var snapshot = await _firestore
                .Collection("users")
                .WhereEqualTo("role", "Öğretmen")
                .GetSnapshotAsync();

            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();

                var name = GetString(data, "name", "Name");
                var branch = GetString(data, "branch", "Branch");

                result.Add(new SelectListItem
                {
                    Value = doc.Id,
                    Text = string.IsNullOrWhiteSpace(branch)
                        ? name
                        : $"{name} • {branch}"
                });
            }

            return result;
        }

        private async Task<string> GetTeacherName(string? teacherId)
        {
            if (string.IsNullOrWhiteSpace(teacherId))
            {
                return "";
            }

            var doc = await _firestore.Collection("users").Document(teacherId).GetSnapshotAsync();

            if (!doc.Exists)
            {
                return "";
            }

            var data = doc.ToDictionary();

            return GetString(data, "name", "Name");
        }

        private async Task<int> CountStudentsByClass(string className)
        {
            var target = NormalizeClassName(className);

            if (string.IsNullOrWhiteSpace(target))
            {
                return 0;
            }

            var snapshot = await _firestore
                .Collection("users")
                .WhereEqualTo("role", "Öğrenci")
                .GetSnapshotAsync();

            int count = 0;

            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();

                var userClass = NormalizeClassName(
                    GetString(data, "className", "ClassName", "class", "Class")
                );

                if (userClass == target)
                {
                    count++;
                }
            }

            return count;
        }

        private async Task<bool> ClassExists(string className, string? excludedId = null)
        {
            var target = NormalizeClassName(className);

            var snapshot = await _firestore.Collection("classes").GetSnapshotAsync();

            foreach (var doc in snapshot.Documents)
            {
                if (!string.IsNullOrWhiteSpace(excludedId) && doc.Id == excludedId)
                {
                    continue;
                }

                var data = doc.ToDictionary();

                var name = NormalizeClassName(
                    GetString(data, "name", "Name", "className", "ClassName")
                );

                if (name == target)
                {
                    return true;
                }
            }

            return false;
        }

        private static (int grade, string branch) ParseClassName(string value)
        {
            var normalized = NormalizeClassName(value);
            var match = Regex.Match(normalized, @"^(9|10|11|12)-([A-F])$");

            if (match.Success)
            {
                return (int.Parse(match.Groups[1].Value), match.Groups[2].Value);
            }

            return (9, "A");
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

        private static int GetInt(Dictionary<string, object> data, int defaultValue, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!data.TryGetValue(key, out var value) || value == null)
                {
                    continue;
                }

                if (value is int intValue)
                {
                    return intValue;
                }

                if (int.TryParse(value.ToString(), out var parsed))
                {
                    return parsed;
                }
            }

            return defaultValue;
        }

        private static string CleanText(string value)
        {
            return (value ?? "")
                .Replace("\u00A0", " ")
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
        }
    }
}