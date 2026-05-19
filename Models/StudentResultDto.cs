namespace FYP_AutomationSystem.Models
{
    public class StudentResultDto
    {
        public string GroupName { get; set; } = string.Empty;
        public List<TaskResultDto> Tasks { get; set; } = new();
        public decimal TotalMarks { get; set; }
        public decimal MaxMarks { get; set; }
        public decimal Percentage => MaxMarks > 0 ? Math.Round((TotalMarks / MaxMarks) * 100, 1) : 0;
    }

    public class TaskResultDto
    {
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime? Date { get; set; }
        public List<EvaluatorMarkDto> Marks { get; set; } = new();
        public decimal AverageMark => Marks.Any() ? Math.Round(Marks.Average(m => m.Marks), 1) : 0;
    }

    public class EvaluatorMarkDto
    {
        public string EvaluatorRole { get; set; } = string.Empty;
        public decimal Marks { get; set; }
        public string? Comment { get; set; }
        public DateTime EvaluatedAt { get; set; }
    }
}
