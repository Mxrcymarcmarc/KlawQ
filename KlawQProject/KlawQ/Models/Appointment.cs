using System.ComponentModel.DataAnnotations;

namespace KlawQ.Models
{
    public class Appointment
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Full_Name { get; set; } = string.Empty;
        public string Service { get; set; }
        public DateTime Date { get; set; }
        public int Status { get; set; }
    }
}
