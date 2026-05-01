namespace FYP_AutomationSystem.Models
{
    public class PlagiarismReport
    {
        public int Id { get; set; }
        public int DocumentId { get; set; }
        public decimal SimilarityPercent { get; set; }
        public string ReportDetails { get; set; } = string.Empty;
        public DateTime CheckedAt { get; set; }
    }
}
