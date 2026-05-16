namespace FYP_AutomationSystem.Models
{
    public class GroupMessageThread
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public int SupervisorId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
