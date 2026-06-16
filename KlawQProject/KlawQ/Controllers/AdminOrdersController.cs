using KlawQ.Data;
using KlawQ.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace KlawQ.Controllers
{
    [Route("AdminOrders")]
    public class AdminOrdersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminOrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 🖥️ WEB VIEW: Accessed via browser at https://localhost:7158/AdminOrders
        [HttpGet("")]
        [ApiExplorerSettings(IgnoreApi = true)] // Hides it from Swagger to prevent 500 view engine errors
        public async Task<IActionResult> Index([FromQuery] string status = "All", [FromQuery] int months = 3)
        {
            var viewModel = await GetFilteredOrdersDataAsync(status, months);

            // 🌟 FIXED: Points explicitly to your layout file's home in the Admin folder
            return View("~/Views/Admin/AdminOrders.cshtml", viewModel);
        }

        // 📡 API DATA ENDPOINT: Accessed via Swagger or API data calls at https://localhost:7158/AdminOrders/data
        [HttpGet("data")]
        public async Task<IActionResult> GetOrdersData([FromQuery] string status = "All", [FromQuery] int months = 3)
        {
            var viewModel = await GetFilteredOrdersDataAsync(status, months);
            return Ok(viewModel);
        }

        // 🧠 Reusable helper method to handle database queries without code duplication
        private async Task<AdminOrdersViewModel> GetFilteredOrdersDataAsync(string status, int months)
        {
            DateTime cutoffDate = DateTime.Now.AddMonths(-months);

            // Fetch data cleanly while eagerly loading structural item collections
            var query = _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .Where(o => o.Order_Date >= cutoffDate);

            if (!string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(o => o.Status == status);
            }

            var filteredOrders = await query.OrderByDescending(o => o.Order_Date).ToListAsync();

            return new AdminOrdersViewModel
            {
                Orders = filteredOrders,
                SelectedStatus = status,
                SelectedMonthsFilter = months
            };
        }
    }
}