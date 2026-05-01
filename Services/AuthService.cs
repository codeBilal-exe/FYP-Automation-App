using System.Security.Cryptography;
using System.Text;
using FYP_AutomationSystem.Data;
using FYP_AutomationSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace FYP_AutomationSystem.Services
{
    public class AuthService
    {
        private readonly AppDbContext _context;
        public static User? CurrentUser { get; set; }

        public AuthService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Authenticates user by email and password
        /// </summary>
        public async Task<User?> Login(string email, string password)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == email && u.IsActive);

                if (user == null)
                    return null;

                if (!VerifyPassword(password, user.PasswordHash))
                    return null;

                CurrentUser = user;
                return user;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Clears the current user session
        /// </summary>
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

        /// <summary>
        /// Hashes a password using SHA256
        /// </summary>
        public string HashPassword(string password)
        {
            try
            {
                using (var sha256 = SHA256.Create())
                {
                    var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                    return Convert.ToBase64String(hashedBytes);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hash password error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Verifies a password against its hash
        /// </summary>
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
