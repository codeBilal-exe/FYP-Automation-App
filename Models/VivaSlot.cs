namespace FYP_AutomationSystem.Models
{
    public class VivaSlot
    {
        public int Id { get; set; }
        public DateTime ScheduledAt { get; set; }
        public string Venue { get; set; } = string.Empty;
        public int ProjectId { get; set; }
        public VivaStatus Status { get; set; }

        // Enhanced scheduling fields
        public int? GroupId { get; set; }
        public int? MilestoneId { get; set; }
        public SlotType SlotType { get; set; } = SlotType.Viva;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        // Navigation properties
        public Group? Group { get; set; }
        public Milestone? Milestone { get; set; }
        public ICollection<User> PanelMembers { get; set; } = new List<User>();
    }
}
