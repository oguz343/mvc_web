using Google.Cloud.Firestore;
using mvc_web.Models;
using System.Text.RegularExpressions;

namespace mvc_web.Services;

public class DataIntegrityService
{
    private readonly FirestoreDb _firestore;

    public DataIntegrityService(FirestoreDb firestore)
    {
        _firestore = firestore;
    }

    public async Task CleanupOrphanActiveLinksAsync()
    {
        var teachers = await LoadActiveTeachersAsync();
        var classes = await LoadActiveClassesAsync();

        var activeTeacherIds = teachers
            .Select(x => NormalizeKey(x.Id))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet();

        var activeTeacherNos = teachers
            .Select(x => OnlyDigits(x.Number))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet();

        var activeTeacherNames = teachers
            .Select(x => NormalizeKey(x.Name))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet();

        var activeClassNames = classes
            .Select(x => NormalizeClassName(x.Name))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet();

        var deletedLessonIds = new HashSet<string>();
        var deletedLessonKeys = new HashSet<string>();

        var lessonSnapshot = await _firestore.Collection("lessons").GetSnapshotAsync();

        foreach (var doc in lessonSnapshot.Documents)
        {
            var data = doc.ToDictionary();

            if (IsDeleted(data))
            {
                continue;
            }

            var lessonName = FirstNonEmpty(
                GetString(data, "name", "Name"),
                GetString(data, "lessonName", "LessonName"),
                GetString(data, "title", "Title"),
                GetString(data, "courseName", "CourseName"),
                GetString(data, "lesson", "Lesson")
            );

            var className = NormalizeClassName(FirstNonEmpty(
                GetString(data, "className", "ClassName"),
                GetString(data, "class", "Class"),
                GetString(data, "targetClass", "TargetClass"),
                GetString(data, "schoolClass", "SchoolClass")
            ));

            var teacherId = NormalizeKey(FirstNonEmpty(
                GetString(data, "teacherId", "TeacherId"),
                GetString(data, "teacherUid", "TeacherUid"),
                GetString(data, "userId", "UserId"),
                GetString(data, "teacherDocId", "TeacherDocId")
            ));

            var teacherNo = OnlyDigits(FirstNonEmpty(
                GetString(data, "teacherNo", "TeacherNo"),
                GetString(data, "teacherNumber", "TeacherNumber"),
                GetString(data, "teacherSchoolNo", "TeacherSchoolNo"),
                GetString(data, "number", "Number"),
                GetString(data, "schoolNo", "SchoolNo")
            ));

            var teacherName = FirstNonEmpty(
                GetString(data, "teacherName", "TeacherName"),
                GetString(data, "teacherFullName", "TeacherFullName"),
                GetString(data, "teacher", "Teacher")
            );

            var teacherStillActive =
                (!string.IsNullOrWhiteSpace(teacherId) && activeTeacherIds.Contains(teacherId)) ||
                (!string.IsNullOrWhiteSpace(teacherNo) && activeTeacherNos.Contains(teacherNo)) ||
                (!string.IsNullOrWhiteSpace(teacherName) && activeTeacherNames.Contains(NormalizeKey(teacherName)));

            var classStillActive =
                !string.IsNullOrWhiteSpace(className) &&
                activeClassNames.Contains(className);

            if (!teacherStillActive || !classStillActive)
            {
                await SoftDeleteDocumentAsync(doc.Reference, "Bağlı öğretmen veya sınıf aktif değil.");

                deletedLessonIds.Add(doc.Id);
                deletedLessonKeys.Add(NormalizeKey($"{lessonName}_{className}"));
            }
        }

        await CleanupOrphanAssignmentsAsync(
            activeTeacherIds,
            activeTeacherNos,
            activeTeacherNames,
            activeClassNames,
            deletedLessonIds,
            deletedLessonKeys
        );
    }

    public async Task<List<LessonViewModel>> LoadVisibleLessonsAsync()
    {
        var teachers = await LoadActiveTeachersAsync();
        var classes = await LoadActiveClassesAsync();

        return await LoadVisibleLessonsAsync(teachers, classes);
    }

