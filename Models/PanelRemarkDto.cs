namespace FYP_AutomationSystem.Models
{
    public class PanelRemarkDto
    {
        public int VivaSlotId { get; set; }
        public int PanelMemberId { get; set; }
        public string PanelMemberName { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public DateTime SavedAt { get; set; }
    }
}
