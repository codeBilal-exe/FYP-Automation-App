namespace FYP_AutomationSystem.Models
{
    public class RubricScore
    {
        public int Id { get; set; }
        public int EvaluationId { get; set; }
        public int RubricItemId { get; set; }
        public decimal ObtainedMarks { get; set; }

        // Navigation properties
        public RubricItem? RubricItem { get; set; }
    }
}
