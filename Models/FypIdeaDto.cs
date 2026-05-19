namespace FYP_AutomationSystem.Models
{
    public class FypIdeaDto
    {
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string ProblemStatement { get; set; } = string.Empty;
        public string Technologies { get; set; } = string.Empty;
    }

    public class IdeaRequestDto
    {
        public string Domain { get; set; } = string.Empty;
        public string Technologies { get; set; } = string.Empty;
        public string ProblemArea { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string AdditionalNotes { get; set; } = string.Empty;
        public int IdeaCount { get; set; } = 4;
    }
}
