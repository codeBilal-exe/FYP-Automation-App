using System.Security.Cryptography;
using System.Text;
using Npgsql;

var root = FindProjectRoot();
var appSettingsPath = Path.Combine(root, "appsettings.json");
var csvPath = args.Length > 0 ? args[0] : Path.Combine(root, "fyp_mock_users.csv");

if (!File.Exists(appSettingsPath))
{
    throw new FileNotFoundException($"appsettings.json not found at: {appSettingsPath}");
}

if (!File.Exists(csvPath))
{
    throw new FileNotFoundException($"CSV file not found at: {csvPath}");
}

var connectionString = GetConnectionString(appSettingsPath);
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing in appsettings.json.");
}

var rows = LoadUsers(csvPath);

await using var conn = new NpgsqlConnection(connectionString);
await conn.OpenAsync();
await using var tx = await conn.BeginTransactionAsync();

try
{
    const string truncateSql = """
DO $$
DECLARE table_list text;
BEGIN
  SELECT string_agg(format('%I.%I', schemaname, tablename), ', ')
    INTO table_list
  FROM pg_tables
  WHERE schemaname = 'public'
    AND tablename <> '__EFMigrationsHistory';

  IF table_list IS NOT NULL THEN
    EXECUTE 'TRUNCATE TABLE ' || table_list || ' RESTART IDENTITY CASCADE';
  END IF;
END $$;
""";

    await using (var truncate = new NpgsqlCommand(truncateSql, conn, tx))
    {
        await truncate.ExecuteNonQueryAsync();
    }

    var inserted = 0;
    foreach (var row in rows)
    {
        await using var insert = new NpgsqlCommand(
            """
INSERT INTO "Users" (
  "FullName", "Email", "PasswordHash", "Role", "Expertise",
  "CreatedAt", "IsActive", "FailedLoginAttempts", "IsLockedOut", "LockoutUntil"
) VALUES (
  @fullName, @email, @passwordHash, @role, @expertise,
  @createdAt, @isActive, @failedAttempts, @isLocked, @lockoutUntil
);
""", conn, tx);

        insert.Parameters.AddWithValue("fullName", row.FullName);
        insert.Parameters.AddWithValue("email", row.Email);
        insert.Parameters.AddWithValue("passwordHash", HashPassword(row.Password));
        insert.Parameters.AddWithValue("role", MapRole(row.Role));
        insert.Parameters.AddWithValue("expertise", DBNull.Value);
        insert.Parameters.AddWithValue("createdAt", DateTime.UtcNow);
        insert.Parameters.AddWithValue("isActive", true);
        insert.Parameters.AddWithValue("failedAttempts", 0);
        insert.Parameters.AddWithValue("isLocked", false);
        insert.Parameters.AddWithValue("lockoutUntil", DBNull.Value);

        await insert.ExecuteNonQueryAsync();
        inserted++;
    }

    long userCount;
    await using (var count = new NpgsqlCommand("""SELECT COUNT(*) FROM "Users";""", conn, tx))
    {
        userCount = (long)(await count.ExecuteScalarAsync() ?? 0L);
    }

    await tx.CommitAsync();
    Console.WriteLine($"Database reset complete. Users inserted: {inserted}. Users in table now: {userCount}");
}
catch
{
    await tx.RollbackAsync();
    throw;
}

return;

static string FindProjectRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "FYP_AutomationSystem.csproj")))
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    throw new DirectoryNotFoundException("Could not locate repository root.");
}

static string? GetConnectionString(string appSettingsPath)
{
    using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(appSettingsPath));
    if (!doc.RootElement.TryGetProperty("ConnectionStrings", out var cs))
    {
        return null;
    }

    return cs.TryGetProperty("DefaultConnection", out var val) ? val.GetString() : null;
}

static List<UserRow> LoadUsers(string csvPath)
{
    var lines = File.ReadAllLines(csvPath);
    if (lines.Length < 2)
    {
        throw new InvalidOperationException("CSV must contain a header and at least one data row.");
    }

    var rows = new List<UserRow>();
    for (var i = 1; i < lines.Length; i++)
    {
        var line = lines[i].Trim();
        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        var parts = line.Split(',');
        if (parts.Length != 4)
        {
            throw new InvalidOperationException($"Invalid CSV format at line {i + 1}: expected 4 columns.");
        }

        var fullName = parts[0].Trim();
        var email = parts[1].Trim();
        var password = parts[2].Trim();
        var role = parts[3].Trim();

        if (string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(role))
        {
            throw new InvalidOperationException($"Missing required value at CSV line {i + 1}.");
        }

        rows.Add(new UserRow(fullName, email, password, role));
    }

    return rows;
}

static int MapRole(string role)
{
    return role.Trim().ToLowerInvariant() switch
    {
        "student" => 0,
        "supervisor" => 1,
        "hod" => 2,
        "coordinator" => 3,
        "panel member" => 4,
        "panel" => 4,
        "admin" => 5,
        _ => throw new InvalidOperationException($"Unknown role in CSV: {role}")
    };
}

static string HashPassword(string plain)
{
    using var sha256 = SHA256.Create();
    var bytes = Encoding.UTF8.GetBytes(plain);
    var hash = sha256.ComputeHash(bytes);
    return Convert.ToBase64String(hash);
}

public sealed record UserRow(string FullName, string Email, string Password, string Role);
