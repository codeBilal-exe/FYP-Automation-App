namespace FYP_AutomationSystem.Models
{
    public class Group
    {
        public int Id { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public int SupervisorId { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation properties
        public User? Supervisor { get; set; }
        public ICollection<User> Members { get; set; } = new List<User>();
        public Project? Project { get; set; }
    }
}
