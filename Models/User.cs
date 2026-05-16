namespace FYP_AutomationSystem.Models
{
    public class User
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public string? Expertise { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public int FailedLoginAttempts { get; set; } = 0;
        public bool IsLockedOut { get; set; } = false;
        public DateTime? LockoutUntil { get; set; }
    }
}