    public async Task<List<LessonViewModel>> LoadVisibleLessonsAsync(
        List<TeacherOption> activeTeachers,
        List<ClassOption> activeClasses)
    {
        var result = new List<LessonViewModel>();
        var seen = new HashSet<string>();

        var activeTeacherIds = activeTeachers
            .Select(x => NormalizeKey(x.Id))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet();

        var activeTeacherNos = activeTeachers
            .Select(x => OnlyDigits(x.Number))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet();

        var activeTeacherNames = activeTeachers
            .Select(x => NormalizeKey(x.Name))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet();

        var activeClassNames = activeClasses
            .Select(x => NormalizeClassName(x.Name))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet();

        var snapshot = await _firestore.Collection("lessons").GetSnapshotAsync();

        foreach (var doc in snapshot.Documents)
        {
            var data = doc.ToDictionary();

            if (IsDeleted(data))
            {
                continue;
            }

            var lessonName = FirstNonEmpty(
                GetString(data, "name", "Name"),
                GetString(data, "lessonName", "LessonName"),
                GetString(data, "title", "Title"),
                GetString(data, "courseName", "CourseName"),
                GetString(data, "lesson", "Lesson"),
                "Ders"
            );

            var className = NormalizeClassName(FirstNonEmpty(
                GetString(data, "className", "ClassName"),
                GetString(data, "class", "Class"),
                GetString(data, "targetClass", "TargetClass"),
                GetString(data, "schoolClass", "SchoolClass")
            ));

            var teacherId = NormalizeKey(FirstNonEmpty(
                GetString(data, "teacherId", "TeacherId"),
                GetString(data, "teacherUid", "TeacherUid"),
                GetString(data, "userId", "UserId"),
                GetString(data, "teacherDocId", "TeacherDocId")
            ));

            var teacherNo = OnlyDigits(FirstNonEmpty(
                GetString(data, "teacherNo", "TeacherNo"),
                GetString(data, "teacherNumber", "TeacherNumber"),
                GetString(data, "teacherSchoolNo", "TeacherSchoolNo"),
                GetString(data, "number", "Number"),
                GetString(data, "schoolNo", "SchoolNo")
            ));

            var teacherName = FirstNonEmpty(
                GetString(data, "teacherName", "TeacherName"),
                GetString(data, "teacherFullName", "TeacherFullName"),
                GetString(data, "teacher", "Teacher")
            );

            var branch = FirstNonEmpty(
                GetString(data, "branch", "Branch"),
                GetString(data, "teacherBranch", "TeacherBranch"),
                GetString(data, "subject", "Subject")
            );

            var teacherStillActive =
                (!string.IsNullOrWhiteSpace(teacherId) && activeTeacherIds.Contains(teacherId)) ||
                (!string.IsNullOrWhiteSpace(teacherNo) && activeTeacherNos.Contains(teacherNo)) ||
                (!string.IsNullOrWhiteSpace(teacherName) && activeTeacherNames.Contains(NormalizeKey(teacherName)));

            var classStillActive =
                !string.IsNullOrWhiteSpace(className) &&
                activeClassNames.Contains(className);

            if (!teacherStillActive || !classStillActive)
            {
                continue;
            }

            var activeTeacher = activeTeachers.FirstOrDefault(x =>
                NormalizeKey(x.Id) == teacherId ||
                OnlyDigits(x.Number) == teacherNo ||
                NormalizeKey(x.Name) == NormalizeKey(teacherName)
            );

            var activeClass = activeClasses.FirstOrDefault(x =>
                NormalizeClassName(x.Name) == className
            );

            var cleanTeacherName = activeTeacher?.Name ?? teacherName;
            var cleanTeacherBranch = activeTeacher?.Branch ?? branch;
            var cleanClassName = activeClass?.Name ?? className;

            var key = NormalizeKey($"{lessonName}_{cleanClassName}_{cleanTeacherName}");

            if (seen.Contains(key))
            {
                continue;
            }

            seen.Add(key);

            result.Add(new LessonViewModel
            {
                Id = doc.Id,
                Name = lessonName,
                ClassName = cleanClassName,
                TeacherName = cleanTeacherName,
                TeacherBranch = cleanTeacherBranch,
            });
        }

        return result
            .OrderBy(x => x.ClassName)
            .ThenBy(x => x.Name)
            .ToList();
    }

