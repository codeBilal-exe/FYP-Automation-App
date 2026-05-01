namespace FYP_AutomationSystem.Models
{
    public class Evaluation
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public int EvaluatorId { get; set; }
        public decimal TotalScore { get; set; }
        public string Feedback { get; set; } = string.Empty;
        public bool IsLocked { get; set; }
        public DateTime EvaluatedAt { get; set; }

        // Navigation properties
        public User? Evaluator { get; set; }
        public ICollection<RubricScore> RubricScores { get; set; } = new List<RubricScore>();
    }
}
