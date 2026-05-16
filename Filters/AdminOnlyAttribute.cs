using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace mvc_web.Filters;

public class AdminOnlyAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var session = context.HttpContext.Session;

        var role =
            session.GetString("UserRole") ??
            session.GetString("Role") ??
            session.GetString("role") ??
            "";

        var number =
            session.GetString("UserNumber") ??
            session.GetString("Number") ??
            session.GetString("SchoolNo") ??
            session.GetString("LoginNumber") ??
            "";

        var roleKey = NormalizeKey(role);
        var numberKey = OnlyDigits(number);

        var isAdmin =
            roleKey == "admin" &&
            numberKey == "0000";

        if (!isAdmin)
        {
            context.Result = new RedirectToActionResult("Login", "Auth", null);
            return;
        }

        base.OnActionExecuting(context);
    }

    private static string OnlyDigits(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return new string(value.Where(char.IsDigit).ToArray());
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
            .Replace("ç", "c");

        return new string(value.Where(char.IsLetterOrDigit).ToArray());
    }
}