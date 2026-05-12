using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using mvc_web.Models;
using mvc_web.Services;

namespace mvc_web.Controllers
{
    public class PasswordRequestsController : Controller
    {
        private readonly FirestoreDb _firestore;
        private readonly SessionService _session;

        public PasswordRequestsController(
            FirestoreDb firestore,
            SessionService session
        )
        {
            _firestore = firestore;
            _session = session;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!_session.IsAdmin(HttpContext))
            {
                return RedirectToAction("Login", "Auth");
            }

            var requests = await LoadPasswordRequests();

            return View(requests);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(string id, string collectionName)
        {
            if (!_session.IsAdmin(HttpContext))
            {
                return RedirectToAction("Login", "Auth");
            }

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
            var now = Timestamp.GetCurrentTimestamp();

            await userDoc.Reference.SetAsync(
                new Dictionary<string, object>
                {
                    { "activationCode", activationCode },
                    { "mustChangePassword", true },
                    { "password", "" },
                    { "updatedAt", now },
                    { "passwordResetAt", now }
                },
                SetOptions.MergeAll
            );

            var requestUpdate = new Dictionary<string, object>
            {
                { "status", "Onaylandı" },
                { "activationCode", activationCode },
                { "approvedAt", now },
                { "updatedAt", now },
                { "isActive", false }
            };

            await UpdateMatchingRequests(data, requestUpdate);

            TempData["Success"] = $"Şifre talebi onaylandı. Yeni aktivasyon kodu: {activationCode}";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(string id, string collectionName)
        {
            if (!_session.IsAdmin(HttpContext))
            {
                return RedirectToAction("Login", "Auth");
            }

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
            if (!_session.IsAdmin(HttpContext))
            {
                return RedirectToAction("Login", "Auth");
            }

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

            await AddRequestsFromCollection("passwordRequests", all);
            await AddRequestsFromCollection("password_requests", all);

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

            var snapshot = await _firestore.Collection("users").GetSnapshotAsync();

            var roleKey = NormalizeKey(role);
            var numberKey = OnlyDigits(number);

            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();

                var userRole = NormalizeKey(GetString(data, "role", "Role"));

                var userNumber = OnlyDigits(
                    FirstNonEmpty(
                        GetString(data, "schoolNo", "SchoolNo"),
                        GetString(data, "number", "Number")
                    )
                );

                if (userRole == roleKey && userNumber == numberKey)
                {
                    return doc;
                }
            }

            return null;
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
                    GetString(originalData, "studentNo", "StudentNo")
                )
            );

            foreach (var collection in collections)
            {
                try
                {
                    var snapshot = await _firestore.Collection(collection).GetSnapshotAsync();

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
                                GetString(data, "studentNo", "StudentNo")
                            )
                        );

                        var same =
                            (!string.IsNullOrWhiteSpace(requestKey) && docRequestKey == requestKey) ||
                            (!string.IsNullOrWhiteSpace(userId) && docUserId == userId) ||
                            (NormalizeKey(docRole) == NormalizeKey(role) && docNumber == number);

                        if (same)
                        {
                            await doc.Reference.SetAsync(update, SetOptions.MergeAll);
                        }
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

            return role;
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
            return Random.Shared.Next(100000, 999999).ToString();
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