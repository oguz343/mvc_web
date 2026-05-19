using Google.Cloud.Firestore;

namespace mvc_web.Services;

public class AuthLoginService
{
    private readonly FirestoreDb _firestore;
    private readonly IWebHostEnvironment _environment;

    public AuthLoginService(FirestoreDb firestore, IWebHostEnvironment environment)
    {
        _firestore = firestore;
        _environment = environment;
    }

    public async Task<AuthLoginResult> ValidateAsync(string role, string number, string password)
    {
        var wantedRoleKey = NormalizeRole(role);
        var numberKey = OnlyDigits(number);
        password ??= "";

        var user = await FindUserAsync(wantedRoleKey, numberKey);

        if (user == null)
        {
            return AuthLoginResult.Failed(AuthLoginFailure.UserNotFound);
        }

        var data = user.ToDictionary();

        if (IsDeleted(data))
        {
            return AuthLoginResult.Failed(AuthLoginFailure.Deleted);
        }

        var passwordHash = GetString(data, "passwordHash", "PasswordHash", "hash", "Hash");
        var legacyPassword = GetString(data, "password", "Password");
        var activationCode = GetString(data, "activationCode", "ActivationCode");
        var mustChangePassword = GetBool(data, "mustChangePassword", "MustChangePassword", "forcePasswordChange", "ForcePasswordChange");

        var passwordOk = false;
        var shouldMigrateToHash = false;

        if (mustChangePassword && PasswordHashService.IsHash(passwordHash))
        {
            passwordOk = PasswordHashService.VerifyPassword(password, passwordHash);
        }
        else if (mustChangePassword && !string.IsNullOrWhiteSpace(activationCode))
        {
            passwordOk = activationCode == password;
        }
        else if (PasswordHashService.IsHash(passwordHash))
        {
            passwordOk = PasswordHashService.VerifyPassword(password, passwordHash);
        }
        else if (!string.IsNullOrWhiteSpace(legacyPassword))
        {
            passwordOk = legacyPassword == password;

            if (passwordOk)
            {
                shouldMigrateToHash = true;
            }
        }

        if (!passwordOk && wantedRoleKey == "admin" && numberKey == "0000")
        {
            var systemAdminConfigured = await SystemAdminAccountHasPasswordAsync();
            passwordOk = await VerifySystemAdminPasswordAsync(password);
            shouldMigrateToHash = passwordOk;

            if (!passwordOk && !IsDevelopment() && !systemAdminConfigured)
            {
                return AuthLoginResult.Failed(AuthLoginFailure.SystemAdminNotConfigured);
            }
        }

        if (!passwordOk)
        {
            return AuthLoginResult.Failed(AuthLoginFailure.InvalidPassword);
        }

        var cleanRole = NormalizeRoleToDisplay(wantedRoleKey);
        var cleanName = FirstNonEmpty(
            GetString(data, "name", "Name"),
            GetString(data, "fullName", "FullName"),
            GetString(data, "userName", "UserName"),
            cleanRole
        );

        var cleanBranch = FirstNonEmpty(
            GetString(data, "branch", "Branch"),
            GetString(data, "teacherBranch", "TeacherBranch")
        );

        var cleanClass = FirstNonEmpty(
            GetString(data, "className", "ClassName"),
            GetString(data, "class", "Class"),
            GetString(data, "studentClass", "StudentClass")
        );

        var cleanNumber = OnlyDigits(FirstNonEmpty(
            GetString(data, "number", "Number"),
            GetString(data, "schoolNo", "SchoolNo"),
            GetString(data, "studentNo", "StudentNo"),
            GetString(data, "teacherNo", "TeacherNo"),
            numberKey
        ));

        if (shouldMigrateToHash)
        {
            var newHash = PasswordHashService.HashPassword(password);
            var now = Timestamp.FromDateTime(DateTime.UtcNow);

            await user.Reference.SetAsync(
                new Dictionary<string, object?>
                {
                    ["passwordHash"] = newHash,
                    ["PasswordHash"] = newHash,
                    ["password"] = "",
                    ["Password"] = "",
                    ["updatedAt"] = now,
                    ["UpdatedAt"] = now,
                },
                SetOptions.MergeAll
            );
        }

        return AuthLoginResult.Success(
            user.Id,
            wantedRoleKey,
            cleanRole,
            cleanNumber,
            cleanName,
            cleanBranch,
            cleanClass,
            mustChangePassword
        );
    }

