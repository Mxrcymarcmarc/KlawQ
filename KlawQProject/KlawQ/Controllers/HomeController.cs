using KlawQ.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace KlawQ.Controllers
{
    [Authorize(Roles = "User")]
    public class HomeController(KlawQ.Data.ApplicationDbContext context) : Controller
    {
        private readonly KlawQ.Data.ApplicationDbContext _context = context;

        [AllowAnonymous]
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

        [Authorize(Roles = "User")]
        [HttpGet("Home/OrderHistory")]
        public async Task<IActionResult> OrderHistory()
        {
            List<OrderItem> orderHistory = [];
            if (User.Identity?.IsAuthenticated is true)
            {
                var email = User.Identity.Name;
                var user = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Email == email);
                if (user != null)
                {
                    orderHistory = await _context.OrderItems
                        .Include(oi => oi.Order)
                        .Include(oi => oi.Product)
                        .Where(oi => oi.Order != null && oi.Order.UserID == user.UserID && oi.Order.Order_Type == 'P')
                        .OrderByDescending(oi => oi.Order!.Order_Date)
                        .ToListAsync();
                }
            }
            return View(orderHistory);
        }

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

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
