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

namespace KlawQ.Controllers
{
    public class RegisterController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IConfiguration _configuration;

        // Constructor cleanly handles dependency injection for everything you need
        public RegisterController(
            ApplicationDbContext context,
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager,
            IConfiguration configuration)
        {
            _context = context;
            _signInManager = signInManager;
            _userManager = userManager;
            _configuration = configuration;
        }

        // Display Registration Page
        [HttpGet]
        public IActionResult Register()
        {
            return View("~/Views/Account/Register.cshtml");
        }

        // Process Details & Send Verification Code
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(RegisterViewModel model)
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
                // Generate a random 6-digit verification code
                string verificationCode = new Random().Next(100000, 999999).ToString();

                // Store the plain password briefly in session for account creation after verification.
                // Session is server-side and will be cleared immediately after use.
                HttpContext.Session.SetString("Reg_FullName", model.FullName);
                HttpContext.Session.SetString("Reg_Email", model.Email);
                HttpContext.Session.SetString("Reg_Password", model.Password);
                HttpContext.Session.SetString("Reg_VerificationCode", verificationCode);

                // Send the email code 
                SendEmailCode(model.Email, verificationCode);

                // Redirect to the verification entry screen
                return RedirectToAction("VerifyEmail", new { email = model.Email });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View("~/Views/Account/Register.cshtml", model);
            }
        }

        // Display Verification Code Screen
        [HttpGet]
        public IActionResult VerifyEmail(string email)
        {
            var model = new VerifyCodeViewModel { Email = email };
            return View("~/Views/Account/VerifyEmail.cshtml", model);
        }

        // Confirm Code & Commit User to Database
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyEmail(VerifyCodeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("~/Views/Account/VerifyEmail.cshtml", model);
            }

            // Retrieve stored data from Session
            string? cachedCode = HttpContext.Session.GetString("Reg_VerificationCode");
            string? cachedEmail = HttpContext.Session.GetString("Reg_Email");
            string? cachedFullName = HttpContext.Session.GetString("Reg_FullName");

            // Ensure session hasn't expired and email matches
            if (string.IsNullOrEmpty(cachedCode) || cachedEmail != model.Email)
            {
                ModelState.AddModelError(string.Empty, "Verification session expired. Please register again.");
                return View("~/Views/Account/VerifyEmail.cshtml", model);
            }

            // Check if the user entered the correct code
            if (model.Code != cachedCode)
            {
                ModelState.AddModelError("Code", "Invalid verification code.");
                return View("~/Views/Account/VerifyEmail.cshtml", model);
            }

            try
            {
                // Create Identity user using UserManager so password and security stamps are handled
                var identityUser = new IdentityUser
                {
                    UserName = cachedEmail,
                    Email = cachedEmail,
                    EmailConfirmed = true
                };

                // Retrieve plain password stored in session
                string? plainPassword = HttpContext.Session.GetString("Reg_Password");
                if (string.IsNullOrEmpty(plainPassword))
                {
                    ModelState.AddModelError(string.Empty, "Registration session expired. Please register again.");
                    return View("~/Views/Account/VerifyEmail.cshtml", model);
                }

                var createResult = await _userManager.CreateAsync(identityUser, plainPassword);
                if (!createResult.Succeeded)
                {
                    foreach (var err in createResult.Errors) ModelState.AddModelError(string.Empty, err.Description);
                    return View("~/Views/Account/VerifyEmail.cshtml", model);
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

                // Save the custom profile to its specific table
                _context.UserProfiles.Add(customUserProfile);
                await _context.SaveChangesAsync();

                // Clear session data
                HttpContext.Session.Remove("Reg_FullName");
                HttpContext.Session.Remove("Reg_Email");
                HttpContext.Session.Remove("Reg_Password");
                HttpContext.Session.Remove("Reg_VerificationCode");

                TempData["SuccessMessage"] = "Email verified! Registration successful.";

                return RedirectToAction("Login", "Account");
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Error finalizing your account database entry.");
                return View("~/Views/Account/VerifyEmail.cshtml", model);
            }
        }

        // Real functional method for sending emails via MailKit
        private void SendEmailCode(string email, string code)
        {

            string Gmail = "klawqwebapp@gmail.com";
            string AppPassword = "tqwc jyao ujds bedm";

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Klaw By Krys", Gmail));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = "Your KlawQ Verification Code";

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
            message.Body = bodyBuilder.ToMessageBody();

            using (var client = new SmtpClient())
            {
                try
                {
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
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View("~/Views/Account/Login.cshtml");
        }
    }
}