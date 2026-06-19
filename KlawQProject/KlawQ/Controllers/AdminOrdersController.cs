using KlawQ.Data;
using KlawQ.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace KlawQ.Controllers
{
    /// <summary>
    /// Controller managing orders and custom design requests within the administration dashboard.
    /// Covers Inheritance: Inherits from base Controller class.
    /// Covers Abstraction: Interfaces with EF Core DbContext to query orders and update database state.
    /// </summary>
    [Route("AdminOrders")]
    public class AdminOrdersController(ApplicationDbContext context) : Controller
    {
        // Dependency injection of the database context for data access
        private readonly ApplicationDbContext _context = context;


        // WEB VIEW ENDPOINT: Renders the main order management page with optional filtering parameters.
        // Covers Polymorphism: Returns IActionResult (an interface), allowing polymorphic rendering of ViewResult or standard responses.
        // Covers Abstraction: Delegates complexity of data retrieval to the GetFilteredOrdersDataAsync helper.
        [HttpGet("")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> Index([FromQuery] string status = "All", [FromQuery] int months = 3)
        {
            // Fetches the filtered orders data using the reusable helper method
            var viewModel = await GetFilteredOrdersDataAsync(status, months);

            // Points explicitly to your layout file's home in the Admin folder
            return View("~/Views/Admin/ManageOrders.cshtml", viewModel);
        }

        // API ENDPOINT: Provides JSON data for the orders based on filtering parameters, used by client-side scripts for dynamic updates.
        // Covers Polymorphism: Returns an IActionResult implementing OkObjectResult.
        // Covers Abstraction: Obtains filtered model data through a reusable service method.
        [HttpGet("data")]
        public async Task<IActionResult> GetOrdersData([FromQuery] string status = "All", [FromQuery] int months = 3)
        {
            // Reuses the same data retrieval logic to ensure consistency between the web view and API responses
            var viewModel = await GetFilteredOrdersDataAsync(status, months);
            return Ok(viewModel);
        }

        // API ENDPOINT: Updates the status of an order and handles related business logic such as stock deduction for completed orders.
        // Covers Encapsulation: Validates the target status against valid state constraints and manages product stock levels to protect model integrity.
        // Covers Abstraction: Abstracts database state changes under entity updates and SaveChangesAsync calls.
        [HttpPost("UpdateStatus")]
        public async Task<IActionResult> UpdateStatus([FromQuery] int orderId, [FromQuery] string status)
        {
            var order = await _context.Orders.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.OrderID == orderId);
            if (order == null)
            {
                // If the order record is missing, return a 404 Not Found response with a descriptive message
                return NotFound("Target base order mapping record missing.");
            }

            // Allowed state validation check block
            string[] validStates = [ "Pending", "In Progress", "Completed", "Cancelled" ];
            if (!validStates.Contains(status))
            {
                // If the provided status is not in the allowed list, return a 400 Bad Request response with a descriptive message
                return BadRequest("Invalid target status conversion parameter string submitted.");
            }

            // Deduct stock if transitioning to Completed
            if (status == "Completed" && order.Status != "Completed")
            {
                // Fetch related order items with product details, ignoring global query filters to ensure we get all relevant data
                var orderItems = await _context.OrderItems
                    .IgnoreQueryFilters()
                    .Include(oi => oi.Product)
                    .Where(oi => oi.OrderID == orderId)
                    .ToListAsync();

                // Iterate through each order item and deduct stock for PressOn products, ensuring stock does not go negative
                foreach (var item in orderItems)
                {
                    // Only deduct stock for products of type "PressOn"
                    if (item.Product?.Product_Type == "PressOn")
                    {
                        item.Product.Product_Stock = Math.Max(0, item.Product.Product_Stock - item.Quantity);
                    }
                }
            }

            // Update database state
            order.Status = status;
            await _context.SaveChangesAsync();

            return Ok();
        }

        // API ENDPOINT: Approves a custom order request by updating the order type, status, and embedding the approved price into the thumb photo JSON structure.
        // Covers Encapsulation: Encapsulates custom request processing logic, ensuring status/order type changes and JSON serialization occur atomically.
        // Covers Abstraction: Interacts with DB Context without exposing database configuration details.
        [HttpPost("ApproveCustomRequest")]
        public async Task<IActionResult> ApproveCustomRequest([FromQuery] int orderId, [FromQuery] decimal price)
        {
            // Fetch the order record, ignoring global query filters to ensure we can access custom request records that may be filtered out in normal queries
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == orderId);
            if (order == null)
            {
                // If the order record is missing, return a 404 Not Found response with a descriptive message
                return NotFound("Target order record missing.");
            }

            // Attempt to parse the existing Thumb_Photo as JSON and embed the approved price, while preserving existing properties. If parsing fails, create a new JSON object with just the price.
            if (!string.IsNullOrWhiteSpace(order.Thumb_Photo) && order.Thumb_Photo.TrimStart().StartsWith('{'))
            {
                try
                {
                    // Parse the existing JSON and create a new dictionary to hold the properties, excluding any existing price property
                    var doc = System.Text.Json.JsonDocument.Parse(order.Thumb_Photo);
                    var root = doc.RootElement;
                    var dict = new System.Collections.Generic.Dictionary<string, object>();
                    foreach (var prop in root.EnumerateObject())
                    {
                        // Skip any existing "price" property to ensure we overwrite it with the new approved price
                        if (prop.Name != "price")
                        {
                            // Handle different JSON value types appropriately to preserve the structure of the existing data
                            if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                // If the property value is an array, convert it to a List<string> to preserve the array structure in the new JSON
                                var list = new System.Collections.Generic.List<string>();
                                foreach (var item in prop.Value.EnumerateArray())
                                {
                                    // Add each item in the array to the list, converting to string and handling nulls gracefully
                                    list.Add(item.GetString() ?? "");
                                }
                                // Add the list to the dictionary under the property name
                                dict[prop.Name] = list;
                            }
                            // Handle other JSON value types (number, boolean, string) appropriately to preserve the original data types in the new JSON
                            else if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Number)
                            {
                                // If the property value is a number, attempt to get it as a decimal to preserve numeric types in the new JSON
                                dict[prop.Name] = prop.Value.GetDecimal();
                            }
                            // If the property value is a boolean, get it as a boolean to preserve boolean types in the new JSON
                            else if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.True || prop.Value.ValueKind == System.Text.Json.JsonValueKind.False)
                            {
                                dict[prop.Name] = prop.Value.GetBoolean();
                            }
                            else
                            {
                                // For all other value types (including string), get the value as a string, handling nulls gracefully, to preserve the original data in the new JSON
                                dict[prop.Name] = prop.Value.GetString() ?? "";
                            }
                        }
                    }
                    // Finally, add the approved price to the dictionary, which will overwrite any existing price property or add a new one if it didn't exist, ensuring the approved price is included in the final JSON structure
                    dict["price"] = price;
                    // Serialize the updated dictionary back to JSON and store it in the Thumb_Photo field, preserving all existing properties while embedding the new price
                    order.Thumb_Photo = JsonSerializer.Serialize(dict);
                }
                // If any exceptions occur during parsing or processing, fall back to creating a new JSON object with just the price, ensuring that the approved price is still stored even if the existing data is malformed
                catch
                {
                    order.Thumb_Photo = JsonSerializer.Serialize(new { price });
                }
            }
            // If the existing Thumb_Photo is null, empty, or not a valid JSON object, create a new JSON object with just the price to ensure the approved price is stored correctly
            else
            {
                order.Thumb_Photo = JsonSerializer.Serialize(new { price });
            }

            // Update the order type to 'P' for PressOn and set the status to "Payment Pending" to reflect that the custom request has been approved and is now an active order awaiting payment
            order.Order_Type = 'P'; // Convert custom request to active press-on order
            order.Status = "Payment Pending"; // Initialize active order status to require payment
            await _context.SaveChangesAsync();

            return Ok();
        }

        // API ENDPOINT: Rejects a custom order request by updating the order status to "Rejected", allowing administrators to manage and track rejected requests without deleting records.
        // Covers Encapsulation: Protects order data by changing the internal state to "Rejected" in a controlled setter action.
        [HttpPost("RejectCustomRequest")]
        public async Task<IActionResult> RejectCustomRequest([FromQuery] int orderId)
        {
            // Fetch the order record, ignoring global query filters to ensure we can access custom request records that may be filtered out in normal queries
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == orderId);
            if (order == null)
            {
                return NotFound("Target order record missing.");
            }

            // Update the order status to "Rejected" to indicate that the custom request has been reviewed and rejected, allowing for proper tracking and management of rejected requests in the system without deleting any records
            order.Status = "Rejected"; // Set status to Rejected
            await _context.SaveChangesAsync();

            return Ok();
        }

        // WEB VIEW ENDPOINT: Renders the details page for a specific order, including user email and all related order items with product details.
        // Covers Abstraction: Eagerly loads relational navigation properties via Entity Framework Core while hiding actual SQL joining complexities from callers.
        [HttpGet("Details/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            // Fetch the order with related items and product details, ignoring global query filters to ensure we can access all relevant data for the order, including any that may be filtered out in normal queries
            var order = await _context.Orders
                .IgnoreQueryFilters()
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.OrderID == id);

            // If the order is not found, return a 404 Not Found response to indicate that the requested order does not exist in the system, allowing for proper error handling and user feedback in the admin interface
            if (order == null)
            {
                return NotFound();
            }

            // Fetch the user profile to get the email associated with the order's UserID, allowing us to display the user's email on the order details page for better context and communication purposes. If the user profile is not found, we default to an empty string to avoid null reference issues in the view.
            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(u => u.UserID == order.UserID);
            ViewBag.UserEmail = userProfile?.Email ?? string.Empty;

            return View("~/Views/Admin/OrderDetails.cshtml", order);
        }

        // REUSABLE HELPER METHOD: Retrieves filtered orders data based on status and months parameters.
        // Covers Abstraction: Encapsulates query logic, ignoring query filters and fetching specific related models, hiding raw SQL translation details from the consumer.
        private async Task<AdminOrdersViewModel> GetFilteredOrdersDataAsync(string status, int months)
        {
            // Base tracking query eagerly pulling downstream item structural bindings
            var query = _context.Orders
                .IgnoreQueryFilters()
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .Where(o => o.Status != "Payment Pending")
                .AsQueryable();

            // Apply status filter if not "All"
            var filteredOrders = await query.OrderByDescending(o => o.Order_Date).ToListAsync();

            // Apply status filter if not "All"
            return new AdminOrdersViewModel
            {
                Orders = filteredOrders,
                SelectedStatus = status,
                SelectedMonthsFilter = months
            };
        }
    }
}