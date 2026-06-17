using KlawQ.Data;
using KlawQ.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KlawQ.Controllers
{
    [Route("[controller]")]
    public class GalleryController(ApplicationDbContext context) : Controller
    {
        private readonly ApplicationDbContext _context = context;

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
