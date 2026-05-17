namespace FYP_AutomationSystem.Models
{
    public class FacultyTimetable
    {
        public int Id { get; set; }
        public int FacultyId { get; set; }
        public DayOfWeek Day { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string RoomNumber { get; set; } = string.Empty;

        // Navigation
        public User? Faculty { get; set; }
    }
}
