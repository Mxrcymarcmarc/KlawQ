using KlawQ.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace KlawQ.Controllers
{
    /// <summary>
    /// Controller managing user landing page, order history, appointment history, and other static pages.
    /// Covers Inheritance: Inherits from the base Controller class.
    /// Covers Abstraction: Integrates with database services to pull user dashboard stats and histories.
    /// </summary>
    [Authorize(Roles = "User")]
    public class HomeController(KlawQ.Data.ApplicationDbContext context) : Controller
    {
        private readonly KlawQ.Data.ApplicationDbContext _context = context;

        // WEB VIEW ENDPOINT: Renders the user home landing page with featured items.
        // Covers Abstraction: Hides database aggregation, sorting, grouping, and fallback algorithms behind simple entity queries.
        public async Task<IActionResult> Index()
        {
            var since = DateTime.UtcNow.AddDays(-7);

            // 1. Get Top 3 Press-On Sets
            var pressOnFavs = await _context.Favorites
                .Where(f => f.CreatedAt >= since)
                .GroupBy(f => f.ProductID)
                .Select(g => new { ProductID = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Select(g => g.ProductID)
                .ToListAsync();

            List<Products> pressOnProducts = [];
            if (pressOnFavs.Count > 0)
            {
                pressOnProducts = await _context.Products
                    .Where(p => p.Product_Type != null && EF.Functions.Like(p.Product_Type, "%press%") && pressOnFavs.Contains(p.ProductID))
                    .ToListAsync();

                pressOnProducts = [.. pressOnProducts
                    .OrderBy(p => pressOnFavs.IndexOf(p.ProductID))
                    .Take(3)];
            }

            if (pressOnProducts.Count < 3)
            {
                var recentPressOns = await _context.Products
                    .Where(p => p.Product_Type != null && EF.Functions.Like(p.Product_Type, "%press%"))
                    .OrderByDescending(p => p.ProductID)
                    .Take(3)
                    .ToListAsync();

                foreach (var item in recentPressOns)
                {
                    if (pressOnProducts.Count >= 3) break;
                    if (!pressOnProducts.Any(p => p.ProductID == item.ProductID))
                    {
                        pressOnProducts.Add(item);
                    }
                }
            }

            // 2. Get Top 3 Original Sets
            var originalFavs = await _context.Favorites
                .Where(f => f.CreatedAt >= since)
                .GroupBy(f => f.ProductID)
                .Select(g => new { ProductID = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Select(g => g.ProductID)
                .ToListAsync();

            List<Products> originalProducts = [];
            if (originalFavs.Count > 0)
            {
                originalProducts = await _context.Products
                    .Where(p => (p.Product_Type == null || !EF.Functions.Like(p.Product_Type, "%press%")) && originalFavs.Contains(p.ProductID))
                    .ToListAsync();

                originalProducts = [.. originalProducts
                    .OrderBy(p => originalFavs.IndexOf(p.ProductID))
                    .Take(3)];
            }

            if (originalProducts.Count < 3)
            {
                var recentOriginals = await _context.Products
                    .Where(p => p.Product_Type == null || !EF.Functions.Like(p.Product_Type, "%press%"))
                    .OrderByDescending(p => p.ProductID)
                    .Take(3)
                    .ToListAsync();

                foreach (var item in recentOriginals)
                {
                    if (originalProducts.Count >= 3) break;
                    if (!originalProducts.Any(p => p.ProductID == item.ProductID))
                    {
                        originalProducts.Add(item);
                    }
                }
            }

            ViewData["FeaturedPressOns"] = pressOnProducts;
            ViewData["FeaturedOriginals"] = originalProducts;

            return View();
        }

        // WEB VIEW ENDPOINT: Renders the user's historical order transactions.
        // Covers Abstraction: Hides the complex relation joining and sorting behavior of EF queries.
        [Authorize(Roles = "User")]
        [HttpGet("Home/OrderHistory")]
        public async Task<IActionResult> OrderHistory()
        {
            List<Order> orders = [];
            if (User.Identity?.IsAuthenticated is true)
            {
                var email = User.Identity.Name;
                var user = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Email == email);
                if (user != null)
                {
                    orders = await _context.Orders
                        .IgnoreQueryFilters()
                        .Include(o => o.Items)
                        .ThenInclude(oi => oi.Product)
                        .Where(o => o.UserID == user.UserID && (o.Order_Type == 'P' || o.Order_Type == 'C'))
                        .OrderByDescending(o => o.Order_Date)
                        .ToListAsync();
                }
            }
            return View(orders);
        }

        // WEB VIEW ENDPOINT: Renders the user's booking appointment schedules.
        // Covers Abstraction: Asynchronously queries database entities filtered by user profiles.
        [Authorize(Roles = "User")]
        [HttpGet("Home/AppointmentHistory")]
        public async Task<IActionResult> AppointmentHistory()
        {
            List<Appointment> appointments = [];
            if (User.Identity?.IsAuthenticated is true)
            {
                var email = User.Identity.Name;
                var user = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Email == email);
                if (user != null)
                {
                    appointments = await _context.Appointments
                        .Include(a => a.Scheduler)
                        .Where(a => a.UserId == user.UserID && a.Down_Payment_Paid)
                        .OrderByDescending(a => a.AppId)
                        .ToListAsync();
                }
            }
            return View(appointments);
        }

        // WEB VIEW ENDPOINT: Renders static FAQ information.
        // Covers Polymorphism: Returns IActionResult (resolving to ViewResult).
        [AllowAnonymous]
        [HttpGet("Home/FAQ")]
        public IActionResult FAQ()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return View();
        }

        // WEB VIEW ENDPOINT: Standard error page.
        // Covers Abstraction: Encapsulates runtime diagnostics within the returned ErrorViewModel model context.
        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
