namespace KlawQ.Models
{
    /// <summary>
    /// Model representing the availability status of a specific calendar day.
    /// Covers Encapsulation: Bundles date string formatting and availability state flags as self-contained properties.
    /// </summary>
    public class CalendarDayStatus
    {
        public string DateString { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
    }
}