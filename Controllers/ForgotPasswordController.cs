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

            await _firestore
                .Collection("passwordRequests")
                .Document(requestKey)
                .SetAsync(requestData, SetOptions.MergeAll);

            await _firestore
                .Collection("password_requests")
                .Document(requestKey)
                .SetAsync(requestData, SetOptions.MergeAll);

            TempData["Success"] = "Şifre talebiniz admin paneline gönderildi.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<DocumentSnapshot?> FindMatchingUser(string role, string name, string number)
        {
            var searchedRole = NormalizeKey(role);
            var searchedName = NormalizeKey(name);
            var searchedNumber = OnlyDigits(number);

            var targeted = await FindMatchingUserByQuery(searchedRole, searchedName, searchedNumber);

            if (targeted != null)
            {
                return targeted;
            }

            var snapshot = await _firestore
                .Collection("users")
                .GetSnapshotAsync();

            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();

                if (IsDeletedUser(data))
                {
                    continue;
                }

                var userRole = NormalizeKey(GetString(data, "role", "Role"));

                var userName = NormalizeKey(
                    GetString(data, "name", "Name")
                );

                var userNumber = OnlyDigits(
                    FirstNonEmpty(
                        GetString(data, "schoolNo", "SchoolNo"),
                        GetString(data, "number", "Number")
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

        private async Task<DocumentSnapshot?> FindMatchingUserByQuery(string searchedRole, string searchedName, string searchedNumber)
        {
            var roleFields = new[] { "role", "Role", "userRole", "UserRole" };
            var numberFields = new[] { "number", "Number", "schoolNo", "SchoolNo", "studentNo", "StudentNo", "teacherNo", "TeacherNo" };
            var roleValues = RoleQueryValues(searchedRole);

            foreach (var roleField in roleFields)
            {
                foreach (var roleValue in roleValues)
                {
                    foreach (var numberField in numberFields)
                    {
                        try
                        {
                            var snapshot = await _firestore
                                .Collection("users")
                                .WhereEqualTo(roleField, roleValue)
                                .WhereEqualTo(numberField, searchedNumber)
                                .Limit(10)
                                .GetSnapshotAsync();

                            var match = FirstMatchingUser(snapshot, searchedRole, searchedName, searchedNumber);

                            if (match != null)
                            {
                                return match;
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }

            foreach (var numberField in numberFields)
            {
                try
                {
                    var snapshot = await _firestore
                        .Collection("users")
                        .WhereEqualTo(numberField, searchedNumber)
                        .Limit(25)
                        .GetSnapshotAsync();

                    var match = FirstMatchingUser(snapshot, searchedRole, searchedName, searchedNumber);

                    if (match != null)
                    {
                        return match;
                    }
                }
                catch
                {
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

            foreach (var collection in collections)
            {
                try
                {
                    var snapshot = await _firestore
                        .Collection(collection)
                        .GetSnapshotAsync();

                    foreach (var doc in snapshot.Documents)
                    {
                        var data = doc.ToDictionary();

                        if (IsDeletedRequest(data))
                        {
                            continue;
                        }

                        var requestRole = NormalizeKey(GetString(data, "role", "Role"));

                        var requestNumber = OnlyDigits(
                            FirstNonEmpty(
                                GetString(data, "number", "Number"),
                                GetString(data, "schoolNo", "SchoolNo"),
                                GetString(data, "studentNo", "StudentNo")
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
                            (requestRole == NormalizeKey(role) && requestNumber == OnlyDigits(number));

                        if (sameUser && isPending)
                        {
                            return true;
                        }
                    }
                }
                catch
                {
                }
            }

            return false;
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
