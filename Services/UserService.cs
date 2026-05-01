using FYP_AutomationSystem.Data;
using FYP_AutomationSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace FYP_AutomationSystem.Services
{
    public class UserService
    {
        private readonly AppDbContext _context;
        private readonly AuthService _authService;

        public UserService(AppDbContext context, AuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        /// <summary>
        /// Retrieves all active users
        /// </summary>
        public async Task<List<User>> GetAllUsers()
        {
            try
            {
                return await _context.Users
                    .Where(u => u.IsActive)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get all users error: {ex.Message}");
                return new List<User>();
            }
        }

        /// <summary>
        /// Retrieves a user by ID
        /// </summary>
        public async Task<User?> GetUserById(int id)
        {
            try
            {
                return await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get user by id error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates a new user with hashed password
        /// </summary>
        public async Task<User?> CreateUser(string fullName, string email, string password, UserRole role)
        {
            try
            {
                // Check if user already exists
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == email);

                if (existingUser != null)
                {
                    Console.WriteLine("User with this email already exists");
                    return null;
                }

                var user = new User
                {
                    FullName = fullName,
                    Email = email,
                    PasswordHash = _authService.HashPassword(password),
                    Role = role,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                return user;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Create user error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Updates user details
        /// </summary>
        public async Task<bool> UpdateUser(int id, string fullName, string email, string? expertise = null)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
                if (user == null)
                    return false;

                // Check if email is unique (excluding current user)
                var existingEmail = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == email && u.Id != id);

                if (existingEmail != null)
                    return false;

                user.FullName = fullName;
                user.Email = email;
                if (!string.IsNullOrEmpty(expertise))
                    user.Expertise = expertise;

                _context.Users.Update(user);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update user error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Deactivates a user (soft delete)
        /// </summary>
        public async Task<bool> DeactivateUser(int id)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
                if (user == null)
                    return false;

                user.IsActive = false;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Deactivate user error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Retrieves users by role
        /// </summary>
        public async Task<List<User>> GetUsersByRole(UserRole role)
        {
            try
            {
                return await _context.Users
                    .Where(u => u.Role == role && u.IsActive)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get users by role error: {ex.Message}");
                return new List<User>();
            }
        }

        /// <summary>
        /// Changes user password
        /// </summary>
        public async Task<bool> ChangePassword(int id, string newPassword)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
                if (user == null)
                    return false;

                user.PasswordHash = _authService.HashPassword(newPassword);
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Change password error: {ex.Message}");
                return false;
            }
        }
    }
}
