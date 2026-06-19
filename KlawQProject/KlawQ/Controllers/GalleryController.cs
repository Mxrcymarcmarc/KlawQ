using KlawQ.Data;
using KlawQ.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KlawQ.Controllers
{
    /// <summary>
    /// Controller managing public gallery listings and favorite toggling.
    /// Covers Inheritance: Inherits from the base Controller class.
    /// Covers Abstraction: Uses database context dependencies to retrieve product lists and manage favorite records.
    /// </summary>
    [Route("[controller]")]
    public class GalleryController(ApplicationDbContext context) : Controller
    {
        private readonly ApplicationDbContext _context = context;

        // WEB VIEW ENDPOINT: Renders the product gallery list, highlighting items the user has favorited.
        // Covers Abstraction: Aggregates products and favorites list records from underlying storage asynchronously.
        // Covers Polymorphism: Returns IActionResult (ViewResult).
        [HttpGet("")]
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var products = await _context.Products.ToListAsync();

            // If user authenticated, get their favorited product IDs to render heart states
            List<int> favIds = [];
            if (User.Identity?.IsAuthenticated is true)
            {
                var email = User.Identity.Name;
                var user = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Email == email);
                if (user != null)
                {
                    favIds = await _context.Favorites.Where(f => f.UserID == user.UserID).Select(f => f.ProductID).ToListAsync();
                }
            }

            ViewData["FavoritedIds"] = favIds;
            return View(products);
        }

        // POST ACTION: Toggles favorite state of a product for the authenticated user.
        // Covers Encapsulation: Protects model integrity by checking user existence and validating if a favorite relation already exists before changing state.
        [HttpPost("toggle-favorite/{id}")]
        [Authorize]
        public async Task<IActionResult> ToggleFavorite(int id)
        {
            try
            {                var email = User.Identity?.Name;
                var user = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null) return Unauthorized();

                var existing = await _context.Favorites.FirstOrDefaultAsync(f => f.ProductID == id && f.UserID == user.UserID);
                if (existing != null)
                {                    _context.Favorites.Remove(existing);
                    await _context.SaveChangesAsync();                    return Ok(new { success = true, favorited = false });                }                else
                {                    var fav = new Favorite { ProductID = id, UserID = user.UserID, CreatedAt = DateTime.UtcNow };                    _context.Favorites.Add(fav);                    await _context.SaveChangesAsync();                    return Ok(new { success = true, favorited = true });                }            }            catch (Exception ex)            {                return BadRequest(new { success = false, error = ex.Message });            }        }
    }
}
