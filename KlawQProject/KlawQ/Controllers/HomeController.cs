using KlawQ.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace KlawQ.Controllers
{
    [Authorize(Roles = "User")]
    public class HomeController : Controller
    {
        private readonly KlawQ.Data.ApplicationDbContext _context;
        public HomeController(KlawQ.Data.ApplicationDbContext context)
        {
            _context = context;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var since = DateTime.UtcNow.AddDays(-7);

            var topProductIds = await _context.Favorites
                .Where(f => f.CreatedAt >= since)
                .GroupBy(f => f.ProductID)
                .Select(g => new { ProductID = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(3)
                .Select(g => g.ProductID)
                .ToListAsync();

            List<Products> featured;
            if (topProductIds.Any())
            {
                featured = await _context.Products.Where(p => topProductIds.Contains(p.ProductID)).ToListAsync();
            }
            else
            {
                featured = await _context.Products.OrderBy(p => p.ProductID).Take(3).ToListAsync();
            }

            ViewData["FeaturedProducts"] = featured;
            return View();
        }

        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
