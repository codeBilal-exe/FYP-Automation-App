namespace FYP_AutomationSystem.Models
{
    public class PanelRemark
    {
        public int Id { get; set; }
        public int VivaSlotId { get; set; }
        public int PanelMemberId { get; set; }
        public int GroupId { get; set; }
        public string Remarks { get; set; } = string.Empty;
        public int? Rating { get; set; } // 1-5 star rating
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public VivaSlot? VivaSlot { get; set; }
        public User? PanelMember { get; set; }
        public Group? Group { get; set; }
    }
}
