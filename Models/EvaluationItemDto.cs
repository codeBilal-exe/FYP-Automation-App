namespace FYP_AutomationSystem.Models
{
    public class EvaluationItemDto
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public int GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime? ScheduledDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool CanEvaluate { get; set; }
        public decimal? ExistingMarks { get; set; }
        public string? ExistingComment { get; set; }
        public bool IsEvaluated => ExistingMarks.HasValue;
    }
}
