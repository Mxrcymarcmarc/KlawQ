using KlawQ.Data;
using KlawQ.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace KlawQ.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 🖥️ GET: /admin
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            // 🌟 1. Eagerly load Items and their associated Products to safely access .Price
            decimal totalRevenue = await _context.Orders
                .Where(o => o.Status == "Completed")
                .SelectMany(o => o.Items)
                .SumAsync(i => (decimal?)i.Quantity * (i.Product != null ? i.Product.Product_Price : 0)) ?? 0;

            // 🌟 2. Query dynamic metrics counts from the Orders table
            int totalOrdersCount = await _context.Orders.CountAsync();
            int pendingOrdersCount = await _context.Orders.CountAsync(o => o.Status == "Pending");
            int completedOrdersCount = await _context.Orders.CountAsync(o => o.Status == "Completed");

            // 🌟 3. NEW: Count bookings scheduled within the NEXT 7 days
            var now = DateTime.UtcNow;
            var sevenDaysFromNow = now.AddDays(7);

            int thisWeekBookingsCount = await _context.Schedulers
                .Where(o => o.Appointment_Date >= now && o.Appointment_Date <= sevenDaysFromNow)
                .CountAsync();

            // 🌟 4. Pass values to the view using ViewData maps
            ViewData["TotalRevenue"] = totalRevenue;
            ViewData["TotalOrdersCount"] = totalOrdersCount;
            ViewData["PendingOrdersCount"] = pendingOrdersCount;
            ViewData["CompletedOrdersCount"] = completedOrdersCount;
            ViewData["ThisWeekBookingsCount"] = thisWeekBookingsCount; // 👈 Injected right here!

            return View();
        }

        [HttpGet("PortfolioManager")]
        public async Task<IActionResult> PortfolioManager()
        {
            var products = await _context.Products.OrderBy(p => p.ProductID).ToListAsync();
            return View(products);
        }

        [HttpPost("PortfolioManager/create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePortfolioItem(Products product, IFormFile imageFile)
        {
            if (imageFile != null && imageFile.Length > 0)
            {
                var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "gallery");
                Directory.CreateDirectory(uploads);
                var fileName = System.Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                var filePath = Path.Combine(uploads, fileName);
                await using var fs = new FileStream(filePath, FileMode.Create);
                await imageFile.CopyToAsync(fs);
                product.Product_Image = "/images/gallery/" + fileName;
                ModelState.Remove(nameof(Products.Product_Image));
            }
            else
            {
                ModelState.AddModelError("imageFile", "Please upload an image file.");
            }

            if (!ModelState.IsValid)
            {
                var currentProducts = await _context.Products.OrderBy(p => p.ProductID).ToListAsync();
                return View("PortfolioManager", currentProducts);
            }

            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            return RedirectToAction("PortfolioManager");
        }

        [HttpPost("PortfolioManager/delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePortfolioItem(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrEmpty(product.Product_Image) && product.Product_Image.StartsWith("/images/gallery/"))
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", product.Product_Image.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            return RedirectToAction("PortfolioManager");
        }

        [HttpGet("ConfigureSlots")]
        public IActionResult ConfigureSlots()
        {
            // This looks for and loads your Views/Admin/ConfigureSlots.cshtml file
            return View("ConfigureDateTimeSlot");
        }

        [HttpGet("ManageAppointments")]
        public IActionResult ManageAppointments()
        {
            // Returns your appointment lists/schedules view
            return View("ManageAppointments");
        }
    }
}