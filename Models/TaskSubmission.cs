namespace FYP_AutomationSystem.Models
{
    public class TaskSubmission
    {
        public int Id { get; set; }
        public int TaskId { get; set; }
        public int SubmittedByStudentId { get; set; }
        public string SubmissionText { get; set; } = string.Empty;
        public string? FilePath { get; set; }
        public string? FileName { get; set; }
        public DateTime SubmittedAt { get; set; }
        public string ReviewStatus { get; set; } = "Submitted";
        public string? Feedback { get; set; }
    }
}
