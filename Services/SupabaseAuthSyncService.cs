using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using FYP_AutomationSystem.Data;
using FYP_AutomationSystem.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FYP_AutomationSystem.Services
{
    public class SupabaseAuthSyncService
    {
        private readonly IConfiguration _configuration;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly HttpClient _httpClient = new();

        public SupabaseAuthSyncService(IConfiguration configuration, IDbContextFactory<AppDbContext> contextFactory)
        {
            _configuration = configuration;
            _contextFactory = contextFactory;
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(GetSupabaseUrl())
            && !string.IsNullOrWhiteSpace(GetServiceRoleKey());

        public async Task<(bool Success, string Message)> EnsureUserSynced(string email, string password, string fullName, UserRole role)
        {
            if (!IsConfigured)
            {
                return (false, "Supabase service role key is missing. Set Supabase:ServiceRoleKey.");
            }

            var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return (false, "Email is required for auth sync.");
            }

            var authUserId = await GetAuthUserIdByEmail(normalizedEmail);
            if (string.IsNullOrWhiteSpace(authUserId))
            {
                return await CreateAuthUser(normalizedEmail, password, fullName, role);
            }

            return await UpdateAuthUser(authUserId, password, fullName, role);
        }

        public async Task<(bool Success, string Message)> DeleteAuthUserByEmail(string email)
        {
            if (!IsConfigured)
            {
                return (false, "Supabase service role key is missing. Set Supabase:ServiceRoleKey.");
            }

            var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return (false, "Email is required for auth delete.");
            }

            var authUserId = await GetAuthUserIdByEmail(normalizedEmail);
            if (string.IsNullOrWhiteSpace(authUserId))
            {
                return (true, "Auth user not found; nothing to delete.");
            }

            var supabaseUrl = GetSupabaseUrl();
            var serviceRoleKey = GetServiceRoleKey();
            var endpoint = $"{supabaseUrl}/auth/v1/admin/users/{authUserId}";

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Delete, endpoint)
                {
                    Content = JsonContent.Create(new { should_soft_delete = false })
                };
                AddAdminHeaders(request, serviceRoleKey);

                using var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    return (true, "Auth user deleted.");
                }

                var body = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Delete auth user failed ({(int)response.StatusCode}): {body}");
                return (false, "Failed to delete user from Supabase Auth.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delete auth user exception: {ex.Message}");
                return (false, "Failed to delete user from Supabase Auth.");
            }
        }

        public async Task<(int Created, int Existing, int Failed, List<string> Errors)> SyncExistingUsersFromApp()
        {
            var created = 0;
            var existing = 0;
            var failed = 0;
            var errors = new List<string>();

            if (!IsConfigured)
            {
                errors.Add("Supabase service role key is missing. Skipping auth sync.");
                return (created, existing, 0, errors);
            }

            await using var context = await _contextFactory.CreateDbContextAsync();
            var users = await context.Users
                .Where(u => u.IsActive)
                .Select(u => new { u.Email, u.FullName, u.Role })
                .ToListAsync();

            foreach (var u in users)
            {
                var email = (u.Email ?? string.Empty).Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(email))
                {
                    continue;
                }

                var authUserId = await GetAuthUserIdByEmail(email);
                if (!string.IsNullOrWhiteSpace(authUserId))
                {
                    existing++;
                    continue;
                }

                var randomPassword = GenerateTempPassword();
                var syncResult = await CreateAuthUser(email, randomPassword, u.FullName, u.Role);
                if (syncResult.Success)
                {
                    created++;
                }
                else
                {
                    failed++;
                    errors.Add($"{email}: {syncResult.Message}");
                }
            }

            return (created, existing, failed, errors);
        }

        private async Task<(bool Success, string Message)> CreateAuthUser(string email, string password, string fullName, UserRole role)
        {
            var supabaseUrl = GetSupabaseUrl();
            var serviceRoleKey = GetServiceRoleKey();
            var endpoint = $"{supabaseUrl}/auth/v1/admin/users";

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = JsonContent.Create(new
                    {
                        email,
                        password,
                        email_confirm = true,
                        user_metadata = new { full_name = fullName },
                        app_metadata = new { role = role.ToString() }
                    })
                };
                AddAdminHeaders(request, serviceRoleKey);

                using var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    return (true, "Auth user created.");
                }

                var body = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Create auth user failed ({(int)response.StatusCode}): {body}");
                return (false, "Failed to create user in Supabase Auth.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Create auth user exception: {ex.Message}");
                return (false, "Failed to create user in Supabase Auth.");
            }
        }

        private async Task<(bool Success, string Message)> UpdateAuthUser(string authUserId, string password, string fullName, UserRole role)
        {
            var supabaseUrl = GetSupabaseUrl();
            var serviceRoleKey = GetServiceRoleKey();
            var endpoint = $"{supabaseUrl}/auth/v1/admin/users/{authUserId}";

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Put, endpoint)
                {
                    Content = JsonContent.Create(new
                    {
                        password,
                        user_metadata = new { full_name = fullName },
                        app_metadata = new { role = role.ToString() }
                    })
                };
                AddAdminHeaders(request, serviceRoleKey);

                using var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    return (true, "Auth user updated.");
                }

                var body = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Update auth user failed ({(int)response.StatusCode}): {body}");
                return (false, "Failed to update user in Supabase Auth.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update auth user exception: {ex.Message}");
                return (false, "Failed to update user in Supabase Auth.");
            }
        }

        private async Task<string?> GetAuthUserIdByEmail(string email)
        {
            var connString = Environment.GetEnvironmentVariable("SUPABASE_CONNECTION")
                ?? _configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connString))
            {
                return null;
            }

            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            await using var cmd = new NpgsqlCommand(
                "select id::text from auth.users where lower(email)=lower(@email) and deleted_at is null limit 1;",
                conn);
            cmd.Parameters.AddWithValue("email", email);

            var scalar = await cmd.ExecuteScalarAsync();
            return scalar?.ToString();
        }

        private static string GenerateTempPassword()
        {
            var bytes = RandomNumberGenerator.GetBytes(18);
            return Convert.ToBase64String(bytes).Replace("+", "A").Replace("/", "b") + "9#";
        }

        private static void AddAdminHeaders(HttpRequestMessage request, string serviceRoleKey)
        {
            request.Headers.Add("apikey", serviceRoleKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceRoleKey);
        }

        private string GetSupabaseUrl()
        {
            return (_configuration["Supabase:Url"] ?? string.Empty).Trim().TrimEnd('/');
        }

        private string GetServiceRoleKey()
        {
            return (Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY")
                ?? _configuration["Supabase:ServiceRoleKey"]
                ?? string.Empty).Trim();
        }
    }
}
