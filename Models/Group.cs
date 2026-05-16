namespace FYP_AutomationSystem.Models
{
    public class Group
    {
        public int Id { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public int SupervisorId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Semester { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public decimal? FinalGrade { get; set; }
        public string? LetterGrade { get; set; }
        public bool IsFinalGradeConfirmed { get; set; }

        // Navigation properties
        public User? Supervisor { get; set; }
        public ICollection<User> Members { get; set; } = new List<User>();
        public Project? Project { get; set; }
    }
}
