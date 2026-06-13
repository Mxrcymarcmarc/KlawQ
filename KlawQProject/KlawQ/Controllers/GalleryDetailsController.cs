using Microsoft.AspNetCore.Mvc;
using KlawQ.Data;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace KlawQ.Controllers
{
    [Route("Gallery")]
    public class GalleryDetailsController : Controller
    {
        private readonly ApplicationDbContext _context;
        public GalleryDetailsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("Details/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == id);
                if (product == null) return NotFound();

                // load related products for thumbnails (exclude current) and match the same product type
                var type = (product.Product_Type ?? "").ToLower();
                var related = await _context.Products
                    .Where(p => p.ProductID != id && ((p.Product_Type ?? "").ToLower() == type))
                    .OrderByDescending(p => p.ProductID)
                    .Take(12)
                    .ToListAsync();

                bool isFavorited = false;
                if (User.Identity?.IsAuthenticated == true)
                {
                    var email = User.Identity.Name;
                    var user = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Email == email);
                    if (user != null)
                    {
                        isFavorited = await _context.Favorites.AnyAsync(f => f.UserID == user.UserID && f.ProductID == id);
                    }
                }

                ViewData["IsFavorited"] = isFavorited;
                ViewBag.RelatedProducts = related;
                return View(product);
            }
            catch (Exception ex)
            {
                // log exception to debug output and redirect back to gallery with message
                System.Diagnostics.Debug.WriteLine(ex);
                TempData["ErrorMessage"] = "Failed to load item details.";
                return RedirectToAction("Index", "Gallery");
            }
        }
    }
}
