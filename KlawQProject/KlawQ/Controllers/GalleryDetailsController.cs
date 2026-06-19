using Microsoft.AspNetCore.Mvc;
using KlawQ.Data;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace KlawQ.Controllers
{
    /// <summary>
    /// Controller managing details view and related product recommendations for the public gallery.
    /// Covers Inheritance: Inherits from the base Controller class.
    /// Covers Abstraction: Uses ApplicationDbContext to isolate query layers.
    /// </summary>
    [Route("Gallery")]
    public class GalleryDetailsController : Controller
    {
        private readonly ApplicationDbContext _context;
        public GalleryDetailsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // WEB VIEW ENDPOINT: Renders the details page for a specific product and loads related items.
        // Covers Abstraction: Hides database query rules (type matching, exclusion of self, limits) behind Entity Framework constructs.
        // Covers Polymorphism: Returns polymorphic IActionResult depending on parameter validity.
        [HttpGet("Details/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == id);
                if (product == null) return NotFound();

                // load related products for thumbnails (exclude current) and match the same product type
                var related = await _context.Products
                    .Where(p => p.ProductID != id && p.Product_Type == product.Product_Type)
                    .OrderByDescending(p => p.ProductID)
                    .Take(12)
                    .ToListAsync();

                bool isFavorited = false;
                if (User.Identity?.IsAuthenticated is true)
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
