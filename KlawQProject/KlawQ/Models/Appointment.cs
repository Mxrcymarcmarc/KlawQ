namespace KlawQ.Models
{
    public class Appointment
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string Service { get; set; }
        public DateTime Date { get; set; }
        public int Status { get; set; }
    }
}
