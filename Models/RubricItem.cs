namespace FYP_AutomationSystem.Models
{
    public class RubricItem
    {
        public int Id { get; set; }
        public string Criterion { get; set; } = string.Empty;
        public decimal MaxMarks { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}
