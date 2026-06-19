using System.ComponentModel.DataAnnotations;

namespace KlawQ.Models
{
    /// <summary>
    /// Model representing custom application user profiles.
    /// Covers Encapsulation: Protects user credentials (email, name, role, password hash parameters) via standard C# property interfaces and validation attributes.
    /// </summary>
    public class Users
    {
        [Key]
        public int UserID { get; set; }

        [Required]
        public required string Full_Name { get; set; } = string.Empty;

        [Required, EmailAddress]
        public required string Email { get; set; } = string.Empty;

        [Required]
        public required string PasswordHash { get; set; } = string.Empty;

        [Required]
        public required string Role { get; set; } = string.Empty; // e.g., "Admin", "Client"

        // Optional link to Identity user
        public string? IdentityUserId { get; set; }
    }
}
