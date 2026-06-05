using System.ComponentModel.DataAnnotations;

namespace KlawQ.Models
{
    public class Appointment
    {
        public int AppId { get; set; }
        public int UserId { get; set; }
        public required string Full_Name { get; set; } = string.Empty;
        public required string Social_Account { get; set; } = string.Empty;
        public required string Inspiration_Image { get; set; } = string.Empty;
        public string Additional_Notes { get; set; } = string.Empty;
        public required string Down_Payment { get; set; } = string.Empty;
        public int Reschedule_Count { get; set; } = 0;
        public required char Appointment_Type { get; set; } = 'S'; // H for home service and 'S' for studio-based service
        public int Status { get; set; }
    }

    public class Scheduler
    {
        [Key]
        public int SchedulerID { get; set; }
        [Required]
        public int AppId { get; set; }
        [Required]
        public required DateTime Appointment_Date { get; set; }
        [Required]
        public required DateTime Time_Slot { get; set; }
    }
}
