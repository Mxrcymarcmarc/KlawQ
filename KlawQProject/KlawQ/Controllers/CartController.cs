using KlawQ.Data;
using KlawQ.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KlawQ.Controllers
{
    [Route("[controller]")]
    [Authorize]
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        private async Task<Cart> GetOrCreateCart()
        {
            var email = User.Identity?.Name;
            var user = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) throw new Exception("User not found");

            var cart = await _context.Carts.FirstOrDefaultAsync(c => c.UserID == user.UserID);
            if (cart == null)
            {
                cart = new Cart { UserID = user.UserID, CreatedAt = DateTime.UtcNow };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }
            return cart;
        }

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
                    }).ToList(),
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

        [HttpPost("add/{id}")]
        public async Task<IActionResult> Add(int id)
        {
            try
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == id);
                if (product == null) return NotFound(new { success = false, error = "Product not found" });

                var cart = await GetOrCreateCart();

                // determine quantity from form or querystring (default 1)
                string qtyStr = Request.Form["quantity"].FirstOrDefault() ?? Request.Query["quantity"].ToString();
                int qty = 1;
                if (!string.IsNullOrWhiteSpace(qtyStr) && int.TryParse(qtyStr, out var q) && q > 0) qty = q;

                var existing = await _context.CartItems.FirstOrDefaultAsync(ci => ci.CartId == cart.CartId && ci.ProductID == id);
                if (existing != null)
                {
                    existing.Quantity += qty;
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
