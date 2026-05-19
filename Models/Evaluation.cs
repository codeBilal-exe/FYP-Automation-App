namespace FYP_AutomationSystem.Models
{
    public class Evaluation
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public string ItemType { get; set; } = string.Empty;
        public int ProjectId { get; set; }
        public int EvaluatorId { get; set; }
        public string EvaluatorRole { get; set; } = string.Empty;
        public int GroupId { get; set; }
        public decimal Marks { get; set; }
        public string? Comment { get; set; }
        public decimal TotalScore { get; set; }
        public string Feedback { get; set; } = string.Empty;
        public bool IsLocked { get; set; }
        public DateTime EvaluatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public User? Evaluator { get; set; }
        public ICollection<RubricScore> RubricScores { get; set; } = new List<RubricScore>();
    }
}
