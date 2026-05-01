namespace FYP_AutomationSystem.Models
{
    public class VivaSlot
    {
        public int Id { get; set; }
        public DateTime ScheduledAt { get; set; }
        public string Venue { get; set; } = string.Empty;
        public int ProjectId { get; set; }
        public VivaStatus Status { get; set; }

        // Navigation properties
        public ICollection<User> PanelMembers { get; set; } = new List<User>();
    }
}
