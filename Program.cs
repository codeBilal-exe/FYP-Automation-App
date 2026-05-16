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
builder.Services.AddScoped<ProposalService>();
builder.Services.AddScoped<MilestoneService>();
builder.Services.AddScoped<EvaluationService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<MessageService>();
builder.Services.AddScoped<VivaService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<AuditService>();

// Custom authentication state provider
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
}).AddCookie(options =>
{
    options.Cookie.Name = "FYP_AutomationSystem.Auth";
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/login";
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
    var auth = scope.ServiceProvider.GetRequiredService<AuthService>();
    await SeedData.InitializeAsync(db, auth);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapPost("/auth/login", async (HttpContext httpContext, AuthService authService) =>
{
    var form = await httpContext.Request.ReadFormAsync();
    var email = form["email"].ToString().Trim();
    var password = form["password"].ToString();

    var user = await authService.Login(email, password);
    if (user == null)
    {
        var error = authService.LastLoginError == "Account locked. Try again later." ? "locked" : "invalid";
        return Results.Redirect($"/login?error={error}&email={Uri.EscapeDataString(email)}", false);
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
        _ => "/dashboard"
    };

    return Results.Redirect(redirectPath, false);
}).DisableAntiforgery();

app.MapGet("/auth/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    AuthService.CurrentUser = null;
    return Results.Redirect("/login", false);
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
