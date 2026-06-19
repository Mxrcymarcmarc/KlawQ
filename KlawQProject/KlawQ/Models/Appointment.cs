using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KlawQ.Models
{
    /// <summary>
    /// Represents the encapsulated data structure for client bookings.
    /// Covers Encapsulation: Restricts direct state manipulation by exposing attributes via standard C# properties with accessors (getters/setters).
    /// </summary>
    public class Appointment
    {
        [Key]
        public int AppId { get; set; }
        public int UserId { get; set; }
        public required string Full_Name { get; set; } = string.Empty;
        public required string Social_Account { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Inspiration_Image { get; set; } = string.Empty;
        public string Additional_Notes { get; set; } = string.Empty;
        public bool Down_Payment_Paid { get; set; } = false; // Fixed rate: 150 pesos
        public int Reschedule_Count { get; set; } = 0;
        public required char Appointment_Type { get; set; } = 'S'; // H for home service and 'S' for studio-based service
        public int Status { get; set; }
        public decimal Price { get; set; } = 0m;
        public bool IsCustom { get; set; } = false;

        public Scheduler? Scheduler { get; set; }
    }

    /// <summary>
    /// Scheduler model tracking specific dates and time allocations.
    /// Covers Association (relationship): Maintains a one-to-one relationship reference back to the Appointment object model.
    /// </summary>
    public class Scheduler
    {
        [Key]
        public int SchedulerID { get; set; }
        [Required]
        [ForeignKey("Appointment")]
        public int AppId { get; set; }
        [Required]
        public required DateTime Appointment_Date { get; set; }
        [Required]
        public required DateTime Time_Slot { get; set; }

        public Appointment? Appointment { get; set; }
    }

    /// <summary>
    /// Data transfer object model capturing a customer's booking request parameters.
    /// Covers Encapsulation: Packages request attributes (appointment identifier, scheduled date, and time slot) into a single object wrapper.
    /// </summary>
    public class BookingRequest
    {
        public int AppId { get; set; }
        public DateTime Appointment_Date { get; set; }
        public DateTime Time_Slot { get; set; }
    }
}
