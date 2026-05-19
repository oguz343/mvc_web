using FirebaseAdmin.Auth;
using Microsoft.AspNetCore.Mvc;
using mvc_web.Services;

namespace mvc_web.Controllers;

[ApiController]
[Route("api/mobile-auth")]
public class MobileAuthController : ControllerBase
{
    private const int FirebaseUidMaxLength = 128;

    private readonly AuthLoginService _authLoginService;
    private readonly FirebaseAuth _firebaseAuth;

    public MobileAuthController(
        AuthLoginService authLoginService,
        FirebaseAuth firebaseAuth)
    {
        _authLoginService = authLoginService;
        _firebaseAuth = firebaseAuth;
    }

    [HttpPost("custom-token")]
    public async Task<IActionResult> CreateCustomToken([FromBody] MobileCustomTokenRequest request)
    {
        var role = request.Role?.Trim() ?? "";
        var number = OnlyDigits(request.Number ?? "");
        var password = request.Password ?? "";

        if (string.IsNullOrWhiteSpace(role) ||
            string.IsNullOrWhiteSpace(number) ||
            string.IsNullOrWhiteSpace(password))
        {
            return BadRequest(new MobileAuthErrorResponse("missing_credentials"));
        }

        var login = await _authLoginService.ValidateAsync(role, number, password);

        if (!login.IsSuccess)
        {
            return Unauthorized(new MobileAuthErrorResponse("invalid_credentials"));
        }

        if (string.IsNullOrWhiteSpace(login.UserId) ||
            login.UserId.Length > FirebaseUidMaxLength)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new MobileAuthErrorResponse("unsupported_user_id")
            );
        }

        var claims = new Dictionary<string, object>
        {
            ["role"] = login.RoleKey,
            ["number"] = login.Number,
        };

        var customToken = await _firebaseAuth.CreateCustomTokenAsync(login.UserId, claims);

        return Ok(
            new MobileCustomTokenResponse(
                customToken,
                login.UserId,
                login.RoleKey,
                login.Role,
                login.Number,
                login.Name,
                login.MustChangePassword
            )
        );
    }

    private static string OnlyDigits(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return new string(value.Where(char.IsDigit).ToArray());
    }
}

public sealed record MobileCustomTokenRequest(
    string? Role,
    string? Number,
    string? Password);

public sealed record MobileCustomTokenResponse(
    string CustomToken,
    string Uid,
    string Role,
    string RoleDisplay,
    string Number,
    string Name,
    bool MustChangePassword);

public sealed record MobileAuthErrorResponse(string Error);
