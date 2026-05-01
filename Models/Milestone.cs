namespace FYP_AutomationSystem.Models
{
    public class Milestone
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public MilestoneStatus Status { get; set; }
        public int ProgressPercent { get; set; }
        public int ProjectId { get; set; }

        // Navigation properties
        public Project? Project { get; set; }
    }
}
