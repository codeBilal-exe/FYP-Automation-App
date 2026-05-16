namespace FYP_AutomationSystem.Models
{
    public class ProjectTask
    {
        public int Id { get; set; }
        public int ThreadId { get; set; }
        public int GroupId { get; set; }
        public int CreatedBySupervisorId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Deadline { get; set; }
        public string Priority { get; set; } = "Medium";
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; }
        public string? AssignToMemberIdsCsv { get; set; }
        public bool IsProgressUpdateDemand { get; set; }
        public string? ResourcePath { get; set; }
        public string? ResourceName { get; set; }
    }
}
