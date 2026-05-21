using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using mvc_web.Models;

namespace mvc_web.Controllers
{
    [Route("ForgotPassword")]
    public class ForgotPasswordController : Controller
    {
        private readonly FirestoreDb _firestore;

        public ForgotPasswordController(FirestoreDb firestore)
        {
            _firestore = firestore;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index()
        {
            return View(new ForgotPasswordViewModel
            {
                Role = ""
            });
        }

        [HttpPost("")]
        [HttpPost("Index")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ForgotPasswordViewModel model)
        {
            ModelState.Clear();

            model.Role = model.Role?.Trim() ?? "";
            model.Name = model.Name?.Trim() ?? "";
            model.Number = OnlyDigits(model.Number);
            model.Note = model.Note?.Trim();

            if (string.IsNullOrWhiteSpace(model.Role))
            {
                ModelState.AddModelError("", "Rol boş bırakılamaz.");
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                ModelState.AddModelError("", "Ad Soyad boş bırakılamaz.");
            }

            if (string.IsNullOrWhiteSpace(model.Number))
            {
                ModelState.AddModelError("", "Numara boş bırakılamaz.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userDoc = await FindMatchingUser(model.Role, model.Name, model.Number);

            if (userDoc == null)
            {
                ModelState.AddModelError("", "Ad Soyad, rol ve numara birbiriyle uyuşmuyor. Lütfen bilgilerinizi kontrol edin.");
                return View(model);
            }

            var userData = userDoc.ToDictionary();

            var realName = FirstNonEmpty(
                GetString(userData, "name", "Name"),
                model.Name
            );

            var normalizedRole = NormalizeRoleForSave(model.Role);
            var requestKey = BuildRequestKey(normalizedRole, model.Number, userDoc.Id);

            var alreadyHasPendingRequest = await HasPendingRequest(normalizedRole, model.Number, userDoc.Id);

            if (alreadyHasPendingRequest)
            {
                ModelState.AddModelError("", "Bu kullanıcı için zaten bekleyen bir şifre talebi var. Admin onayladıktan sonra tekrar deneyebilirsiniz.");
                return View(model);
            }

            var now = Timestamp.GetCurrentTimestamp();

            var requestData = new Dictionary<string, object>
            {
                { "requestKey", requestKey },
                { "userId", userDoc.Id },

                { "name", realName },
                { "userName", realName },
                { "fullName", realName },

                { "role", normalizedRole },
                { "number", model.Number },
                { "schoolNo", model.Number },
                { "studentNo", model.Number },
                { "teacherNo", model.Number },
                { "parentNo", model.Number },
                { "userNumber", model.Number },

                { "note", model.Note ?? "" },
                { "message", model.Note ?? "" },

                { "status", "Bekliyor" },
                { "createdAt", now },
                { "updatedAt", now },

                { "isDeleted", false },
                { "isActive", true }
            };

            /*
             * Kritik:
             * Bazı eski kodlar passwordRequests okuyor,
             * bazı eski kodlar password_requests okuyor.
             * O yüzden talebi iki yere de aynı requestKey ile yazıyoruz.
             * Admin paneli de bunları tek kayıt gibi gösterecek.
             */

            await Task.WhenAll(
                _firestore
                    .Collection("passwordRequests")
                    .Document(requestKey)
                    .SetAsync(requestData, SetOptions.MergeAll),
                _firestore
                    .Collection("password_requests")
                    .Document(requestKey)
                    .SetAsync(requestData, SetOptions.MergeAll)
            );

            TempData["Success"] = "Şifre talebiniz admin paneline gönderildi.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<DocumentSnapshot?> FindMatchingUser(string role, string name, string number)
        {
            var searchedRole = NormalizeKey(role);
            var searchedName = NormalizeKey(name);
            var searchedNumber = OnlyDigits(number);

            return await FindMatchingUserByQuery(searchedRole, searchedName, searchedNumber);
        }

        private async Task<DocumentSnapshot?> FindMatchingUserByQuery(string searchedRole, string searchedName, string searchedNumber)
        {
            var numberFields = NumberQueryFields();

            async Task<QuerySnapshot?> ReadByNumberField(string numberField)
            {
                try
                {
                    return await _firestore
                        .Collection("users")
                        .WhereEqualTo(numberField, searchedNumber)
                        .Limit(25)
                        .GetSnapshotAsync();
                }
                catch
                {
                    return null;
                }
            }

            var snapshots = await Task.WhenAll(numberFields.Select(ReadByNumberField));

            foreach (var snapshot in snapshots)
            {
                if (snapshot == null)
                {
                    continue;
                }

                var match = FirstMatchingUser(snapshot, searchedRole, searchedName, searchedNumber);

                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static DocumentSnapshot? FirstMatchingUser(QuerySnapshot snapshot, string searchedRole, string searchedName, string searchedNumber)
        {
            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();

                if (IsDeletedUser(data))
                {
                    continue;
                }

                var userRole = NormalizeKey(GetString(data, "role", "Role", "userRole", "UserRole"));
                var userName = NormalizeKey(GetString(data, "name", "Name", "fullName", "FullName", "userName", "UserName"));
                var userNumber = OnlyDigits(
                    FirstNonEmpty(
                        GetString(data, "schoolNo", "SchoolNo"),
                        GetString(data, "number", "Number"),
                        GetString(data, "studentNo", "StudentNo"),
                        GetString(data, "teacherNo", "TeacherNo")
                    )
                );

                if (userRole == searchedRole &&
                    userName == searchedName &&
                    userNumber == searchedNumber)
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

        private static string[] RoleQueryValues(string searchedRole)
        {
            var values = new List<string> { searchedRole };

            if (searchedRole == "ogrenci")
            {
                values.Add("Ã–ÄŸrenci");
            }
            else if (searchedRole == "ogretmen")
            {
                values.Add("Ã–ÄŸretmen");
            }
            else if (searchedRole == "veli")
            {
                values.Add("Veli");
            }
            else if (searchedRole == "admin")
            {
                values.Add("Admin");
            }

            return values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private async Task<bool> HasPendingRequest(string role, string number, string userId)
        {
            var collections = new[] { "passwordRequests", "password_requests" };
            var roleKey = NormalizeKey(role);
            var numberKey = OnlyDigits(number);
            var requestKey = BuildRequestKey(roleKey, numberKey, userId);

            async Task<DocumentSnapshot?> ReadDirectRequest(string collection)
            {
                try
                {
                    return await _firestore
                        .Collection(collection)
                        .Document(requestKey)
                        .GetSnapshotAsync();
                }
                catch
                {
                    return null;
                }
            }

            var directDocs = await Task.WhenAll(collections.Select(ReadDirectRequest));

            if (directDocs.Any(doc => RequestMatchesPendingUser(doc, roleKey, numberKey, userId)))
            {
                return true;
            }

            var queries = new List<Task<QuerySnapshot?>>();

            foreach (var collection in collections)
            {
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    queries.Add(ReadPendingQuery(collection, "userId", userId));
                    queries.Add(ReadPendingQuery(collection, "UserId", userId));
                }

                foreach (var numberField in NumberQueryFields())
                {
                    queries.Add(ReadPendingQuery(collection, numberField, numberKey));
                }
            }

            var snapshots = await Task.WhenAll(queries);

            return snapshots
                .Where(snapshot => snapshot != null)
                .SelectMany(snapshot => snapshot!.Documents)
                .Any(doc => RequestMatchesPendingUser(doc, roleKey, numberKey, userId));
        }

        private async Task<QuerySnapshot?> ReadPendingQuery(string collection, string field, string value)
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

        private static bool RequestMatchesPendingUser(
            DocumentSnapshot? doc,
            string roleKey,
            string numberKey,
            string userId)
        {
            if (doc == null || !doc.Exists)
            {
                return false;
            }

            var data = doc.ToDictionary();

            if (IsDeletedRequest(data))
            {
                return false;
            }

            var requestRole = NormalizeKey(GetString(data, "role", "Role"));
            var requestNumber = OnlyDigits(
                FirstNonEmpty(
                    GetString(data, "number", "Number"),
                    GetString(data, "schoolNo", "SchoolNo"),
                    GetString(data, "studentNo", "StudentNo"),
                    GetString(data, "teacherNo", "TeacherNo"),
                    GetString(data, "parentNo", "ParentNo"),
                    GetString(data, "userNumber", "UserNumber")
                )
            );
            var requestUserId = GetString(data, "userId", "UserId");
            var status = NormalizeKey(GetString(data, "status", "Status"));

            var isPending =
                status == "bekliyor" ||
                status == "pending" ||
                status == "onaybekliyor" ||
                status == "waiting" ||
                string.IsNullOrWhiteSpace(status);

            var sameUser =
                (!string.IsNullOrWhiteSpace(userId) && requestUserId == userId) ||
                (requestRole == roleKey && requestNumber == numberKey);

            return sameUser && isPending;
        }

        private static bool IsDeletedUser(Dictionary<string, object> data)
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

            var status = NormalizeKey(GetString(data, "status", "Status"));

            if (status == "silindi" ||
                status == "deleted" ||
                status == "pasif" ||
                status == "inactive" ||
                status == "arsivlendi" ||
                status == "archived")
            {
                return true;
            }

            return false;
        }

        private static bool IsDeletedRequest(Dictionary<string, object> data)
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

            if (status == "silindi" ||
                status == "deleted" ||
                status == "iptal" ||
                status == "cancelled" ||
                status == "canceled" ||
                status == "reddedildi" ||
                status == "rejected" ||
                status == "red" ||
                status == "onaylandi" ||
                status == "approved" ||
                status == "tamamlandi" ||
                status == "completed" ||
                status == "sifredegisti" ||
                status == "passwordchanged")
            {
                return true;
            }

            return false;
        }

        private static string BuildRequestKey(string role, string number, string userId)
        {
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

            return Guid.NewGuid().ToString("N");
        }

        private static string NormalizeRoleForSave(string role)
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
