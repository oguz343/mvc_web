using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.DataProtection;
using mvc_web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddControllersWithViews();

var mobileAuthAllowedOrigins = builder.Configuration
    .GetSection("MobileAuth:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "MobileAuthCors",
        policy =>
        {
            policy
                .SetIsOriginAllowed(origin =>
                {
                    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    {
                        return false;
                    }

                    if (uri.Host is "localhost" or "127.0.0.1")
                    {
                        return true;
                    }

                    return mobileAuthAllowedOrigins.Any(allowed =>
                        Uri.TryCreate(allowed, UriKind.Absolute, out var allowedUri) &&
                        string.Equals(
                            allowedUri.Scheme,
                            uri.Scheme,
                            StringComparison.OrdinalIgnoreCase
                        ) &&
                        string.Equals(
                            allowedUri.Host,
                            uri.Host,
                            StringComparison.OrdinalIgnoreCase
                        ) &&
                        allowedUri.Port == uri.Port
                    );
                })
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    );
});

var dataProtectionPath = Path.Combine(
    builder.Environment.ContentRootPath,
    "App_Data",
    "DataProtectionKeys"
);

Directory.CreateDirectory(dataProtectionPath);

builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));

builder.Services.AddHttpContextAccessor();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(6);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

var firebaseKeyPath = Path.Combine(
    builder.Environment.ContentRootPath,
    "firebase-service-account.json"
);

Environment.SetEnvironmentVariable(
    "GOOGLE_APPLICATION_CREDENTIALS",
    firebaseKeyPath
);

builder.Services.AddSingleton(provider =>
{
    var projectId = builder.Configuration["Firebase:ProjectId"];

    if (string.IsNullOrWhiteSpace(projectId))
    {
        throw new Exception("Firebase ProjectId appsettings.json içinde bulunamadı.");
    }

    return FirestoreDb.Create(projectId);
});

builder.Services.AddSingleton(provider =>
{
    var projectId = builder.Configuration["Firebase:ProjectId"];

    if (string.IsNullOrWhiteSpace(projectId))
    {
        throw new Exception("Firebase ProjectId appsettings.json içinde bulunamadı.");
    }

    return FirebaseApp.DefaultInstance ?? FirebaseApp.Create(
        new AppOptions
        {
            Credential = GoogleCredential.GetApplicationDefault(),
            ProjectId = projectId,
        }
    );
});

builder.Services.AddSingleton(provider =>
    FirebaseAuth.GetAuth(provider.GetRequiredService<FirebaseApp>()));

builder.Services.AddScoped<FirestoreService>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<AuthLoginService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Auth/Login");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseCors("MobileAuthCors");

app.UseSession();

app.UseAuthorization();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}"
);

app.Run();
