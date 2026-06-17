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
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> Index([FromQuery] string status = "All", [FromQuery] int months = 3)
        {
            var viewModel = await GetFilteredOrdersDataAsync(status, months);

            // Points explicitly to your layout file's home in the Admin folder
            return View("~/Views/Admin/ManageOrders.cshtml", viewModel);
        }

        // 📡 API DATA ENDPOINT: Accessed via Swagger or API data calls at https://localhost:7158/AdminOrders/data
        [HttpGet("data")]
        public async Task<IActionResult> GetOrdersData([FromQuery] string status = "All", [FromQuery] int months = 3)
        {
            var viewModel = await GetFilteredOrdersDataAsync(status, months);
            return Ok(viewModel);
        }

        // 🚀 NEW POST ENDPOINT: Processes the background fetch state transitions asynchronously
        [HttpPost("UpdateStatus")]
        public async Task<IActionResult> UpdateStatus([FromQuery] int orderId, [FromQuery] string status)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == orderId);
            if (order == null)
            {
                return NotFound("Target base order mapping record missing.");
            }

            // Allowed state validation check block
            string[] validStates = { "Pending", "In Progress", "Completed" };
            if (!validStates.Contains(status))
            {
                return BadRequest("Invalid target status conversion parameter string submitted.");
            }

            // Update database state
            order.Status = status;
            await _context.SaveChangesAsync();

            return Ok();
        }

        // 🧠 Reusable helper method to handle database queries
        private async Task<AdminOrdersViewModel> GetFilteredOrdersDataAsync(string status, int months)
        {
            // Base tracking query eagerly pulling downstream item structural bindings
            var query = _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .AsQueryable();

            // 🌟 LOGIC TWEAK: If looking for backlog status types ("Pending"/"In Progress"), 
            // skip chronological date limits so old unfulfilled items don't hide from admins.
            if (string.Equals(status, "All", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                DateTime cutoffDate = DateTime.Now.AddMonths(-months);
                query = query.Where(o => o.Order_Date >= cutoffDate);
            }

            // Apply status filter strings
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