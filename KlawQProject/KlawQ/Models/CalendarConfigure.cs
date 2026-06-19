namespace KlawQ.Models
{
    /// <summary>
    /// Model capturing administration blocks and calendar override configurations.
    /// Covers Encapsulation: Controls access to calendar rules, specifying target dates and blocked hourly slots.
    /// </summary>
    public class CalendarConfigure
    {
        public int Id { get; set; }
        public DateTime TargetDate { get; set; }

        // If Null, the entire day is closed. If it has a value (e.g., 10, 14), only that hour is blocked.
        public int? BlockedHour { get; set; }
    }
}