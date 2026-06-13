using KlawQ.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KlawQ.Controllers
{
    [Route("[controller]")]
    [Authorize]
    public class FavoritesController : Controller
    {
        private readonly ApplicationDbContext _context;
        public FavoritesController(ApplicationDbContext context)
        {
            _context = context;
        }

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
