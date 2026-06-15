namespace KlawQ.Models
{
    public class CalendarConfigure
    {
        public int Id { get; set; }
        public DateTime TargetDate { get; set; }

        // If Null, the entire day is closed. If it has a value (e.g., 10, 14), only that hour is blocked.
        public int? BlockedHour { get; set; }
    }
}