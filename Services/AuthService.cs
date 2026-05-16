using System.Security.Cryptography;
using System.Text;
using FYP_AutomationSystem.Data;
using FYP_AutomationSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace FYP_AutomationSystem.Services
{
    public class AuthService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        public static User? CurrentUser { get; set; }
        public string? LastLoginError { get; private set; }

        public AuthService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<User?> Login(string email, string password)
        {
            LastLoginError = null;
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive);

                if (user == null)
                {
                    LastLoginError = "Invalid email or password.";
                    return null;
                }

                if (user.IsLockedOut)
                {
                    if (user.LockoutUntil.HasValue && user.LockoutUntil.Value > DateTime.UtcNow)
                    {
                        LastLoginError = "Account locked. Try again later.";
                        return null;
                    }

                    user.IsLockedOut = false;
                    user.FailedLoginAttempts = 0;
                    user.LockoutUntil = null;
                    await context.SaveChangesAsync();
                }

                if (!VerifyPassword(password, user.PasswordHash))
                {
                    user.FailedLoginAttempts += 1;
                    if (user.FailedLoginAttempts >= 3)
                    {
                        user.IsLockedOut = true;
                        user.LockoutUntil = DateTime.UtcNow.AddMinutes(30);
                        LastLoginError = "Account locked. Try again later.";
                    }
                    else
                    {
                        LastLoginError = "Invalid email or password.";
                    }

                    await context.SaveChangesAsync();
                    return null;
                }

                user.FailedLoginAttempts = 0;
                user.IsLockedOut = false;
                user.LockoutUntil = null;
                await context.SaveChangesAsync();

                CurrentUser = user;
                return user;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login error: {ex.Message}");
                LastLoginError = "Unable to sign in at the moment.";
                return null;
            }
        }

        public void Logout()
        {
            try
            {
                CurrentUser = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Logout error: {ex.Message}");
            }
        }

        public string HashPassword(string password)
        {
            try
            {
                using var sha256 = SHA256.Create();
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hash password error: {ex.Message}");
                throw;
            }
        }

        public bool VerifyPassword(string password, string hash)
        {
            try
            {
                var hashOfInput = HashPassword(password);
                return hashOfInput == hash;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Verify password error: {ex.Message}");
                return false;
            }
        }
    }
}
