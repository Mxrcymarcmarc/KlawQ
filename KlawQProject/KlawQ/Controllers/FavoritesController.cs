using KlawQ.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KlawQ.Controllers
{
    /// <summary>
    /// Controller managing user favorite products.
    /// Covers Inheritance: Inherits from the base Controller class.
    /// Covers Abstraction: Interfaces with ApplicationDbContext to query favorite items.
    /// </summary>
    [Route("[controller]")]
    [Authorize]
    public class FavoritesController : Controller
    {
        private readonly ApplicationDbContext _context;
        public FavoritesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // WEB VIEW ENDPOINT: Renders the user's favorite products list.
        // Covers Abstraction: Retrieves user profile and queries relational favorites database records asynchronously.
        // Covers Polymorphism: Returns IActionResult, resolving dynamically to ViewResult or UnauthorizedResult.
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var email = User.Identity?.Name;
            var user = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return Unauthorized();
            var favs = await _context.Favorites.Where(f => f.UserID == user.UserID).Include(f => f.Product).ToListAsync();
            return View(favs);
        }
    }
}
