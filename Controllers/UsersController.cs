using ClosedXML.Excel;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using mvc_web.Models;
using System.Text.RegularExpressions;

namespace mvc_web.Controllers
{
    public class UsersController : Controller
    {
        private readonly FirestoreDb _firestore;

        public UsersController(FirestoreDb firestore)
        {
            _firestore = firestore;
        }

        public async Task<IActionResult> Index(string? search)
        {
            var snapshot = await _firestore
                .Collection("users")
                .OrderByDescending("createdAt")
                .GetSnapshotAsync();

            var users = new List<UserViewModel>();

            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();

                var user = new UserViewModel
                {
                    Id = doc.Id,
                    Name = GetString(data, "name", "Name"),
                    Tc = GetString(data, "tc", "Tc", "TC"),
                    SchoolNo = GetString(data, "schoolNo", "SchoolNo", "number", "Number"),
                    Phone = NormalizePhone(GetString(data, "phone", "Phone")),
                    Role = GetString(data, "role", "Role"),
                    ClassName = NormalizeClassName(GetString(data, "className", "ClassName", "class", "Class")),
                    LinkedStudentNo = GetString(data, "linkedStudentNo", "LinkedStudentNo"),
                    Branch = GetString(data, "branch", "Branch"),
                    ActivationCode = GetString(data, "activationCode", "ActivationCode"),
                    MustChangePassword = GetBool(data, "mustChangePassword", "MustChangePassword"),
                    CreatedAt = GetDate(data, "createdAt", "CreatedAt")
                };

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = OnlyDigits(search);

                    if (!OnlyDigits(user.SchoolNo).StartsWith(s))
                    {
                        continue;
                    }
                }

                users.Add(user);
            }

