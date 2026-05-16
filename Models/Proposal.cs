namespace FYP_AutomationSystem.Models
{
    public class Proposal
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Abstract { get; set; } = string.Empty;
        public string Objectives { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string Technologies { get; set; } = string.Empty;
        public string? GitHubUrl { get; set; }
        public ProposalStatus Status { get; set; }
        public string? RejectionReason { get; set; }
        public string? ReviewerComments { get; set; }

        public string? CurrentApprovalLevel { get; set; } = "supervisor";

        public string? DocumentPath { get; set; }
        public string? DocumentName { get; set; }
        public DateTime? DocumentUploadedAt { get; set; }

        public bool ApprovedByCoordinator { get; set; }
        public bool ApprovedByHOD { get; set; }
        public DateTime? CoordinatorApprovedAt { get; set; }
        public string? HODFeedback { get; set; }
        public DateTime? HODApprovedAt { get; set; }
        public DateTime? HODRejectedAt { get; set; }

        public int StudentId { get; set; }
        public int GroupId { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