    private async Task<DocumentSnapshot?> FindUserAsync(string roleKey, string number)
    {
        var targeted = await FindUserByRoleAndNumberQueryAsync(roleKey, number);

        if (targeted != null)
        {
            return targeted;
        }

        var snapshot = await _firestore.Collection("users").GetSnapshotAsync();

        foreach (var doc in snapshot.Documents)
        {
            var data = doc.ToDictionary();

            if (IsDeleted(data))
            {
                continue;
            }

            var currentRole = NormalizeRole(FirstNonEmpty(
                GetString(data, "role", "Role"),
                GetString(data, "userRole", "UserRole")
            ));

            var currentNumber = OnlyDigits(FirstNonEmpty(
                GetString(data, "number", "Number"),
                GetString(data, "schoolNo", "SchoolNo"),
                GetString(data, "studentNo", "StudentNo"),
                GetString(data, "teacherNo", "TeacherNo"),
                GetString(data, "parentNo", "ParentNo"),
                GetString(data, "adminNo", "AdminNo")
            ));

            if (currentRole == roleKey && currentNumber == number)
            {
                return doc;
            }
        }

        return null;
    }

    private async Task<DocumentSnapshot?> FindUserByRoleAndNumberQueryAsync(string roleKey, string number)
    {
        var roleFields = new[] { "role", "Role", "userRole", "UserRole" };
        var numberFields = new[] { "number", "Number", "schoolNo", "SchoolNo", "studentNo", "StudentNo", "teacherNo", "TeacherNo", "parentNo", "ParentNo", "adminNo", "AdminNo" };
        var roleValues = RoleQueryValues(roleKey);
        var numberKey = OnlyDigits(number);

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
                            .WhereEqualTo(numberField, numberKey)
                            .Limit(10)
                            .GetSnapshotAsync();

                        var match = FirstMatchingUser(snapshot, roleKey, numberKey);

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
                    .WhereEqualTo(numberField, numberKey)
                    .Limit(25)
                    .GetSnapshotAsync();

                var match = FirstMatchingUser(snapshot, roleKey, numberKey);

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

    private static DocumentSnapshot? FirstMatchingUser(QuerySnapshot snapshot, string roleKey, string number)
    {
        foreach (var doc in snapshot.Documents)
        {
            var data = doc.ToDictionary();

            if (IsDeleted(data))
            {
                continue;
            }

            var currentRole = NormalizeRole(FirstNonEmpty(
                GetString(data, "role", "Role"),
                GetString(data, "userRole", "UserRole")
            ));

            var currentNumber = OnlyDigits(FirstNonEmpty(
                GetString(data, "number", "Number"),
                GetString(data, "schoolNo", "SchoolNo"),
                GetString(data, "studentNo", "StudentNo"),
                GetString(data, "teacherNo", "TeacherNo"),
                GetString(data, "parentNo", "ParentNo"),
                GetString(data, "adminNo", "AdminNo")
            ));

            if (currentRole == roleKey && currentNumber == number)
            {
                return doc;
            }
        }

        return null;
    }

    private async Task<bool> VerifySystemAdminPasswordAsync(string password)
    {
        var doc = await _firestore
            .Collection("system")
            .Document("admin_account")
            .GetSnapshotAsync();

        if (!doc.Exists)
        {
            return IsDevelopment() && password == "admin123";
        }

        var data = doc.ToDictionary();
        var passwordHash = GetString(data, "passwordHash", "PasswordHash", "hash", "Hash");

        if (PasswordHashService.IsHash(passwordHash) &&
            PasswordHashService.VerifyPassword(password, passwordHash))
        {
            return true;
        }

        var legacyPassword = GetString(
            data,
            "password",
            "Password",
            "adminPassword",
            "AdminPassword",
            "sifre",
            "Sifre",
            "Ã…Å¸ifre",
            "Ã…Âifre"
        );

        if (!string.IsNullOrWhiteSpace(legacyPassword))
        {
            return legacyPassword == password;
        }

        return IsDevelopment() && password == "admin123";
    }

    private async Task<bool> SystemAdminAccountHasPasswordAsync()
    {
        var doc = await _firestore
            .Collection("system")
            .Document("admin_account")
            .GetSnapshotAsync();

        if (!doc.Exists)
        {
            return false;
        }

        var data = doc.ToDictionary();
        var passwordValue = GetString(
            data,
            "passwordHash",
            "PasswordHash",
            "hash",
            "Hash",
            "password",
            "Password",
            "adminPassword",
            "AdminPassword",
            "sifre",
            "Sifre",
            "Ãƒâ€¦Ã…Â¸ifre",
            "Ãƒâ€¦Ã‚Âifre"
        );

        return !string.IsNullOrWhiteSpace(passwordValue);
    }

    private bool IsDevelopment()
    {
        return _environment.IsDevelopment();
    }

    private static string[] RoleQueryValues(string roleKey)
    {
        var display = NormalizeRoleToDisplay(roleKey);

        return new[] { roleKey, display }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool IsDeleted(Dictionary<string, object> data)
    {
        var deleted = GetString(data, "isDeleted", "IsDeleted", "deleted", "Deleted").Trim().ToLowerInvariant();
        var active = GetString(data, "isActive", "IsActive", "active", "Active").Trim().ToLowerInvariant();

        if (deleted is "true" or "1" or "evet" or "yes")
        {
            return true;
        }

        if (active is "false" or "0" or "hayir" or "hayÄ±r" or "no")
        {
            return true;
        }

        return false;
    }

    private static bool GetBool(Dictionary<string, object> data, params string[] keys)
    {
        var value = GetString(data, keys).Trim().ToLowerInvariant();

        return value is "true" or "1" or "evet" or "yes";
    }

    private static string GetString(Dictionary<string, object> data, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!data.TryGetValue(key, out var value) || value == null)
            {
                continue;
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

    private static string OnlyDigits(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static string NormalizeRole(string value)
    {
        var key = NormalizeKey(value);

        if (key is "admin" or "yonetici")
        {
            return "admin";
        }

        if (key is "ogretmen" or "teacher")
        {
            return "ogretmen";
        }

        if (key is "ogrenci" or "student")
        {
            return "ogrenci";
        }

        if (key is "veli" or "parent")
        {
            return "veli";
        }

        return key;
    }

    private static string NormalizeRoleToDisplay(string roleKey)
    {
        return roleKey switch
        {
            "admin" => "Admin",
            "ogretmen" => "Öğretmen",
            "ogrenci" => "Öğrenci",
            "veli" => "Veli",
            _ => roleKey
        };
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

public enum AuthLoginFailure
{
    None,
    UserNotFound,
    Deleted,
    InvalidPassword,
    SystemAdminNotConfigured,
}

public sealed record AuthLoginResult(
    bool IsSuccess,
    AuthLoginFailure Failure,
    string UserId,
    string RoleKey,
    string Role,
    string Number,
    string Name,
    string Branch,
    string ClassName,
    bool MustChangePassword)
{
    public static AuthLoginResult Failed(AuthLoginFailure failure)
    {
        return new AuthLoginResult(false, failure, "", "", "", "", "", "", "", false);
    }

    public static AuthLoginResult Success(
        string userId,
        string roleKey,
        string role,
        string number,
        string name,
        string branch,
        string className,
        bool mustChangePassword)
    {
        return new AuthLoginResult(true, AuthLoginFailure.None, userId, roleKey, role, number, name, branch, className, mustChangePassword);
    }
}
