namespace mvc_web.Services;

public class SessionService
{
    private readonly IHttpContextAccessor? _contextAccessor;

    public SessionService()
    {
    }

    public string? GetRole(HttpContext httpContext)
    {
        return httpContext.Session.GetString("Role");
    }

    public string? GetUserId(HttpContext httpContext)
    {
        return httpContext.Session.GetString("UserId");
    }

    public string? GetName(HttpContext httpContext)
    {
        return httpContext.Session.GetString("Name");
    }

    public string? GetNumber(HttpContext httpContext)
    {
        return httpContext.Session.GetString("Number");
    }

    public bool IsLoggedIn(HttpContext httpContext)
    {
        return !string.IsNullOrWhiteSpace(GetRole(httpContext));
    }

    public bool IsAdmin(HttpContext httpContext)
    {
        return GetRole(httpContext) == "Admin";
    }

    public bool IsTeacher(HttpContext httpContext)
    {
        return GetRole(httpContext) == "Öğretmen";
    }

    public void Login(
        HttpContext httpContext,
        string userId,
        string role,
        string name,
        string number
    )
    {
        httpContext.Session.SetString("UserId", userId);
        httpContext.Session.SetString("Role", role);
        httpContext.Session.SetString("Name", name);
        httpContext.Session.SetString("Number", number);
    }

    public void Logout(HttpContext httpContext)
    {
        httpContext.Session.Clear();
    }
}