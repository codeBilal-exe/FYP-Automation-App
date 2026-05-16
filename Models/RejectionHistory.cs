namespace FYP_AutomationSystem.Models
{
    public class RejectionHistory
    {
        public int Id { get; set; }
        public int ProposalId { get; set; }
        public int GroupId { get; set; }
        public int RejectedByUserId { get; set; }
        public string RejectedByRole { get; set; } = string.Empty;
        public string RejectionReason { get; set; } = string.Empty;
        public DateTime RejectedAt { get; set; }
        public int? ResubmissionId { get; set; }
        public bool? ResubmissionImproved { get; set; }
    }
}
