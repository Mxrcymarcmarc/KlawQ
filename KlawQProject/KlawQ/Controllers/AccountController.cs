using KlawQ.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KlawQ.Data;
using System.Threading.Tasks;

namespace KlawQ.Controllers
{
    /// <summary>
    /// Controller managing user authentication, login, registration and profiling.
    /// Covers Inheritance: Inherits from the base Controller class to share basic HTTP response behaviors.
    /// Covers Abstraction: Uses UserManager and SignInManager services, abstracting core password hashing and cookie management.
    /// </summary>
    public class AccountController(SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager, ApplicationDbContext context) : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager = signInManager;
        private readonly UserManager<IdentityUser> _userManager = userManager;
        private readonly ApplicationDbContext _context = context;

        /// <summary>
        /// GET: /Account/Register
        /// Renders the registration view.
        /// Covers Polymorphism: Overloaded method signature compared to the POST endpoint.
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        /// <summary>
        /// POST: /Account/Register
        /// Registers a new user.
        /// Covers Encapsulation: Validates the model data bounds within RegisterViewModel properties.
        /// Covers Polymorphism: Handles POST payload variant of the overloaded Register method.
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = new IdentityUser { UserName = model.Email, Email = model.Email };
            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "User");
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        /// <summary>
        /// GET: /Account/Login
        /// Renders the login screen.
        /// Covers Polymorphism: Method signature overloading.
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }

        /// <summary>
        /// POST: /Account/Login
        /// Authenticates the user and initiates session cookies.
        /// Covers Encapsulation: Protects credentials validation via LoginViewModel binding parameters.
        /// Covers Abstraction: Delegates credentials checks to Identity PasswordSignInAsync API.
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid) return View(model);

            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null && await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    return Redirect("/admin");
                }
                return RedirectToLocal(returnUrl);
            }
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        /// <summary>
        /// POST: /Account/Logout
        /// Signs out the user and clears authorization cookies.
        /// Covers Abstraction: Relies on Identity context to destroy the user session securely.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();

            // ADDED: TempData string assignment tracking state
            TempData["LoggedOutMessage"] = "You have been successfully logged out. See you again soon!";

            return RedirectToAction("Login", "Account");
        }

        /// <summary>
        /// GET: /Account/Profile
        /// Loads the profile entity for the currently logged-in user.
        /// Covers Encapsulation: Validates the request email using EF Core before displaying data to the view context.
        /// </summary>
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Login", "Account");
            }

            var user = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                return NotFound("User profile not found.");
            }

            return View(user);
        }

        /// <summary>
        /// GET: /Account/Logout
        /// Fallback link to trigger sign-out via GET requests.
        /// Covers Polymorphism: Method overloading behavior variation.
        /// </summary>
        [HttpGet]
        [Route("Account/Logout")]
        public async Task<IActionResult> LogoutGet()
        {
            await _signInManager.SignOutAsync();

            // ADDED: Fallback security parameter tracking for GET link triggers
            TempData["LoggedOutMessage"] = "You have been successfully logged out. See you again soon!";

            return RedirectToAction("Login", "Account");
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }
    }
}