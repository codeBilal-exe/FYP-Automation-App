using System.ComponentModel.DataAnnotations.Schema;

namespace FYP_AutomationSystem.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public int RecipientId { get; set; }
        public string RecipientRole { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        [NotMapped]
        public string Message
        {
            get => Description;
            set => Description = value;
        }

        public NotificationType Type { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string? ReferenceId { get; set; }
        public string? LinkUrl { get; set; }

        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime SentAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
