using KlawQ.Data;
using KlawQ.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KlawQ.Controllers
{
    /// <summary>
    /// Controller managing operations for adding, viewing, and removing items in the user's cart.
    /// Covers Inheritance: Inherits from the base Controller class.
    /// Covers Abstraction: Interfaces with ApplicationDbContext to query user profiles, carts, and cart items.
    /// </summary>
    [Route("[controller]")]
    [Authorize]
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        // PRIVATE HELPER: Retrieves or initializes the user's cart.
        // Covers Abstraction: Abstracts cart retrieval logic, ensuring the client receives a valid cart without exposing DB state details.
        private async Task<Cart> GetOrCreateCart()
        {
            var email = User.Identity?.Name;
            var user = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Email == email);
            if (user is null) throw new Exception("User not found");

            var cart = await _context.Carts.FirstOrDefaultAsync(c => c.UserID == user.UserID);
            if (cart is null)
            {
                cart = new Cart { UserID = user.UserID, CreatedAt = DateTime.UtcNow };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }
            return cart;
        }

        // API ENDPOINT: Retrieves all items in the user's cart.
        // Covers Abstraction: Projects database records onto a simplified anonymous model.
        // Covers Polymorphism: Returns IActionResult, allowing polymorphic HTTP responses.
        [HttpGet("items")]
        public async Task<IActionResult> GetItems()
        {
            try
            {
                var cart = await GetOrCreateCart();
                var items = await _context.CartItems
                    .Where(ci => ci.CartId == cart.CartId)
                    .Include(ci => ci.Product)
                    .ToListAsync();

                var result = new
                {
                    items = items.Select(ci => new
                    {
                        id = ci.CartItemId,
                        productId = ci.ProductID,
                        name = ci.Product?.Product_Name ?? "Unknown",
                        price = ci.Product?.Product_Price ?? 0,
                        qty = ci.Quantity,
                        image = ci.Product?.Product_Image ?? "/images/placeholder.png"
                    }),
                    count = items.Count,
                    total = items.Sum(ci => (ci.Product?.Product_Price ?? 0) * ci.Quantity)
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // POST ACTION: Adds a product to the user's cart.
        // Covers Encapsulation: Restricts quantity mutations to positive values and verifies product existence before modifying model state.
        [HttpPost("add/{id}")]
        public async Task<IActionResult> Add(int id)
        {
            try
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == id);
                if (product == null) return NotFound(new { success = false, error = "Product not found" });

                var isPressOn = string.Equals(product.Product_Type, "PressOn", StringComparison.OrdinalIgnoreCase);
                if (isPressOn && product.Product_Stock <= 0)
                {
                    return BadRequest(new { success = false, error = "This item is currently out of stock." });
                }

                var cart = await GetOrCreateCart();

                // determine quantity from form or querystring (default 1)
                string qtyStr = Request.Form["quantity"].FirstOrDefault() ?? Request.Query["quantity"].ToString();
                int qty = 1;
                if (!string.IsNullOrWhiteSpace(qtyStr) && int.TryParse(qtyStr, out var q) && q > 0) qty = q;

                var existing = await _context.CartItems.FirstOrDefaultAsync(ci => ci.CartId == cart.CartId && ci.ProductID == id);
                int targetQty = qty;
                if (existing != null)
                {
                    targetQty += existing.Quantity;
                }

                if (isPressOn && targetQty > product.Product_Stock)
                {
                    return BadRequest(new { success = false, error = $"Only {product.Product_Stock} item(s) are available in stock. You currently have {existing?.Quantity ?? 0} in your cart." });
                }

                if (existing != null)
                {
                    existing.Quantity = targetQty;
                }
                else
                {
                    var cartItem = new CartItem { CartId = cart.CartId, ProductID = id, Quantity = qty };
                    _context.CartItems.Add(cartItem);
                }

                await _context.SaveChangesAsync();

                var count = await _context.CartItems.Where(ci => ci.CartId == cart.CartId).CountAsync();
                return Ok(new { success = true, count });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // POST ACTION: Removes a specific item from the user's cart.
        // Covers Encapsulation: Enforces verification that the cart item exists before removing it from the database context.
        [HttpPost("remove/{id}")]
        public async Task<IActionResult> Remove(int id)
        {
            try
            {
                var cartItem = await _context.CartItems.FirstOrDefaultAsync(ci => ci.CartItemId == id);
                if (cartItem == null) return NotFound(new { success = false, error = "Item not found" });

                _context.CartItems.Remove(cartItem);
                await _context.SaveChangesAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }
    }
}