    public async Task<List<TeacherOption>> LoadActiveTeachersAsync()
    {
        var result = new List<TeacherOption>();
        var seen = new HashSet<string>();

        var teacherDocs = await LoadUserDocsByRoleAsync("ogretmen");

        foreach (var doc in teacherDocs)
        {
            var data = doc.ToDictionary();

            var number = OnlyDigits(FirstNonEmpty(
                GetString(data, "number", "Number"),
                GetString(data, "schoolNo", "SchoolNo"),
                GetString(data, "teacherNo", "TeacherNo"),
                GetString(data, "teacherNumber", "TeacherNumber")
            ));

            var name = FirstNonEmpty(
                GetString(data, "name", "Name"),
                GetString(data, "fullName", "FullName"),
                GetString(data, "userName", "UserName"),
                GetString(data, "teacherName", "TeacherName"),
                "Öğretmen"
            );

            var branch = FirstNonEmpty(
                GetString(data, "branch", "Branch"),
                GetString(data, "teacherBranch", "TeacherBranch")
            );

            var key = NormalizeKey($"{doc.Id}_{number}_{name}");

            if (seen.Contains(key))
            {
                continue;
            }

            seen.Add(key);

            result.Add(new TeacherOption
            {
                Id = doc.Id,
                Name = name,
                Number = number,
                Branch = branch,
            });
        }

        return result
            .OrderBy(x => x.Name)
            .ToList();
    }

    private async Task<List<DocumentSnapshot>> LoadUserDocsByRoleAsync(string roleKey)
    {
        var roleFields = new[] { "role", "Role", "userRole", "UserRole" };
        var roleValues = RoleQueryValues(roleKey);

        async Task<QuerySnapshot?> ReadRoleQuery(string field, string value)
        {
            try
            {
                return await _firestore
                    .Collection("users")
                    .WhereEqualTo(field, value)
                    .GetSnapshotAsync();
            }
            catch
            {
                return null;
            }
        }

        var snapshots = await Task.WhenAll(
            roleFields.SelectMany(field =>
                roleValues.Select(value => ReadRoleQuery(field, value)))
        );

        var docs = new Dictionary<string, DocumentSnapshot>();

        foreach (var snapshot in snapshots)
        {
            if (snapshot == null)
            {
                continue;
            }

            AddMatchingUserDocs(snapshot.Documents, roleKey, docs);
        }

        if (docs.Count == 0)
        {
            try
            {
                var fallback = await _firestore.Collection("users").GetSnapshotAsync();
                AddMatchingUserDocs(fallback.Documents, roleKey, docs);
            }
            catch
            {
            }
        }

        return docs.Values.ToList();
    }

    private static void AddMatchingUserDocs(
        IEnumerable<DocumentSnapshot> source,
        string roleKey,
        Dictionary<string, DocumentSnapshot> target)
    {
        foreach (var doc in source)
        {
            var data = doc.ToDictionary();

            if (IsDeleted(data))
            {
                continue;
            }

            var role = NormalizeKey(GetString(data, "role", "Role", "userRole", "UserRole"));

            if (role != roleKey)
            {
                continue;
            }

            target.TryAdd(doc.Id, doc);
        }
    }

    private static string[] RoleQueryValues(string roleKey)
    {
        return roleKey switch
        {
            "ogretmen" => new[]
            {
                "\u00d6\u011fretmen",
                "\u00f6\u011fretmen",
                "Ogretmen",
                "ogretmen",
                "Teacher",
                "teacher"
            },
            _ => new[] { roleKey }
        };
    }

    public async Task<List<ClassOption>> LoadActiveClassesAsync()
    {
        var result = new List<ClassOption>();
        var seen = new HashSet<string>();

        var snapshot = await _firestore.Collection("classes").GetSnapshotAsync();

        foreach (var doc in snapshot.Documents)
        {
            var data = doc.ToDictionary();

            if (IsDeleted(data))
            {
                continue;
            }

            var name = NormalizeClassName(FirstNonEmpty(
                GetString(data, "name", "Name"),
                GetString(data, "className", "ClassName"),
                GetString(data, "class", "Class"),
                GetString(data, "schoolClass", "SchoolClass")
            ));

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var key = NormalizeClassName(name);

            if (seen.Contains(key))
            {
                continue;
            }

            seen.Add(key);

            result.Add(new ClassOption
            {
                Id = doc.Id,
                Name = name,
            });
        }

        return result
            .OrderBy(x => x.Name)
            .ToList();
    }

