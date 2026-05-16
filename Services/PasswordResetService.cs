using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FYP_AutomationSystem.Data;
using FYP_AutomationSystem.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FYP_AutomationSystem.Services
{
    public class PasswordResetService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly AuthService _authService;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient = new();

        public PasswordResetService(
            IDbContextFactory<AppDbContext> contextFactory,
            AuthService authService,
            IConfiguration configuration)
        {
            _contextFactory = contextFactory;
            _authService = authService;
            _configuration = configuration;
        }

        public async Task<(bool Success, string Status, string Message)> RequestPasswordReset(string projectEmail, string appBaseUrl)
        {
            var normalizedProjectEmail = (projectEmail ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedProjectEmail))
            {
                return (false, "invalid", "Please enter a valid email.");
            }

            var existsInAppUsers = await ExistsInAppUsers(normalizedProjectEmail);
            if (!existsInAppUsers)
            {
                return (false, "notfound", "Email doesn't exist.");
            }

            if (!await ExistsInSupabaseAuth(normalizedProjectEmail))
            {
                return (false, "not_in_auth", "Email exists in app users, but not in Supabase Auth users.");
            }

            var mailResult = await SendSupabaseRecoveryEmail(normalizedProjectEmail, appBaseUrl);
            if (!mailResult.Success)
            {
                return (false, mailResult.Status, mailResult.Message);
            }

            return (true, "sent", "Password reset link sent to your project email.");
        }

        public async Task<(bool Success, string Message)> ResetPassword(string token, string newPassword)
        {
            var rawToken = (token ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(rawToken))
            {
                return (false, "Invalid or expired reset link.");
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            {
                return (false, "Password must be at least 6 characters.");
            }

            var tokenHash = ComputeSha256(rawToken);
            var now = DateTime.UtcNow;

            await using var context = await _contextFactory.CreateDbContextAsync();
            var resetToken = await context.PasswordResetTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

            if (resetToken == null || resetToken.UsedAt.HasValue || resetToken.ExpiresAt <= now || resetToken.User == null || !resetToken.User.IsActive)
            {
                return (false, "Invalid or expired reset link.");
            }

            resetToken.User.PasswordHash = _authService.HashPassword(newPassword);
            resetToken.User.IsLockedOut = false;
            resetToken.User.FailedLoginAttempts = 0;
            resetToken.User.LockoutUntil = null;
            resetToken.UsedAt = now;

            await context.SaveChangesAsync();
            return (true, "Password reset successful.");
        }

        public async Task<(bool Success, string Status, string Message)> ResetSupabasePassword(string accessToken, string newPassword)
        {
            var token = (accessToken ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                return (false, "missing_token", "Recovery token is missing.");
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            {
                return (false, "invalid_password", "Password must be at least 6 characters.");
            }

            var supabaseUrl = (_configuration["Supabase:Url"] ?? string.Empty).Trim().TrimEnd('/');
            var anonKey = (_configuration["Supabase:AnonKey"] ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(anonKey))
            {
                return (false, "supabase_config_missing", "Supabase configuration is missing.");
            }

            try
            {
                var requestUrl = $"{supabaseUrl}/auth/v1/user";
                using var request = new HttpRequestMessage(HttpMethod.Put, requestUrl)
                {
                    Content = JsonContent.Create(new { password = newPassword })
                };
                request.Headers.Add("apikey", anonKey);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    return (true, "success", "Password updated successfully.");
                }

                var errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Supabase update password error ({(int)response.StatusCode}): {errorBody}");
                if ((int)response.StatusCode == 401 || (int)response.StatusCode == 403)
                {
                    return (false, "invalid_token", "Recovery link is invalid or expired.");
                }

                return (false, "update_failed", "Failed to update password. Please request a new reset link.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Supabase update password exception: {ex.Message}");
                return (false, "update_failed", "Failed to update password. Please request a new reset link.");
            }
        }

        private async Task<(bool Success, string Status, string Message)> SendSupabaseRecoveryEmail(string recipientEmail, string appBaseUrl)
        {
            var supabaseUrl = (_configuration["Supabase:Url"] ?? string.Empty).Trim().TrimEnd('/');
            var anonKey = (_configuration["Supabase:AnonKey"] ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(anonKey))
            {
                return (false, "supabase_config_missing", "Supabase Auth is not configured. Reset email could not be sent.");
            }

            try
            {
                var redirectTo = $"{appBaseUrl.TrimEnd('/')}/reset-password-supabase";
                var requestUrl = $"{supabaseUrl}/auth/v1/recover?redirect_to={Uri.EscapeDataString(redirectTo)}";

                using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
                {
                    Content = JsonContent.Create(new { email = recipientEmail })
                };
                request.Headers.Add("apikey", anonKey);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", anonKey);

                using var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    return (true, "sent", "Password reset email sent.");
                }

                var errorBody = await response.Content.ReadAsStringAsync();
                if ((int)response.StatusCode == 429)
                {
                    return (false, "rate_limited", "Too many requests. Supabase built-in mail is rate-limited (2 emails/hour).");
                }

                Console.WriteLine($"Supabase recover error ({(int)response.StatusCode}): {errorBody}");
                return (false, "send_failed", "Supabase failed to send reset email. Please try again.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Supabase recover exception: {ex.Message}");
                return (false, "send_failed", "Failed to send reset email. Please try again.");
            }
        }

        private async Task<bool> ExistsInSupabaseAuth(string email)
        {
            var connString = Environment.GetEnvironmentVariable("SUPABASE_CONNECTION")
                ?? _configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connString))
            {
                return false;
            }

            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            await using var cmd = new NpgsqlCommand(
                "select exists(select 1 from auth.users where lower(email)=lower(@email) and deleted_at is null);",
                conn);
            cmd.Parameters.AddWithValue("email", email);

            var scalar = await cmd.ExecuteScalarAsync();
            return scalar is bool b && b;
        }

        private async Task<bool> ExistsInAppUsers(string email)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Users.AnyAsync(u => u.IsActive && u.Email.ToLower() == email.ToLower());
        }

        private static string ComputeSha256(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes);
        }
    }
}
