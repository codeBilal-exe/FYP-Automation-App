using FYP_AutomationSystem.Components;
using FYP_AutomationSystem.Data;
using FYP_AutomationSystem.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Razor / Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// EF Core PostgreSQL (Supabase)
// Connection string is read from appsettings.json -> ConnectionStrings:DefaultConnection
// or from the SUPABASE_CONNECTION environment variable (overrides appsettings).
var supabaseConn = Environment.GetEnvironmentVariable("SUPABASE_CONNECTION")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(supabaseConn))
{
    throw new InvalidOperationException(
        "Supabase connection string not configured. Set ConnectionStrings:DefaultConnection in appsettings.json " +
        "or the SUPABASE_CONNECTION environment variable. See SUPABASE_SETUP.md.");
}

NpgsqlConnectionStringBuilder connBuilder;
try
{
    connBuilder = new NpgsqlConnectionStringBuilder(supabaseConn);
}
catch (Exception ex)
{
    throw new InvalidOperationException(
        "Supabase connection string format is invalid. Verify host, username, password, and SSL settings. " +
        "See SUPABASE_SETUP.md.", ex);
}

if (string.IsNullOrWhiteSpace(connBuilder.Host) ||
    connBuilder.Host.Contains("REGION", StringComparison.OrdinalIgnoreCase) ||
    connBuilder.Host.Contains("your-host", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "Invalid Supabase DB host in connection string. Replace the placeholder host with your actual " +
        "Session pooler host from Supabase Dashboard -> Project Settings -> Database -> Connection string.");
}

if (string.IsNullOrWhiteSpace(connBuilder.Password) ||
    connBuilder.Password.Contains("YOUR_DB_PASSWORD", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "Invalid Supabase DB password in connection string. Replace the placeholder password with your real DB password.");
}

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(connBuilder.ConnectionString, npgsqlOptions => npgsqlOptions.EnableRetryOnFailure())
           .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));
builder.Services.AddTransient(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

// HttpContext + HttpClient (used by AuditService and GitHubService)
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<GitHubService>();

// Application services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<GroupService>();
builder.Services.AddScoped<ProposalService>();
builder.Services.AddScoped<MilestoneService>();
builder.Services.AddScoped<EvaluationService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<ProjectThreadService>();
builder.Services.AddScoped<VivaService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<PasswordResetService>();
builder.Services.AddScoped<SupabaseAuthSyncService>();

// Custom authentication state provider
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
}).AddCookie(options =>
{
    options.Cookie.Name = "FYP_AutomationSystem.Auth";
    options.LoginPath = "/";
    options.AccessDeniedPath = "/";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
});
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

// Apply migrations and seed data
using (var scope = app.Services.CreateScope())
{
    await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();

    // Add RepoLink column to Groups table if it doesn't exist (safe to run repeatedly)
    try
    {
        await db.Database.ExecuteSqlRawAsync(
            @"ALTER TABLE ""Groups"" ADD COLUMN IF NOT EXISTS ""RepoLink"" text;");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"RepoLink migration note: {ex.Message}");
    }

    if (builder.Configuration.GetValue<bool>("StartupTasks:SyncSupabaseAuth"))
    {
        var authSync = scope.ServiceProvider.GetRequiredService<SupabaseAuthSyncService>();
        var syncResult = await authSync.SyncExistingUsersFromApp();
        if (syncResult.Errors.Count > 0)
        {
            foreach (var err in syncResult.Errors)
            {
                Console.WriteLine($"Supabase auth sync error: {err}");
            }
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapPost("/auth/login", async (HttpContext httpContext, AuthService authService) =>
{
    var form = await httpContext.Request.ReadFormAsync();
    var email = form["email"].ToString().Trim();
    var password = form["password"].ToString().Trim();

    var user = await authService.Login(email, password);
    if (user == null)
    {
        var error = authService.LastLoginError == "Account locked. Try again later." ? "locked" : "invalid";
        return Results.Redirect($"/?error={error}&email={Uri.EscapeDataString(email)}", false);
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Name, user.FullName),
        new(ClaimTypes.Email, user.Email),
        new(ClaimTypes.Role, user.Role.ToString())
    };

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new ClaimsPrincipal(identity);
    await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
        new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30) });

    var redirectPath = user.Role switch
    {
        FYP_AutomationSystem.Models.UserRole.HOD => "/hod/dashboard",
        FYP_AutomationSystem.Models.UserRole.Student => "/student/dashboard",
        FYP_AutomationSystem.Models.UserRole.Supervisor => "/supervisor/dashboard",
        FYP_AutomationSystem.Models.UserRole.Coordinator => "/coordinator/dashboard",
        FYP_AutomationSystem.Models.UserRole.Admin => "/admin/dashboard",
        FYP_AutomationSystem.Models.UserRole.Panel => "/panel/dashboard",
        _ => "/dashboard"
    };

    return Results.Redirect(redirectPath, false);
}).DisableAntiforgery();

app.MapGet("/auth/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    AuthService.CurrentUser = null;
    return Results.Redirect("/", false);
});