    public async Task<bool> LessonExistsAsync(string name, string className, string teacherNo, string exceptId = "")
    {
        var lessonKey = NormalizeKey(name);
        var classKey = NormalizeClassName(className);
        var teacherKey = OnlyDigits(teacherNo);

        var snapshot = await _firestore.Collection("lessons").GetSnapshotAsync();

        foreach (var doc in snapshot.Documents)
        {
            if (!string.IsNullOrWhiteSpace(exceptId) && doc.Id == exceptId)
            {
                continue;
            }

            var data = doc.ToDictionary();

            if (IsDeleted(data))
            {
                continue;
            }

            var currentLesson = NormalizeKey(FirstNonEmpty(
                GetString(data, "name", "Name"),
                GetString(data, "lessonName", "LessonName"),
                GetString(data, "title", "Title"),
                GetString(data, "courseName", "CourseName")
            ));

            var currentClass = NormalizeClassName(FirstNonEmpty(
                GetString(data, "className", "ClassName"),
                GetString(data, "class", "Class"),
                GetString(data, "targetClass", "TargetClass")
            ));

            var currentTeacher = OnlyDigits(FirstNonEmpty(
                GetString(data, "teacherNo", "TeacherNo"),
                GetString(data, "teacherNumber", "TeacherNumber"),
                GetString(data, "number", "Number")
            ));

            if (currentLesson == lessonKey &&
                currentClass == classKey &&
                currentTeacher == teacherKey)
            {
                return true;
            }
        }

        return false;
    }

    public async Task SoftDeleteLessonAndItsActiveAssignmentsAsync(string lessonId)
    {
        if (string.IsNullOrWhiteSpace(lessonId))
        {
            return;
        }

        var lessonDoc = await _firestore.Collection("lessons").Document(lessonId).GetSnapshotAsync();

        string lessonName = "";
        string className = "";

        if (lessonDoc.Exists)
        {
            var data = lessonDoc.ToDictionary();

            lessonName = FirstNonEmpty(
                GetString(data, "name", "Name"),
                GetString(data, "lessonName", "LessonName"),
                GetString(data, "title", "Title"),
                GetString(data, "courseName", "CourseName")
            );

            className = NormalizeClassName(FirstNonEmpty(
                GetString(data, "className", "ClassName"),
                GetString(data, "class", "Class"),
                GetString(data, "targetClass", "TargetClass")
            ));

            await SoftDeleteDocumentAsync(lessonDoc.Reference, "Ders admin tarafından silindi.");
        }

        await SoftDeleteAssignmentsForLessonAsync(lessonId, lessonName, className);
    }

    private async Task CleanupOrphanAssignmentsAsync(
        HashSet<string> activeTeacherIds,
        HashSet<string> activeTeacherNos,
        HashSet<string> activeTeacherNames,
        HashSet<string> activeClassNames,
        HashSet<string> deletedLessonIds,
        HashSet<string> deletedLessonKeys)
    {
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
                    GetString(data, "lessonId", "LessonId"),
                    GetString(data, "courseId", "CourseId")
                );

                var lessonName = FirstNonEmpty(
                    GetString(data, "lessonName", "LessonName"),
                    GetString(data, "lesson", "Lesson"),
                    GetString(data, "courseName", "CourseName")
                );

                var className = NormalizeClassName(FirstNonEmpty(
                    GetString(data, "className", "ClassName"),
                    GetString(data, "class", "Class"),
                    GetString(data, "targetClass", "TargetClass")
                ));

                var teacherId = NormalizeKey(FirstNonEmpty(
                    GetString(data, "teacherId", "TeacherId"),
                    GetString(data, "teacherUid", "TeacherUid"),
                    GetString(data, "userId", "UserId")
                ));

