namespace FYP_AutomationSystem.Models
{
    public class ProjectThread
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public int ProjectId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime OverallDeadline { get; set; }
        public string Status { get; set; } = "Active";
    }
}
