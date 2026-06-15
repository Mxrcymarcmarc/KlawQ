namespace KlawQ.Models
{
    public class BookingWithAppointmentRequest
    {
        // Schedule fields
        public DateTime Appointment_Date { get; set; }
        public DateTime Time_Slot { get; set; }

        // Appointment fields (saved only after payment succeeds)
        public string Full_Name { get; set; } = "";
        public string Social_Account { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Additional_Notes { get; set; } = "";
        public char Appointment_Type { get; set; } = 'S';
        public string? Inspiration_Image { get; set; }
    }
}
