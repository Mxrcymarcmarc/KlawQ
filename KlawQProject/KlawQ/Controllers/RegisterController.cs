using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using KlawQ.Models;
using KlawQ.Data;
using BCrypt.Net;
using System;
using System.Linq;

namespace KlawQ.Controllers
{
    public class RegisterController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RegisterController(ApplicationDbContext context)
        {
            _context = context;
        }

        // STEP 1: Display Registration Page
        [HttpGet]
        public IActionResult Register() => View();

        // STEP 1 POST: Process Details & Send Verification Code
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // Check if email already exists in DB
            if (_context.Users.Any(u => u.Email == model.Email))
            {
                ModelState.AddModelError("Email", "This email address is already registered.");
                return View(model);
            }

            try
            {
                // Generate a random 6-digit verification code
                string verificationCode = new Random().Next(100000, 999999).ToString();

                // Hash the password now so it's secure while sitting in the session
                string secureHash = BCrypt.Net.BCrypt.HashPassword(model.Password);

                // Save registration info and code to Session variables
                HttpContext.Session.SetString("Reg_FullName", model.FullName);
                HttpContext.Session.SetString("Reg_Email", model.Email);
                HttpContext.Session.SetString("Reg_PasswordHash", secureHash);
                HttpContext.Session.SetString("Reg_VerificationCode", verificationCode);

                // Send the email code (Call your email service here)
                SendEmailCode(model.Email, verificationCode);

                // Redirect to the verification entry screen
                return RedirectToAction("VerifyEmail", new { email = model.Email });
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "An error occurred. Please try again.");
                return View(model);
            }
        }

        // STEP 2: Display Verification Code Screen
        [HttpGet]
        public IActionResult VerifyEmail(string email)
        {
            var model = new VerifyCodeViewModel { Email = email };
            return View(model);
        }

        // STEP 2 POST: Confirm Code & Commit User to Database
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult VerifyEmail(VerifyCodeViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // Retrieve stored data from Session
            string? cachedCode = HttpContext.Session.GetString("Reg_VerificationCode");
            string? cachedEmail = HttpContext.Session.GetString("Reg_Email");
            string? cachedFullName = HttpContext.Session.GetString("Reg_FullName");
            string? cachedHash = HttpContext.Session.GetString("Reg_PasswordHash");

            // Ensure session hasn't expired and email matches
            if (string.IsNullOrEmpty(cachedCode) || cachedEmail != model.Email)
            {
                ModelState.AddModelError(string.Empty, "Verification session expired. Please register again.");
                return View(model);
            }

            // Check if the user entered the correct code
            if (model.Code != cachedCode)
            {
                ModelState.AddModelError("Code", "Invalid verification code.");
                return View(model);
            }

            try
            {
                // 1. Create the Microsoft Identity user that _context.Users expects
                var identityUser = new Microsoft.AspNetCore.Identity.IdentityUser
                {
                    UserName = cachedEmail,
                    Email = cachedEmail,
                    EmailConfirmed = true // They just verified via email code!
                };

                // 2. Add the Identity User to the context first
                _context.Users.Add(identityUser);

                // 3. Map the data to your leader's custom profile entity 
                // (Assuming your DbContext has a separate DbSet for your custom table, like CustomUsers or AppUsers)
                var customUserProfile = new KlawQ.Models.Users
                {
                    Full_Name = cachedFullName!,
                    Email = cachedEmail!,
                    PasswordHash = cachedHash!, // Storing your BCrypt hash here
                    Role = "Client",
                    IdentityUserId = identityUser.Id // Link it to the Identity account we just created above!
                };

                // 4. Save the custom profile to its specific table
                // Note: Replace "CustomUsers" with whatever your leader named your custom table DbSet in ApplicationDbContext.cs
                _context.UserProfiles.Add(customUserProfile);

                // 5. Commit everything to the SQL database at once
                _context.SaveChanges();

                // Clear session data
                HttpContext.Session.Remove("Reg_FullName");
                HttpContext.Session.Remove("Reg_Email");
                HttpContext.Session.Remove("Reg_PasswordHash");
                HttpContext.Session.Remove("Reg_VerificationCode");

                TempData["SuccessMessage"] = "Email verified! Registration successful.";
                return RedirectToAction("Login", "Account");
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Error finalizing your account database entry.");
                return View(model);
            }
        }

        // Dummy method placeholder for sending your emails
        private void SendEmailCode(string email, string code)
        {
            // Integrate SMTP, SendGrid, or FluentEmail here
            // Example concept: 
            // _emailService.Send(email, "Your Verification Code", $"Your code is: {code}");
        }

        [HttpGet]
        public IActionResult Login() => View();
    }
}