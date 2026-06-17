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
            string[] validStates = { "Pending", "In Progress", "Completed", "Cancelled" };
            if (!validStates.Contains(status))
            {
                return BadRequest("Invalid target status conversion parameter string submitted.");
            }

            // Update database state
            order.Status = status;
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("ApproveCustomRequest")]
        public async Task<IActionResult> ApproveCustomRequest([FromQuery] int orderId)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == orderId);
            if (order == null)
            {
                return NotFound("Target order record missing.");
            }

            order.Order_Type = 'P'; // Convert custom request to active press-on order
            order.Status = "Pending"; // Initialize active order status
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("RejectCustomRequest")]
        public async Task<IActionResult> RejectCustomRequest([FromQuery] int orderId)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == orderId);
            if (order == null)
            {
                return NotFound("Target order record missing.");
            }

            order.Status = "Cancelled"; // Set status to Cancelled/Rejected
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpGet("Details/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.OrderID == id);

            if (order == null)
            {
                return NotFound();
            }

            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(u => u.UserID == order.UserID);
            ViewBag.UserEmail = userProfile?.Email ?? string.Empty;

            return View("~/Views/Admin/OrderDetails.cshtml", order);
        }

        // 🧠 Reusable helper method to handle database queries
        private async Task<AdminOrdersViewModel> GetFilteredOrdersDataAsync(string status, int months)
        {
            // Base tracking query eagerly pulling downstream item structural bindings
            var query = _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .Where(o => o.Status != "Payment Pending")
                .AsQueryable();

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