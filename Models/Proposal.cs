namespace FYP_AutomationSystem.Models
{
    public class Proposal
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Abstract { get; set; } = string.Empty;
        public string Objectives { get; set; } = string.Empty;
        public ProposalStatus Status { get; set; }
        public string? RejectionReason { get; set; }
        public int StudentId { get; set; }
        public int GroupId { get; set; }
        public DateTime SubmittedAt { get; set; }
    }
}
