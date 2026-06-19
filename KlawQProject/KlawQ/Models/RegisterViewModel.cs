using System.ComponentModel.DataAnnotations;

namespace KlawQ.Models
{
    /// <summary>
    /// ViewModel representing user input properties during signup registration.
    /// Covers Encapsulation: Restricts field criteria using property validation checks (Compare, EmailAddress, Password length constraint).
    /// </summary>
    public class RegisterViewModel
    {
        [Required]
        [Display(Name = "Username")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [StringLength(100, ErrorMessage = "Password must be at least {2} characters long.", MinimumLength = 6)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    /// <summary>
    /// ViewModel representing email verification code validation input parameters.
    /// Covers Encapsulation: Restricts the verification code format rules to a 6-digit check before processing requests.
    /// </summary>
    public class VerifyCodeViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "The verification code must be 6 digits.")]
        [Display(Name = "Verification Code")]
        public string Code { get; set; } = string.Empty;
    }
}