app.MapPost("/auth/forgot-password", async (HttpContext httpContext, PasswordResetService passwordResetService) =>
{
    var form = await httpContext.Request.ReadFormAsync();
    var projectEmail = form["projectEmail"].ToString().Trim();

    var appBaseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
    var result = await passwordResetService.RequestPasswordReset(projectEmail, appBaseUrl);

    if (result.Success)
    {
        return Results.Redirect("/forgot-password?status=sent", false);
    }

    var status = Uri.EscapeDataString(result.Status);
    return Results.Redirect($"/forgot-password?status={status}", false);
}).DisableAntiforgery();

app.MapPost("/auth/reset-password", async (HttpContext httpContext, PasswordResetService passwordResetService) =>
{
    var form = await httpContext.Request.ReadFormAsync();
    var token = form["token"].ToString().Trim();
    var password = form["password"].ToString();
    var confirmPassword = form["confirmPassword"].ToString();

    if (password != confirmPassword)
    {
        return Results.Redirect($"/reset-password?token={Uri.EscapeDataString(token)}&status=nomatch", false);
    }

    var result = await passwordResetService.ResetPassword(token, password);
    if (!result.Success)
    {
        return Results.Redirect($"/reset-password?token={Uri.EscapeDataString(token)}&status=invalid", false);
    }

    return Results.Redirect("/?reset=success", false);
}).DisableAntiforgery();

app.MapPost("/auth/reset-password-supabase", async (HttpContext httpContext, PasswordResetService passwordResetService) =>
{
    var form = await httpContext.Request.ReadFormAsync();
    var accessToken = form["accessToken"].ToString().Trim();
    var password = form["password"].ToString();
    var confirmPassword = form["confirmPassword"].ToString();

    if (password != confirmPassword)
    {
        return Results.Redirect("/reset-password-supabase?status=nomatch", false);
    }

    var result = await passwordResetService.ResetSupabasePassword(accessToken, password);
    if (!result.Success)
    {
        var status = Uri.EscapeDataString(result.Status);
        return Results.Redirect($"/reset-password-supabase?status={status}", false);
    }

    return Results.Redirect("/?reset=success", false);
}).DisableAntiforgery();

// ───────────────────────────────────────────────────────────────────────────
// File-download endpoints — serve user-uploaded content from the database so
// it survives Azure App Service redeploys (which wipe wwwroot uploads).
// Each endpoint prefers the bytea column; falls back to wwwroot/<path> for
// legacy rows that pre-date the columns.
// ───────────────────────────────────────────────────────────────────────────

static string GuessContentType(string? fileName)
{
    var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
    return ext switch
    {
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".zip" => "application/zip",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream"
    };
}

static IResult ServeFromBytesOrDisk(byte[]? bytes, string? relativePath, string? fileName, IWebHostEnvironment env)
{
    if (bytes != null && bytes.Length > 0)
    {
        return Results.File(bytes, GuessContentType(fileName), fileDownloadName: fileName);
    }

    if (!string.IsNullOrWhiteSpace(relativePath))
    {
        var trimmed = relativePath.Replace('\\', '/').TrimStart('/');
        var full = Path.Combine(env.WebRootPath, trimmed);
        if (File.Exists(full))
        {
            var disk = File.OpenRead(full);
            return Results.File(disk, GuessContentType(fileName), fileDownloadName: fileName);
        }
    }

    return Results.NotFound();
}

app.MapGet("/files/proposal/{id:int}", async (int id, AppDbContext db, IWebHostEnvironment env) =>
{
    var p = await db.Proposals
        .Where(x => x.Id == id)
        .Select(x => new { x.DocumentBytes, x.DocumentPath, x.DocumentName })
        .FirstOrDefaultAsync();
    if (p == null) return Results.NotFound();
    return ServeFromBytesOrDisk(p.DocumentBytes, p.DocumentPath, p.DocumentName ?? $"proposal-{id}.pdf", env);
}).RequireAuthorization();

app.MapGet("/files/milestone/{id:int}", async (int id, AppDbContext db, IWebHostEnvironment env) =>
{
    var m = await db.Milestones
        .Where(x => x.Id == id)
        .Select(x => new { x.SubmissionBytes, x.SubmissionFilePath, x.SubmissionFileName })
        .FirstOrDefaultAsync();
    if (m == null) return Results.NotFound();
    return ServeFromBytesOrDisk(m.SubmissionBytes, m.SubmissionFilePath, m.SubmissionFileName ?? $"milestone-{id}.pdf", env);
}).RequireAuthorization();

app.MapGet("/files/document/{id:int}", async (int id, AppDbContext db, IWebHostEnvironment env) =>
{
    var d = await db.Documents
        .Where(x => x.Id == id)
        .Select(x => new { x.Content, x.FilePath, x.FileName })
        .FirstOrDefaultAsync();
    if (d == null) return Results.NotFound();
    return ServeFromBytesOrDisk(d.Content, d.FilePath, d.FileName, env);
}).RequireAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
