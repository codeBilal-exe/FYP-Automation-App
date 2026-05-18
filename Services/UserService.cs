using FYP_AutomationSystem.Data;
using FYP_AutomationSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace FYP_AutomationSystem.Services
{
    public class UserService
    {
        private readonly AppDbContext _context;
        private readonly AuthService _authService;
        private readonly SupabaseAuthSyncService _supabaseAuthSyncService;

        public UserService(AppDbContext context, AuthService authService, SupabaseAuthSyncService supabaseAuthSyncService)
        {
            _context = context;
            _authService = authService;
            _supabaseAuthSyncService = supabaseAuthSyncService;
        }

        public sealed class CreateUserResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public User? User { get; set; }
        }

        public sealed class DeleteUserResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
        }

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

        public async Task<CreateUserResult> CreateUserDetailed(string fullName, string email, string password, UserRole role)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fullName))
                    return new CreateUserResult { Success = false, Message = "Full name is required." };

                if (string.IsNullOrWhiteSpace(email))
                    return new CreateUserResult { Success = false, Message = "Email is required." };

                if (string.IsNullOrWhiteSpace(password) || password.Trim().Length < 6)
                    return new CreateUserResult { Success = false, Message = "Password must be at least 6 characters." };

                var normalizedEmail = email.Trim().ToLowerInvariant();
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
                if (existingUser != null)
                {
                    if (!existingUser.IsActive)
                    {
                        var reactivationSync = await _supabaseAuthSyncService.EnsureUserSynced(
                            normalizedEmail,
                            password,
                            fullName.Trim(),
                            role);
                        if (!reactivationSync.Success)
                        {
                            return new CreateUserResult { Success = false, Message = reactivationSync.Message };
                        }

                        existingUser.FullName = fullName.Trim();
                        existingUser.PasswordHash = _authService.HashPassword(password);
                        existingUser.Role = role;
                        existingUser.IsActive = true;
                        existingUser.IsLockedOut = false;
                        existingUser.FailedLoginAttempts = 0;
                        existingUser.LockoutUntil = null;

                        _context.Users.Update(existingUser);
                        await _context.SaveChangesAsync();

                        return new CreateUserResult
                        {
                            Success = true,
                            Message = "Existing inactive user reactivated successfully.",
                            User = existingUser
                        };
                    }

                    return new CreateUserResult { Success = false, Message = "A user with this email already exists." };
                }

                var passwordHash = _authService.HashPassword(password);
                if (string.IsNullOrWhiteSpace(passwordHash))
                    return new CreateUserResult { Success = false, Message = "Failed to hash password." };

                var syncResult = await _supabaseAuthSyncService.EnsureUserSynced(normalizedEmail, password, fullName.Trim(), role);
                if (!syncResult.Success)
                {
                    return new CreateUserResult { Success = false, Message = syncResult.Message };
                }

                var user = new User
                {
                    FullName = fullName.Trim(),
                    Email = normalizedEmail,
                    PasswordHash = passwordHash,
                    Role = role,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    FailedLoginAttempts = 0,
                    IsLockedOut = false,
                    LockoutUntil = null
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                return new CreateUserResult
                {
                    Success = true,
                    Message = "User created successfully.",
                    User = user
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Create user error: {ex}");
                return new CreateUserResult
                {
                    Success = false,
                    Message = "Unable to create user right now. Please try again."
                };
            }
        }

        public async Task<User?> CreateUser(string fullName, string email, string password, UserRole role)
        {
            var result = await CreateUserDetailed(fullName, email, password, role);
            return result.User;
        }

        public async Task<bool> UpdateUser(int id, string fullName, string email, string? expertise = null)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
                if (user == null)
                    return false;

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

        public async Task<bool> DeactivateUser(int id)
        {
            var result = await DeleteUserDetailed(id);
            return result.Success;
        }

        public async Task<DeleteUserResult> DeleteUserDetailed(int id)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
                if (user == null)
                    return new DeleteUserResult { Success = false, Message = "User not found." };

                if (user.Role == UserRole.Admin)
                {
                    var activeAdmins = await _context.Users.CountAsync(u => u.Role == UserRole.Admin && u.IsActive);
                    if (activeAdmins <= 1)
                    {
                        return new DeleteUserResult { Success = false, Message = "Cannot delete the last active admin." };
                    }
                }

                if (user.Role == UserRole.Supervisor)
                {
                    var assignedGroups = await _context.Groups.CountAsync(g => g.SupervisorId == user.Id);
                    if (assignedGroups > 0)
                    {
                        return new DeleteUserResult
                        {
                            Success = false,
                            Message = $"Cannot delete supervisor. Reassign or remove {assignedGroups} assigned group(s) first."
                        };
                    }
                }

                var groupsWithLead = await _context.Groups.Where(g => g.GroupLeadId == user.Id).ToListAsync();
                foreach (var g in groupsWithLead)
                {
                    g.GroupLeadId = null;
                }

                var groupsWithMember = await _context.Groups
                    .Include(g => g.Members)
                    .Where(g => g.Members.Any(m => m.Id == user.Id))
                    .ToListAsync();

                foreach (var g in groupsWithMember)
                {
                    var member = g.Members.FirstOrDefault(m => m.Id == user.Id);
                    if (member != null)
                    {
                        g.Members.Remove(member);
                    }
                }

                var vivaSlots = await _context.VivaSlots
                    .Include(v => v.PanelMembers)
                    .Where(v => v.PanelMembers.Any(p => p.Id == user.Id))
                    .ToListAsync();

                foreach (var slot in vivaSlots)
                {
                    var panelMember = slot.PanelMembers.FirstOrDefault(p => p.Id == user.Id);
                    if (panelMember != null)
                    {
                        slot.PanelMembers.Remove(panelMember);
                    }
                }

                var notifications = await _context.Notifications.Where(n => n.RecipientId == user.Id).ToListAsync();
                if (notifications.Count > 0)
                {
                    _context.Notifications.RemoveRange(notifications);
                }

                var evaluations = await _context.Evaluations.Where(e => e.EvaluatorId == user.Id).ToListAsync();
                if (evaluations.Count > 0)
                {
                    _context.Evaluations.RemoveRange(evaluations);
                }

                var authDelete = await _supabaseAuthSyncService.DeleteAuthUserByEmail(user.Email);
                if (!authDelete.Success)
                {
                    return new DeleteUserResult { Success = false, Message = authDelete.Message };
                }

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                return new DeleteUserResult { Success = true, Message = "User deleted successfully." };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Deactivate user error: {ex.Message}");
                return new DeleteUserResult
                {
                    Success = false,
                    Message = "Unable to delete user due to linked records. Reassign dependencies and try again."
                };
            }
        }

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
