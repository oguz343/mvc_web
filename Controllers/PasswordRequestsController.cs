using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using mvc_web.Models;
using mvc_web.Services;
using mvc_web.Filters;
using System.Security.Cryptography;

namespace mvc_web.Controllers
{
    [AdminOnly]
    public class PasswordRequestsController : Controller
    {
        private readonly FirestoreDb _firestore;

        public PasswordRequestsController(FirestoreDb firestore)
        {
            _firestore = firestore;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var requests = await LoadPasswordRequests();

            return View(requests);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(string id, string collectionName)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Şifre talebi bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(collectionName))
            {
                collectionName = "passwordRequests";
            }

            var requestRef = _firestore.Collection(collectionName).Document(id);
            var requestDoc = await requestRef.GetSnapshotAsync();

            if (!requestDoc.Exists)
            {
                requestDoc = await FindRequestByIdInBothCollections(id);

                if (!requestDoc.Exists)
                {
                    TempData["Error"] = "Şifre talebi bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }

                requestRef = requestDoc.Reference;
            }

            var data = requestDoc.ToDictionary();

            var status = NormalizeKey(GetString(data, "status", "Status"));

            if (status == "onaylandi" || status == "approved")
            {
                TempData["Error"] = "Bu şifre talebi zaten onaylanmış.";
                return RedirectToAction(nameof(Index));
            }

            if (status == "reddedildi" || status == "rejected" || status == "red")
            {
                TempData["Error"] = "Reddedilen talep tekrar onaylanamaz.";
                return RedirectToAction(nameof(Index));
            }

            var userId = GetString(data, "userId", "UserId");
            var role = GetString(data, "role", "Role");

            var number = OnlyDigits(
                FirstNonEmpty(
                    GetString(data, "number", "Number"),
                    GetString(data, "schoolNo", "SchoolNo"),
                    GetString(data, "studentNo", "StudentNo")
                )
            );

            var userDoc = await FindUser(userId, role, number);

            if (userDoc == null)
            {
                TempData["Error"] = "Bu talebe ait kullanıcı bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            var activationCode = GenerateActivationCode();
            var activationHash = PasswordHashService.HashPassword(activationCode);
            var now = Timestamp.GetCurrentTimestamp();

            await userDoc.Reference.SetAsync(
                new Dictionary<string, object>
                {
                    { "activationCode", activationCode },
                    { "ActivationCode", activationCode },
                    { "passwordHash", activationHash },
                    { "PasswordHash", activationHash },
                    { "mustChangePassword", true },
                    { "MustChangePassword", true },
                    { "password", "" },
                    { "Password", "" },
                    { "updatedAt", now },
                    { "passwordResetAt", now }
                },
                SetOptions.MergeAll
            );

            var requestUpdate = new Dictionary<string, object>
            {
                { "status", "Onaylandı" },

                // Aktivasyon kodu artık sadece TempData ile üstte tek seferlik görünmez.
                // Talep kartının içinde de kalıcı olarak saklanır ve gösterilir.
                { "activationCode", activationCode },
                { "ActivationCode", activationCode },

                { "passwordDeliveredInUi", true },
                { "PasswordDeliveredInUi", true },
                { "approvedAt", now },
                { "updatedAt", now },
                { "isActive", false }
            };

            await UpdateMatchingRequests(data, requestUpdate);

            TempData["Success"] = "Şifre talebi onaylandı. Yeni aktivasyon kodu talep kartının içine eklendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(string id, string collectionName)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Şifre talebi bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(collectionName))
            {
                collectionName = "passwordRequests";
            }

            var requestDoc = await _firestore.Collection(collectionName).Document(id).GetSnapshotAsync();

            if (!requestDoc.Exists)
            {
                requestDoc = await FindRequestByIdInBothCollections(id);

                if (!requestDoc.Exists)
                {
                    TempData["Error"] = "Şifre talebi bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }
            }

            var data = requestDoc.ToDictionary();
            var status = NormalizeKey(GetString(data, "status", "Status"));

            if (status == "onaylandi" || status == "approved")
            {
                TempData["Error"] = "Onaylanan talep reddedilemez.";
                return RedirectToAction(nameof(Index));
            }

            var now = Timestamp.GetCurrentTimestamp();

            await UpdateMatchingRequests(
                data,
                new Dictionary<string, object>
                {
                    { "status", "Reddedildi" },
                    { "rejectedAt", now },
                    { "updatedAt", now },
                    { "isActive", false }
                }
            );

            TempData["Success"] = "Şifre talebi reddedildi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id, string collectionName)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Şifre talebi bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            var collections = new[] { "passwordRequests", "password_requests" };

            foreach (var collection in collections)
            {
                try
                {
                    await _firestore.Collection(collection).Document(id).DeleteAsync();
                }
                catch
                {
                }
            }

            TempData["Success"] = "Şifre talebi silindi.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<List<PasswordRequestViewModel>> LoadPasswordRequests()
        {
            var all = new List<PasswordRequestViewModel>();
            var primary = new List<PasswordRequestViewModel>();
            var legacy = new List<PasswordRequestViewModel>();

            await Task.WhenAll(
                AddRequestsFromCollection("passwordRequests", primary),
                AddRequestsFromCollection("password_requests", legacy)
            );

            all.AddRange(primary);
            all.AddRange(legacy);

            var merged = new Dictionary<string, PasswordRequestViewModel>();

            foreach (var item in all)
            {
                var key = BuildRequestKey(item.Role, item.Number, item.UserId, item.RequestKey);

                if (string.IsNullOrWhiteSpace(key))
                {
                    key = item.CollectionName + "_" + item.Id;
                }

                if (!merged.ContainsKey(key))
                {
                    merged[key] = item;
                    continue;
                }

                var existing = merged[key];

                if (IsPending(item.Status) && !IsPending(existing.Status))
                {
                    merged[key] = item;
                    continue;
                }

                if ((item.CreatedAt ?? DateTime.MinValue) > (existing.CreatedAt ?? DateTime.MinValue))
                {
                    merged[key] = item;
                }
            }

            return merged.Values
                .OrderByDescending(x => IsPending(x.Status))
                .ThenByDescending(x => x.CreatedAt ?? DateTime.MinValue)
                .ToList();
        }

        private async Task AddRequestsFromCollection(string collectionName, List<PasswordRequestViewModel> list)
        {
            try
            {
                var snapshot = await _firestore
                    .Collection(collectionName)
                    .GetSnapshotAsync();

                foreach (var doc in snapshot.Documents)
                {
                    var data = doc.ToDictionary();

                    if (IsDeleted(data))
                    {
                        continue;
                    }

                    var role = NormalizeRoleForDisplay(GetString(data, "role", "Role"));

                    var number = OnlyDigits(
                        FirstNonEmpty(
                            GetString(data, "number", "Number"),
                            GetString(data, "schoolNo", "SchoolNo"),
                            GetString(data, "studentNo", "StudentNo")
                        )
                    );

                    var name = FirstNonEmpty(
                        GetString(data, "name", "Name"),
                        GetString(data, "userName", "UserName"),
                        GetString(data, "fullName", "FullName"),
                        "-"
                    );

                    var status = FirstNonEmpty(
                        GetString(data, "status", "Status"),
                        "Bekliyor"
                    );

                    var userId = GetString(data, "userId", "UserId");

                    if (string.IsNullOrWhiteSpace(userId) &&
                        string.IsNullOrWhiteSpace(number) &&
                        string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    list.Add(new PasswordRequestViewModel
                    {
                        Id = doc.Id,
                        CollectionName = collectionName,
                        RequestKey = GetString(data, "requestKey", "RequestKey"),

                        UserId = userId,
                        Name = name,
                        Role = role,
                        Number = number,
                        Note = FirstNonEmpty(
                            GetString(data, "note", "Note"),
                            GetString(data, "message", "Message")
                        ),
                        Status = NormalizeStatusForDisplay(status),
                        ActivationCode = GetString(data, "activationCode", "ActivationCode"),
                        CreatedAt = GetDate(data, "createdAt", "CreatedAt"),
                        ApprovedAt = GetDate(data, "approvedAt", "ApprovedAt"),
                        RejectedAt = GetDate(data, "rejectedAt", "RejectedAt")
                    });
                }
            }
            catch
            {
            }
        }

        private async Task<DocumentSnapshot> FindRequestByIdInBothCollections(string id)
        {
            var collections = new[] { "passwordRequests", "password_requests" };

            foreach (var collection in collections)
            {
                var doc = await _firestore.Collection(collection).Document(id).GetSnapshotAsync();

                if (doc.Exists)
                {
                    return doc;
                }
            }

            return await _firestore.Collection("passwordRequests").Document("__not_found__").GetSnapshotAsync();
        }

        private async Task<DocumentSnapshot?> FindUser(string userId, string role, string number)
        {
            if (!string.IsNullOrWhiteSpace(userId))
            {
                var doc = await _firestore.Collection("users").Document(userId).GetSnapshotAsync();

                if (doc.Exists)
                {
                    return doc;
                }
            }

            var targeted = await FindUserByRoleAndNumberQuery(role, number);

            if (targeted != null)
            {
                return targeted;
            }

            return null;
        }

        private async Task<DocumentSnapshot?> FindUserByRoleAndNumberQuery(string role, string number)
        {
            var roleKey = NormalizeKey(role);
            var numberKey = OnlyDigits(number);

            async Task<QuerySnapshot?> ReadByNumberField(string numberField)
            {
                try
                {
                    return await _firestore
                        .Collection("users")
                        .WhereEqualTo(numberField, numberKey)
                        .Limit(25)
                        .GetSnapshotAsync();
                }
                catch
                {
                    return null;
                }
            }

            var snapshots = await Task.WhenAll(NumberQueryFields().Select(ReadByNumberField));

            foreach (var snapshot in snapshots)
            {
                if (snapshot == null)
                {
                    continue;
                }

                var match = FirstMatchingUser(snapshot, roleKey, numberKey);

                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static DocumentSnapshot? FirstMatchingUser(QuerySnapshot snapshot, string roleKey, string numberKey)
        {
            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();

                var userRole = NormalizeKey(GetString(data, "role", "Role", "userRole", "UserRole"));

                var userNumber = OnlyDigits(
                    FirstNonEmpty(
                        GetString(data, "schoolNo", "SchoolNo"),
                        GetString(data, "number", "Number"),
                        GetString(data, "studentNo", "StudentNo"),
                        GetString(data, "teacherNo", "TeacherNo"),
                        GetString(data, "teacherNumber", "TeacherNumber"),
                        GetString(data, "parentNo", "ParentNo"),
                        GetString(data, "userNumber", "UserNumber")
                    )
                );

                if (userRole == roleKey && userNumber == numberKey)
                {
                    return doc;
                }
            }

            return null;
        }

        private static string[] NumberQueryFields()
        {
            return new[]
            {
                "number",
                "Number",
                "schoolNo",
                "SchoolNo",
                "studentNo",
                "StudentNo",
                "teacherNo",
                "TeacherNo",
                "teacherNumber",
                "TeacherNumber",
                "parentNo",
                "ParentNo",
                "userNumber",
                "UserNumber"
            };
        }

        private static string[] RoleQueryValues(string roleKey)
        {
            var values = new List<string> { roleKey };

            if (roleKey == "ogrenci")
            {
                values.Add("Öğrenci");
                values.Add("Ogrenci");
                values.Add("öğrenci");
                values.Add("ogrenci");
                values.Add("student");
            }
            else if (roleKey == "ogretmen")
            {
                values.Add("Öğretmen");
                values.Add("Ogretmen");
                values.Add("öğretmen");
                values.Add("ogretmen");
                values.Add("teacher");
            }
            else if (roleKey == "veli")
            {
                values.Add("Veli");
                values.Add("veli");
                values.Add("parent");
            }
            else if (roleKey == "admin")
            {
                values.Add("Admin");
                values.Add("admin");
                values.Add("Yönetici");
                values.Add("Yonetici");
            }

            return values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private async Task UpdateMatchingRequests(Dictionary<string, object> originalData, Dictionary<string, object> update)
        {
            var collections = new[] { "passwordRequests", "password_requests" };

            var requestKey = GetString(originalData, "requestKey", "RequestKey");
            var userId = GetString(originalData, "userId", "UserId");
            var role = GetString(originalData, "role", "Role");

            var number = OnlyDigits(
                FirstNonEmpty(
                    GetString(originalData, "number", "Number"),
                    GetString(originalData, "schoolNo", "SchoolNo"),
                    GetString(originalData, "studentNo", "StudentNo"),
                    GetString(originalData, "teacherNo", "TeacherNo"),
                    GetString(originalData, "parentNo", "ParentNo"),
                    GetString(originalData, "userNumber", "UserNumber")
                )
            );

            var refs = new Dictionary<string, DocumentReference>();
            var queries = new List<Task<QuerySnapshot?>>();

            foreach (var collection in collections)
            {
                if (!string.IsNullOrWhiteSpace(requestKey))
                {
                    var directRef = _firestore.Collection(collection).Document(requestKey);
                    refs.TryAdd(directRef.Path, directRef);
                }

                if (!string.IsNullOrWhiteSpace(userId))
                {
                    queries.Add(ReadRequestQuery(collection, "userId", userId));
                    queries.Add(ReadRequestQuery(collection, "UserId", userId));
                }

                foreach (var numberField in NumberQueryFields())
                {
                    queries.Add(ReadRequestQuery(collection, numberField, number));
                }
            }

            var snapshots = await Task.WhenAll(queries);

            foreach (var snapshot in snapshots)
            {
                if (snapshot == null)
                {
                    continue;
                }

                foreach (var doc in snapshot.Documents)
                {
                    var data = doc.ToDictionary();

                    var docRequestKey = GetString(data, "requestKey", "RequestKey");
                    var docUserId = GetString(data, "userId", "UserId");
                    var docRole = GetString(data, "role", "Role");

                    var docNumber = OnlyDigits(
                        FirstNonEmpty(
                            GetString(data, "number", "Number"),
                            GetString(data, "schoolNo", "SchoolNo"),
                            GetString(data, "studentNo", "StudentNo"),
                            GetString(data, "teacherNo", "TeacherNo"),
                            GetString(data, "parentNo", "ParentNo"),
                            GetString(data, "userNumber", "UserNumber")
                        )
                    );

                    var same =
                        (!string.IsNullOrWhiteSpace(requestKey) && docRequestKey == requestKey) ||
                        (!string.IsNullOrWhiteSpace(userId) && docUserId == userId) ||
                        (NormalizeKey(docRole) == NormalizeKey(role) && docNumber == number);

                    if (same)
                    {
                        refs.TryAdd(doc.Reference.Path, doc.Reference);
                    }
                }
            }

            await Task.WhenAll(refs.Values.Select(docRef => docRef.SetAsync(update, SetOptions.MergeAll)));
        }

        private async Task<QuerySnapshot?> ReadRequestQuery(string collection, string field, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            try
            {
                return await _firestore
                    .Collection(collection)
                    .WhereEqualTo(field, value)
                    .Limit(10)
                    .GetSnapshotAsync();
            }
            catch
            {
                return null;
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

            var status = NormalizeKey(GetString(data, "status", "Status"));

            if (status == "silindi" || status == "deleted")
            {
                return true;
            }

            return false;
        }

        private static bool IsPending(string status)
        {
            var value = NormalizeKey(status);

            return value == "bekliyor" ||
                   value == "pending" ||
                   value == "onaybekliyor" ||
                   value == "waiting" ||
                   string.IsNullOrWhiteSpace(value);
        }

        private static string NormalizeStatusForDisplay(string status)
        {
            var value = NormalizeKey(status);

            if (value == "onaylandi" || value == "approved")
            {
                return "Onaylandı";
            }

            if (value == "reddedildi" || value == "rejected" || value == "red")
            {
                return "Reddedildi";
            }

            if (value == "tamamlandi" || value == "completed" || value == "sifredegisti" || value == "passwordchanged")
            {
                return "Tamamlandı";
            }

            return "Bekliyor";
        }

        private static string NormalizeRoleForDisplay(string role)
        {
            var value = NormalizeKey(role);

            if (value == "ogrenci")
            {
                return "Öğrenci";
            }

            if (value == "ogretmen")
            {
                return "Öğretmen";
            }

            if (value == "veli")
            {
                return "Veli";
            }

            if (value == "admin")
            {
                return "Admin";
            }

            return string.IsNullOrWhiteSpace(role) ? "-" : role;
        }

        private static string BuildRequestKey(string role, string number, string userId, string requestKey = "")
        {
            if (!string.IsNullOrWhiteSpace(requestKey))
            {
                return NormalizeKey(requestKey);
            }

            var roleKey = NormalizeKey(role);
            var numberKey = OnlyDigits(number);

            if (!string.IsNullOrWhiteSpace(roleKey) && !string.IsNullOrWhiteSpace(numberKey))
            {
                return $"{roleKey}_{numberKey}";
            }

            if (!string.IsNullOrWhiteSpace(userId))
            {
                return NormalizeKey(userId);
            }

            return "";
        }

        private static string GenerateActivationCode()
        {
            return RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
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

        private static string OnlyDigits(string value)
        {
            return new string((value ?? "").Where(char.IsDigit).ToArray());
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
