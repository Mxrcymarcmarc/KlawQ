using BCrypt.Net;
using Humanizer.Configuration;
using KlawQ.Data;
using KlawQ.Models;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MimeKit;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace KlawQ.Controllers
{
    /// <summary>
    /// Controller managing user registration processes, email verification, and security code dispatching.
    /// Covers Inheritance: Inherits from the base Controller class.
    /// Covers Abstraction: Interfaces with Identity UserManager and database contexts, hiding account validation and email protocols.
    /// </summary>
    public class RegisterController(
        ApplicationDbContext context,
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        IConfiguration configuration) : Controller
    {
        private readonly ApplicationDbContext _context = context;
        private readonly SignInManager<IdentityUser> _signInManager = signInManager;
        private readonly UserManager<IdentityUser> _userManager = userManager;
        private readonly IConfiguration _configuration = configuration;

        // WEB VIEW ENDPOINT: Display Registration Page.
        // Covers Polymorphism: Returns IActionResult (resolving to ViewResult).
        [HttpGet]
        public IActionResult Register()
        {
            return View("~/Views/Account/Register.cshtml");
        }

        // POST ACTION: Process Details & Send Verification Code.
        // Covers Encapsulation: Validates the model parameters and tests password requirements against the identity validators list before saving verification credentials to the Session and sending the verification email.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("~/Views/Account/Register.cshtml", model);
            }

            // Check if email already exists in DB
            if (_context.Users.Any(u => u.Email == model.Email))
            {
                ModelState.AddModelError("Email", "This email address is already registered.");
                return View("~/Views/Account/Register.cshtml", model);
            }

            try
            {
                // Pre-validate the password against Identity's configured rules before sending the email to avoid unnecessary email dispatches for invalid passwords
                var temporaryUser = new IdentityUser { UserName = model.Email, Email = model.Email };
                foreach (var validator in _userManager.PasswordValidators)
                {
                    var validationResult = await validator.ValidateAsync(_userManager, temporaryUser, model.Password);
                    if (!validationResult.Succeeded)
                    {
                        // If password rules fail (like missing a non-alphanumeric char), trap it here on the Register page!
                        foreach (var error in validationResult.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }
                        return View("~/Views/Account/Register.cshtml", model);
                    }
                }

                // Generate a random 6-digit verification code
                string verificationCode = new Random().Next(100000, 999999).ToString();

                // Store registration data safely in Server Session
                HttpContext.Session.SetString("Reg_FullName", model.FullName);
                HttpContext.Session.SetString("Reg_Email", model.Email);
                HttpContext.Session.SetString("Reg_Password", model.Password);
                HttpContext.Session.SetString("Reg_VerificationCode", verificationCode);

                // Send the email code 
                SendEmailCode(model.Email, verificationCode);

                return RedirectToAction("VerifyEmail", new { email = model.Email });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View("~/Views/Account/Register.cshtml", model);
            }
        }

        // WEB VIEW ENDPOINT: Display Verification Code Screen.
        // Covers Abstraction: Hides verification flow setup behind a simple VerifyCodeViewModel.
        [HttpGet]
        public IActionResult VerifyEmail(string email)
        {
            var model = new VerifyCodeViewModel { Email = email };
            return View("~/Views/Account/VerifyEmail.cshtml", model);
        }

        // POST ACTION: Confirm Code & Commit User to Database.
        // Covers Encapsulation: Performs verification check of the submitted code against the cached code and updates the Identity database user profile atomically.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyEmail(VerifyCodeViewModel model)
        {
            // Wipes lingering validation states so code processing is 100% clean
            ModelState.Clear();

            if (!TryValidateModel(model))
            {
                return View("~/Views/Account/VerifyEmail.cshtml", model);
            }

            // Retrieve stored data from Session
            string? cachedCode = HttpContext.Session.GetString("Reg_VerificationCode");
            string? cachedEmail = HttpContext.Session.GetString("Reg_Email");
            string? cachedFullName = HttpContext.Session.GetString("Reg_FullName");

            if (string.IsNullOrEmpty(cachedCode) || cachedEmail != model.Email)
            {
                ModelState.AddModelError(string.Empty, "Verification session expired. Please register again.");
                return View("~/Views/Account/VerifyEmail.cshtml", model);
            }

            // Check if the user entered the correct code
            if (model.Code != cachedCode)
            {
                // Returns a direct 400 Bad Request error if code is wrong so the AJAX handler in your View knows NOT to show the success popup!
                return BadRequest("Invalid verification code.");
            }

            try
            {
                // Create the IdentityUser account first to leverage Identity's password hashing and user management features. This ensures that the password is stored securely and that the user can log in immediately after verification without needing a separate login step.
                var identityUser = new IdentityUser
                {
                    UserName = cachedEmail,
                    Email = cachedEmail,
                    EmailConfirmed = true
                };

                // Retrieve the plain password from session to create the IdentityUser. This is necessary because Identity needs the plain password to generate the PasswordHash and store it securely.
                string? plainPassword = HttpContext.Session.GetString("Reg_Password");
                if (string.IsNullOrEmpty(plainPassword))
                {
                    return BadRequest("Registration session expired. Please register again.");
                }

                var createResult = await _userManager.CreateAsync(identityUser, plainPassword);
                if (!createResult.Succeeded)
                {
                    return BadRequest(createResult.Errors.First().Description);
                }

                // Assign default role
                await _userManager.AddToRoleAsync(identityUser, "User");

                // Refresh identityUser to read the generated PasswordHash and Id
                var createdUser = await _userManager.FindByEmailAsync(cachedEmail);

                // Map the data to custom profile entity 
                var customUserProfile = new KlawQ.Models.Users
                {
                    Full_Name = cachedFullName!,
                    Email = cachedEmail!,
                    PasswordHash = createdUser?.PasswordHash ?? string.Empty,
                    Role = "User",
                    IdentityUserId = createdUser?.Id
                };

                // Save the custom user profile to your application's database
                _context.UserProfiles.Add(customUserProfile);
                await _context.SaveChangesAsync();

                // Clear session data
                HttpContext.Session.Remove("Reg_FullName");
                HttpContext.Session.Remove("Reg_Email");
                HttpContext.Session.Remove("Reg_Password");
                HttpContext.Session.Remove("Reg_VerificationCode");

                TempData["SuccessMessage"] = "Email verified! Registration successful.";

                // Return redirect target back to the fetch pipeline smoothly
                return Ok(new { redirectUrl = Url.Action("Login", "Account") });
            }
            catch (Exception)
            {
                return StatusCode(500, "Error finalizing your account database entry.");
            }
        }

        // PRIVATE UTILITY: Sends emails via MailKit.
        // Covers Abstraction: Employs SMTP protocol components (MimeMessage, SmtpClient) to dispatch verification codes without exposing the transport configurations.
        private static void SendEmailCode(string email, string code)
        {
            // NOTE: For production use, it's critical to store email credentials securely (e.g., in environment variables or a secrets manager) rather than hardcoding them. This example uses hardcoded values for demonstration purposes only.
            const string Gmail = "klawqwebapp@gmail.com";
            const string AppPassword = "tqwc jyao ujds bedm";

            // Construct the email message with a visually appealing HTML template
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Klaw By Krys", Gmail));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = "Your KlawQ Verification Code";

            // The HTML body includes a clean design with clear instructions and a prominently displayed verification code. It also has a note about the code's expiration and a disclaimer for unintended recipients.
            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = $@"
                    <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #eee; max-width: 500px; margin: 0 auto;'>
                        <h2 style='color: #333;'>Welcome to Klaw By Krys!</h2>
                        <p>Thank you for signing up. Please use the following 6-digit verification code to complete your registration:</p>
                        <div style='font-size: 28px; font-weight: bold; color: #d63384; letter-spacing: 4px; padding: 15px; background-color: #f8f9fa; text-align: center; border-radius: 5px; margin: 20px 0;'>
                            {code}
                        </div>
                        <p style='color: #666;'>This code will expire in 15 minutes.</p>
                        <hr style='border: none; border-top: 1px solid #eee;' />
                        <p style='font-size: 11px; color: #999; text-align: center;'>If you did not request this code, you can safely ignore this email.</p>
                    </div>"
            };
            // The BodyBuilder allows us to easily construct a rich HTML email with inline styles for better presentation. The verification code is prominently displayed in a styled box to ensure it catches the user's attention.
            message.Body = bodyBuilder.ToMessageBody();

            // Send the email using MailKit's SmtpClient. We connect to Gmail's SMTP server with STARTTLS for security, authenticate with the provided credentials, and send the message. If any errors occur during this process, we catch the exception and log it for debugging while also providing a user-friendly error message.
            using var client = new SmtpClient();
            // For development/testing, you might want to use a service like Mailtrap or Ethereal Email to avoid sending real emails. In that case, you would change the SMTP server and credentials accordingly.
            try
            {
                // Connect to Gmail's SMTP server with STARTTLS for secure email transmission
                client.Connect("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
                client.Authenticate(Gmail, AppPassword);
                client.Send(message);
                client.Disconnect(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Email dispatch error: {ex.Message}");
                throw new Exception("We encountered an issue sending your verification email. Please check your credentials.");
            }
        }

        // Display Login Page (for redirect after successful registration)
        [HttpGet]
        public IActionResult Login()
        {
            return View("~/Views/Account/Login.cshtml");
        }

        // POST ACTION: Resends the verification code.
        // Covers Encapsulation: Ensures that session variables are verified before generating a new validation code and updating registration states.
        [HttpPost]
        [Route("Account/ResendCode")]
        public IActionResult ResendCode()
        {
            // Retrieve the email from session to resend the code. If the email is missing, it means the session has expired or the user navigated away, so we return an error.
            string? cachedEmail = HttpContext.Session.GetString("Reg_Email");

            // If the email is not found in session, it indicates that the registration session has expired or the user has navigated away from the registration flow. In this case, we return a 400 Bad Request response with a message prompting the user to start the registration process again. This allows the frontend to handle this scenario gracefully, such as by showing a notification and redirecting the user back to the registration page.
            if (string.IsNullOrEmpty(cachedEmail))
            {
                return BadRequest("Session expired. Please register again.");
            }
            // If the email is found, we proceed to generate a new verification code, update the session with the new code, and send the email. If any errors occur during this process, we catch the exception and return a 500 Internal Server Error response with the error message.
            try
            {
                string newCode = new Random().Next(100000, 999999).ToString();
                HttpContext.Session.SetString("Reg_VerificationCode", newCode);

                SendEmailCode(cachedEmail, newCode);

                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}