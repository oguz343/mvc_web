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
            return context.Session.GetString(UserRoleKey);
        }

        public string? GetNumber(HttpContext context)
        {
            return context.Session.GetString(UserNumberKey);
        }

        public bool IsLoggedIn(HttpContext context)
        {
            return !string.IsNullOrWhiteSpace(GetRole(context));
        }

        public bool IsAdmin(HttpContext context)
        {
            return GetRole(context) == "Admin";
        }

        public bool IsTeacher(HttpContext context)
        {
            return GetRole(context) == "Öğretmen";
        }

        public bool IsStudent(HttpContext context)
        {
            return GetRole(context) == "Öğrenci";
        }

        public bool IsParent(HttpContext context)
        {
            return GetRole(context) == "Veli";
        }
    }
}