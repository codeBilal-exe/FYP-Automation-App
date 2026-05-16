namespace FYP_AutomationSystem.Models
{
    public class GroupMessage
    {
        public int Id { get; set; }
        public int ThreadId { get; set; }
        public int SenderId { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }
    }
}
