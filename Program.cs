using Google.Cloud.Firestore;
using Microsoft.AspNetCore.DataProtection;
using mvc_web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddControllersWithViews();

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

builder.Services.AddScoped<FirestoreService>();
builder.Services.AddScoped<SessionService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Auth/Login");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}"
);

app.Run();