                var teacherNo = OnlyDigits(FirstNonEmpty(
                    GetString(data, "teacherNo", "TeacherNo"),
                    GetString(data, "teacherNumber", "TeacherNumber"),
                    GetString(data, "number", "Number")
                ));

                var teacherName = FirstNonEmpty(
                    GetString(data, "teacherName", "TeacherName"),
                    GetString(data, "teacher", "Teacher")
                );

                var lessonDeleted =
                    (!string.IsNullOrWhiteSpace(lessonId) && deletedLessonIds.Contains(lessonId)) ||
                    deletedLessonKeys.Contains(NormalizeKey($"{lessonName}_{className}"));

                var classStillActive =
                    !string.IsNullOrWhiteSpace(className) &&
                    activeClassNames.Contains(className);

                var hasTeacherField =
                    !string.IsNullOrWhiteSpace(teacherId) ||
                    !string.IsNullOrWhiteSpace(teacherNo) ||
                    !string.IsNullOrWhiteSpace(teacherName);

                var teacherStillActive =
                    (!string.IsNullOrWhiteSpace(teacherId) && activeTeacherIds.Contains(teacherId)) ||
                    (!string.IsNullOrWhiteSpace(teacherNo) && activeTeacherNos.Contains(teacherNo)) ||
                    (!string.IsNullOrWhiteSpace(teacherName) && activeTeacherNames.Contains(NormalizeKey(teacherName)));

                var shouldDelete =
                    lessonDeleted ||
                    !classStillActive ||
                    (hasTeacherField && !teacherStillActive);

                if (shouldDelete)
                {
                    await SoftDeleteDocumentAsync(doc.Reference, "Bağlı ders, sınıf veya öğretmen aktif değil.");
                }
            }
        }
    }

    private async Task SoftDeleteAssignmentsForLessonAsync(string lessonId, string lessonName, string className)
    {
        var collections = new[] { "homeworks", "assignments" };
        var lessonKey = NormalizeKey($"{lessonName}_{NormalizeClassName(className)}");

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

                var docLessonId = FirstNonEmpty(
                    GetString(data, "lessonId", "LessonId"),
                    GetString(data, "courseId", "CourseId")
                );

                var docLessonName = FirstNonEmpty(
                    GetString(data, "lessonName", "LessonName"),
                    GetString(data, "lesson", "Lesson"),
                    GetString(data, "courseName", "CourseName")
                );

                var docClassName = NormalizeClassName(FirstNonEmpty(
                    GetString(data, "className", "ClassName"),
                    GetString(data, "class", "Class"),
                    GetString(data, "targetClass", "TargetClass")
                ));

                var sameById =
                    !string.IsNullOrWhiteSpace(lessonId) &&
                    docLessonId == lessonId;

                var sameByKey =
                    !string.IsNullOrWhiteSpace(lessonKey) &&
                    NormalizeKey($"{docLessonName}_{docClassName}") == lessonKey;

                if (sameById || sameByKey)
                {
                    await SoftDeleteDocumentAsync(doc.Reference, "Bağlı ders silindi.");
                }
            }
        }
    }

    private static async Task SoftDeleteDocumentAsync(DocumentReference reference, string reason)
    {
        await reference.SetAsync(
            new Dictionary<string, object?>
            {
                ["isDeleted"] = true,
                ["IsDeleted"] = true,
                ["isActive"] = false,
                ["IsActive"] = false,
                ["deleteReason"] = reason,
                ["DeleteReason"] = reason,
                ["deletedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
                ["DeletedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
                ["updatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
                ["UpdatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow),
            },
            SetOptions.MergeAll
        );
    }

    public static bool IsDeleted(Dictionary<string, object> data)
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

    public static string GetString(Dictionary<string, object> data, params string[] keys)
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

    public static string FirstNonEmpty(params string[] values)
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

    public static string OnlyDigits(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return new string(value.Where(char.IsDigit).ToArray());
    }

    public static string NormalizeClassName(string value)
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

    public static string NormalizeKey(string value)
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

public class TeacherOption
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Number { get; set; } = "";
    public string Branch { get; set; } = "";
}

public class ClassOption
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}
