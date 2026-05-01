namespace FYP_AutomationSystem.Models
{
    public class Project
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? GitHubUrl { get; set; }
        public ProjectStatus Status { get; set; }
        public int GroupId { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation properties
        public Group? Group { get; set; }
        public ICollection<Milestone> Milestones { get; set; } = new List<Milestone>();
        public ICollection<Document> Documents { get; set; } = new List<Document>();
    }
}
