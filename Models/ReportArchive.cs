namespace FYP_AutomationSystem.Models
{
    public class ReportArchive
    {
        public int Id { get; set; }
        public string ReportType { get; set; } = string.Empty;
        public string Semester { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public int GeneratedByUserId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string FileFormat { get; set; } = string.Empty;
    }
}
