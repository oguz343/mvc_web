namespace mvc_web.Services
{
    public class SessionService
    {
        private const string UserIdKey = "UserId";
        private const string UserNameKey = "UserName";
        private const string UserRoleKey = "UserRole";
        private const string UserNumberKey = "UserNumber";

        public void Login(
            HttpContext context,
            string userId,
            string name,
            string role,
            string number
        )
        {
            context.Session.SetString(UserIdKey, userId ?? "");
            context.Session.SetString(UserNameKey, name ?? "");
            context.Session.SetString(UserRoleKey, role ?? "");
            context.Session.SetString(UserNumberKey, number ?? "");
        }

        public void Logout(HttpContext context)
        {
            context.Session.Clear();
        }

        public string? GetUserId(HttpContext context)
        {
            return context.Session.GetString(UserIdKey);
        }

        public string? GetName(HttpContext context)
        {
            return context.Session.GetString(UserNameKey);
        }

        public string? GetRole(HttpContext context)
        {
            return FirstNonEmpty(
                context.Session.GetString(UserRoleKey),
                context.Session.GetString("Role"),
                context.Session.GetString("role")
            );
        }

        public string? GetNumber(HttpContext context)
        {
            return FirstNonEmpty(
                context.Session.GetString(UserNumberKey),
                context.Session.GetString("Number"),
                context.Session.GetString("SchoolNo"),
                context.Session.GetString("LoginNumber")
            );
        }

        public bool IsLoggedIn(HttpContext context)
        {
            return !string.IsNullOrWhiteSpace(GetRole(context));
        }

        public bool IsAdmin(HttpContext context)
        {
            return NormalizeRole(GetRole(context)) == "admin" &&
                OnlyDigits(GetNumber(context)) == "0000";
        }

        public bool IsTeacher(HttpContext context)
        {
            return NormalizeRole(GetRole(context)) == "ogretmen";
        }

        public bool IsStudent(HttpContext context)
        {
            return NormalizeRole(GetRole(context)) == "ogrenci";
        }

        public bool IsParent(HttpContext context)
        {
            return NormalizeRole(GetRole(context)) == "veli";
        }

        private static string NormalizeRole(string? value)
        {
            var key = NormalizeKey(value);

            return key switch
            {
                "admin" or "yonetici" => "admin",
                "ogretmen" or "teacher" => "ogretmen",
                "ogrenci" or "student" => "ogrenci",
                "veli" or "parent" => "veli",
                _ => key
            };
        }

        private static string NormalizeKey(string? value)
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
                .Replace("Ã§", "c")
                .Replace("Ã„Â±", "i")
                .Replace("Ã„Å¸", "g")
                .Replace("ÃƒÂ¼", "u")
                .Replace("Ã…Å¸", "s")
                .Replace("ÃƒÂ¶", "o")
                .Replace("ÃƒÂ§", "c");

            return new string(value.Where(char.IsLetterOrDigit).ToArray());
        }

        private static string OnlyDigits(string? value)
        {
            return new string((value ?? "").Where(char.IsDigit).ToArray());
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "";
        }
    }
}