            ViewBag.Search = search ?? "";
            return View(users);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new UserViewModel
            {
                Role = "Öğrenci",
                ClassName = "9-A"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserViewModel model)
        {
            ModelState.Clear();

            NormalizeModel(model);
            ValidateUser(model);

            if (await NumberExistsAsync(model.SchoolNo))
            {
                ModelState.AddModelError(nameof(model.SchoolNo), "Bu numara zaten kayıtlı. Lütfen farklı bir numara girin.");
            }

            if (!ModelState.IsValid)
            {
                model.Phone = FormatPhoneForView(model.Phone);
                return View(model);
            }

            var activationCode = GenerateActivationCode();
            var now = Timestamp.GetCurrentTimestamp();

            await _firestore.Collection("users").AddAsync(new Dictionary<string, object>
            {
                { "name", model.Name },
                { "tc", model.Tc },
                { "schoolNo", model.SchoolNo },
                { "number", model.SchoolNo },
                { "phone", model.Phone },
                { "role", model.Role },
                { "className", model.Role == "Öğrenci" ? model.ClassName : "" },
                { "linkedStudentNo", model.Role == "Veli" ? model.LinkedStudentNo : "" },
                { "branch", model.Role == "Öğretmen" ? model.Branch : "" },
                { "activationCode", activationCode },
                { "mustChangePassword", true },
                { "password", "" },
                { "createdAt", now },
                { "updatedAt", now }
            });

            TempData["Success"] = $"Kullanıcı eklendi. Aktivasyon kodu: {activationCode}";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Kullanıcı bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            var doc = await _firestore.Collection("users").Document(id).GetSnapshotAsync();

            if (!doc.Exists)
            {
                TempData["Error"] = "Kullanıcı bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            var data = doc.ToDictionary();

            var model = new UserViewModel
            {
                Id = doc.Id,
                Name = GetString(data, "name", "Name"),
                Tc = GetString(data, "tc", "Tc", "TC"),
                SchoolNo = GetString(data, "schoolNo", "SchoolNo", "number", "Number"),
                Phone = FormatPhoneForView(GetString(data, "phone", "Phone")),
                Role = GetString(data, "role", "Role"),
                ClassName = NormalizeClassName(GetString(data, "className", "ClassName", "class", "Class")),
                LinkedStudentNo = GetString(data, "linkedStudentNo", "LinkedStudentNo"),
                Branch = GetString(data, "branch", "Branch"),
                ActivationCode = GetString(data, "activationCode", "ActivationCode"),
                MustChangePassword = GetBool(data, "mustChangePassword", "MustChangePassword")
            };

            if (string.IsNullOrWhiteSpace(model.ClassName))
            {
                model.ClassName = "9-A";
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UserViewModel model)
        {
            ModelState.Clear();

            if (string.IsNullOrWhiteSpace(model.Id))
            {
                TempData["Error"] = "Kullanıcı bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            NormalizeModel(model);
            ValidateUser(model);

            if (await NumberExistsAsync(model.SchoolNo, model.Id))
            {
                ModelState.AddModelError(nameof(model.SchoolNo), "Bu numara başka bir kullanıcıda kayıtlı. Lütfen farklı bir numara girin.");
            }

            if (!ModelState.IsValid)
            {
                model.Phone = FormatPhoneForView(model.Phone);
                return View(model);
            }

            await _firestore.Collection("users").Document(model.Id).UpdateAsync(new Dictionary<string, object>
            {
                { "name", model.Name },
                { "tc", model.Tc },
                { "schoolNo", model.SchoolNo },
                { "number", model.SchoolNo },
                { "phone", model.Phone },
                { "role", model.Role },
                { "className", model.Role == "Öğrenci" ? model.ClassName : "" },
                { "linkedStudentNo", model.Role == "Veli" ? model.LinkedStudentNo : "" },
                { "branch", model.Role == "Öğretmen" ? model.Branch : "" },
                { "updatedAt", Timestamp.GetCurrentTimestamp() }
            });

            TempData["Success"] = "Kullanıcı güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Kullanıcı bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            await _firestore.Collection("users").Document(id).DeleteAsync();

            TempData["Success"] = "Kullanıcı silindi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportStudentsExcel(IFormFile excelFile)
        {
            return await ImportUsersExcel(excelFile, "Öğrenci", "öğrenci");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportTeachersExcel(IFormFile excelFile)
        {
            return await ImportUsersExcel(excelFile, "Öğretmen", "öğretmen");
        }

        private async Task<IActionResult> ImportUsersExcel(IFormFile excelFile, string role, string successName)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["Error"] = "Excel dosyası seçilmedi.";
                return RedirectToAction(nameof(Index));
            }

            if (Path.GetExtension(excelFile.FileName).ToLowerInvariant() != ".xlsx")
            {
                TempData["Error"] = "Sadece gerçek .xlsx Excel dosyası yükleyebilirsiniz.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                using var stream = new MemoryStream();
                await excelFile.CopyToAsync(stream);
                stream.Position = 0;

                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheets.FirstOrDefault();

                if (worksheet == null || worksheet.RangeUsed() == null)
                {
                    TempData["Error"] = "Excel dosyası boş.";
                    return RedirectToAction(nameof(Index));
                }

                var rows = worksheet.RangeUsed()!.RowsUsed().ToList();
                var startIndex = LooksLikeHeader(rows.First()) ? 1 : 0;

                var importedNumbers = new HashSet<string>();

                int addedCount = 0;
                int duplicateCount = 0;
                int emptyCount = 0;
                int fixedClassCount = 0;

                var batch = _firestore.StartBatch();
                int batchCounter = 0;

                for (int i = startIndex; i < rows.Count; i++)
                {
                    var row = rows[i];

                    var name = CleanText(GetCell(row, 1));
                    var tc = OnlyDigits(GetCell(row, 2));
                    var number = OnlyDigits(GetCell(row, 3));
                    var fourth = CleanText(GetCell(row, 4));
                    var phone = NormalizePhone(GetCell(row, 5));

                    var hasAnyValue =
                        !string.IsNullOrWhiteSpace(name) ||
                        !string.IsNullOrWhiteSpace(tc) ||
                        !string.IsNullOrWhiteSpace(number) ||
                        !string.IsNullOrWhiteSpace(fourth) ||
                        !string.IsNullOrWhiteSpace(phone);

                    if (!hasAnyValue)
                    {
                        emptyCount++;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(number))
                    {
                        number = GenerateFallbackNumber();
                    }

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        name = $"{role} {number}";
                    }

                    if (await NumberExistsAsync(number) || importedNumbers.Contains(number))
                    {
                        duplicateCount++;
                        continue;
                    }

                    importedNumbers.Add(number);

                    var className = "";
                    var branch = "";

                    if (role == "Öğrenci")
                    {
                        className = NormalizeClassName(fourth);

                        if (!string.IsNullOrWhiteSpace(fourth) && !string.IsNullOrWhiteSpace(className))
                        {
                            fixedClassCount++;
                        }
                    }

                    if (role == "Öğretmen")
                    {
                        branch = string.IsNullOrWhiteSpace(fourth) ? "Genel" : fourth;
                    }

                    var now = Timestamp.GetCurrentTimestamp();
                    var activationCode = GenerateActivationCode();
                    var docRef = _firestore.Collection("users").Document();

                    batch.Set(docRef, new Dictionary<string, object>
                    {
                        { "name", name },
                        { "tc", tc },
                        { "schoolNo", number },
                        { "number", number },
                        { "phone", phone },
                        { "className", className },
                        { "linkedStudentNo", "" },
                        { "branch", branch },
                        { "role", role },
                        { "activationCode", activationCode },
                        { "mustChangePassword", true },
                        { "password", "" },
                        { "createdAt", now },
                        { "updatedAt", now }
                    });

                    addedCount++;
                    batchCounter++;

                    if (batchCounter >= 450)
                    {
                        await batch.CommitAsync();
                        batch = _firestore.StartBatch();
                        batchCounter = 0;
                    }
                }

                if (batchCounter > 0)
                {
                    await batch.CommitAsync();
                }

                if (addedCount == 0)
                {
                    TempData["Error"] = $"Hiç {successName} eklenmedi. Dosya boş olabilir veya kayıtlar zaten mevcut olabilir.";
                    return RedirectToAction(nameof(Index));
                }

                TempData["Success"] =
                    $"{addedCount} {successName} Excel’den aktarıldı. " +
                    $"{duplicateCount} kayıt zaten var diye atlandı. " +
                    $"{emptyCount} boş satır atlandı.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Excel aktarımı sırasında hata oluştu: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        private void NormalizeModel(UserViewModel model)
        {
            model.Name = CleanText(model.Name);
            model.Tc = OnlyDigits(model.Tc);
            model.SchoolNo = OnlyDigits(model.SchoolNo);
            model.Phone = NormalizePhone(model.Phone);
            model.Role = CleanText(model.Role);
            model.ClassName = model.Role == "Öğrenci" ? NormalizeClassName(model.ClassName) : "";
            model.LinkedStudentNo = OnlyDigits(model.LinkedStudentNo);
            model.Branch = CleanText(model.Branch);
        }

        private void ValidateUser(UserViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Role))
            {
                ModelState.AddModelError(nameof(model.Role), "Rol seçilmelidir.");
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                ModelState.AddModelError(nameof(model.Name), "Ad soyad boş bırakılamaz.");
            }

            if (string.IsNullOrWhiteSpace(model.SchoolNo))
            {
                ModelState.AddModelError(nameof(model.SchoolNo), "Numara boş bırakılamaz.");
            }

            if (!string.IsNullOrWhiteSpace(model.Tc) && model.Tc.Length != 11)
            {
                ModelState.AddModelError(nameof(model.Tc), "T.C. kimlik numarası 11 haneli olmalıdır.");
            }

            if (!string.IsNullOrWhiteSpace(model.Phone) && (model.Phone.Length != 11 || !model.Phone.StartsWith("0")))
            {
                ModelState.AddModelError(nameof(model.Phone), "Telefon numarası 0 ile başlamalı ve 11 haneli olmalıdır. Örn: 0 555 123 45 67");
            }

            if (model.Role == "Öğrenci" && string.IsNullOrWhiteSpace(model.ClassName))
            {
                ModelState.AddModelError(nameof(model.ClassName), "Öğrenci için sınıf seçilmelidir.");
            }

            if (model.Role == "Öğretmen" && string.IsNullOrWhiteSpace(model.Branch))
            {
                ModelState.AddModelError(nameof(model.Branch), "Öğretmen için branş girilmelidir.");
            }

            if (model.Role == "Veli" && string.IsNullOrWhiteSpace(model.LinkedStudentNo))
            {
                ModelState.AddModelError(nameof(model.LinkedStudentNo), "Veli için bağlı öğrenci numarası girilmelidir.");
            }
        }

        private async Task<bool> NumberExistsAsync(string number, string? excludedUserId = null)
        {
            var cleanNumber = OnlyDigits(number);

            if (string.IsNullOrWhiteSpace(cleanNumber))
            {
                return false;
            }

            var snapshot = await _firestore.Collection("users").GetSnapshotAsync();

            foreach (var doc in snapshot.Documents)
            {
                if (!string.IsNullOrWhiteSpace(excludedUserId) && doc.Id == excludedUserId)
                {
                    continue;
                }

                var data = doc.ToDictionary();

                var existingNumber = OnlyDigits(
                    GetString(data, "schoolNo", "SchoolNo", "number", "Number")
                );

                if (existingNumber == cleanNumber)
                {
                    return true;
                }
            }

            return false;
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

        private static string GenerateActivationCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var random = new Random();

            return new string(
                Enumerable
                    .Repeat(chars, 6)
                    .Select(s => s[random.Next(s.Length)])
                    .ToArray()
            );
        }

        private static string GenerateFallbackNumber()
        {
            var random = new Random();
            var raw = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{random.Next(1000, 9999)}";
            return raw.Length > 10 ? raw[^10..] : raw.PadLeft(10, '0');
        }

        private static string GetCell(IXLRangeRow row, int cellNumber)
        {
            try
            {
                return row.Cell(cellNumber).GetFormattedString().Trim();
            }
            catch
            {
                return "";
            }
        }

        private static bool LooksLikeHeader(IXLRangeRow row)
        {
            var joined = string.Join(" ", Enumerable.Range(1, 5).Select(i => Normalize(GetCell(row, i))));

            return joined.Contains("ad") ||
                   joined.Contains("soyad") ||
                   joined.Contains("tc") ||
                   joined.Contains("kimlik") ||
                   joined.Contains("numara") ||
                   joined.Contains("okulno") ||
                   joined.Contains("sinif") ||
                   joined.Contains("sube") ||
                   joined.Contains("telefon") ||
                   joined.Contains("brans");
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

                    if (bool.TryParse(value.ToString(), out var parsed))
                    {
                        return parsed;
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

        private static string NormalizePhone(string value)
        {
            var digits = OnlyDigits(value);

            if (string.IsNullOrWhiteSpace(digits))
            {
                return "";
            }

            if (digits.Length == 10 && !digits.StartsWith("0"))
            {
                digits = "0" + digits;
            }

            if (digits.Length > 11)
            {
                digits = digits[..11];
            }

            return digits;
        }

        private static string FormatPhoneForView(string value)
        {
            var digits = NormalizePhone(value);

            if (string.IsNullOrWhiteSpace(digits))
            {
                return "";
            }

            var p0 = digits[..1];
            var p1 = digits.Length > 1 ? digits.Substring(1, Math.Min(3, digits.Length - 1)) : "";
            var p2 = digits.Length > 4 ? digits.Substring(4, Math.Min(3, digits.Length - 4)) : "";
            var p3 = digits.Length > 7 ? digits.Substring(7, Math.Min(2, digits.Length - 7)) : "";
            var p4 = digits.Length > 9 ? digits.Substring(9, Math.Min(2, digits.Length - 9)) : "";

            return string.Join(" ", new[] { p0, p1, p2, p3, p4 }.Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        private static string CleanText(string value)
        {
            return (value ?? "")
                .Replace("\u00A0", " ")
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
        }

        private static string Normalize(string value)
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
                .Replace(".", "")
                .Replace("-", "")
                .Replace("_", "");
        }
    }
}