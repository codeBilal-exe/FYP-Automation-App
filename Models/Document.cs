namespace FYP_AutomationSystem.Models
{
    public class Document
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public int Version { get; set; }
        public int ProjectId { get; set; }
        public int UploadedById { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}
