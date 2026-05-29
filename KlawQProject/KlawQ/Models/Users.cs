using System.ComponentModel.DataAnnotations;

namespace KlawQ.Models
{
    public class Users
    {
        public int UserID { get; set; }
        public required string Full_Name { get; set; } = string.Empty;
        public required string Email { get; set; } = string.Empty;
        public required string PasswordHash { get; set; } = string.Empty;
        public required string Role { get; set; } = string.Empty; // e.g., "Admin", "Client"
    }
}
