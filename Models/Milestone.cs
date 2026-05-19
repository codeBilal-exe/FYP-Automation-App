namespace FYP_AutomationSystem.Models
{
    public class Milestone
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public int? CreatedBySupervisorId { get; set; }
        public MilestoneStatus Status { get; set; }
        public int ProgressPercent { get; set; }
        public int ProjectId { get; set; }
        public string? SubmissionFilePath { get; set; }
        public string? SubmissionFileName { get; set; }
        public string? SubmissionNotes { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public int? SubmittedByStudentId { get; set; }
        // Durable storage in Postgres (see comment on Proposal.DocumentBytes).
        public byte[]? SubmissionBytes { get; set; }

        // Navigation properties
        public Project? Project { get; set; }
    }
}
