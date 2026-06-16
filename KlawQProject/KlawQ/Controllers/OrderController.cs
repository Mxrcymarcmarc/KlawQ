using KlawQ.Data;
using KlawQ.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace KlawQ.Controllers
{
    [Route("[controller]")]
    [Authorize]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        public OrderController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpGet("Start")]
        public async Task<IActionResult> Start([FromQuery] int[] productIds, int productId = 0)
        {
            // If single productId provided via query, use it; otherwise use list
            var ids = productIds?.Where(i => i > 0).ToList() ?? new List<int>();
            if (productId > 0) ids.Insert(0, productId);

            List<Products> products = new();
            if (ids.Any()) products = await _context.Products.Where(p => ids.Contains(p.ProductID)).ToListAsync();

            var vm = new OrderStartViewModel { Products = products };
            return View(vm);
        }

        [HttpPost("Confirm")]
        public async Task<IActionResult> Confirm(OrderSubmitModel model)
        {
            try
            {
                var email = User.Identity?.Name;
                var user = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null) return Unauthorized();

                // parse product ids from form if needed
                var raw = Request.Form["ProductIds"].ToString();
                var ids = new List<int>();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    ids = raw.Split(new[] { ',', '&' }, StringSplitOptions.RemoveEmptyEntries).Select(s => { int.TryParse(s, out var x); return x; }).Where(x => x > 0).ToList();
                }

                // read uploaded files and convert to base64
                string handBase64 = null;
                string thumbBase64 = null;
                if (Request.Form.Files != null && Request.Form.Files.Count > 0)
                {
                    var handFile = Request.Form.Files.GetFile("HandPhoto");
                    var thumbFile = Request.Form.Files.GetFile("ThumbPhoto");
                    if (handFile != null) { using var ms = new MemoryStream(); await handFile.CopyToAsync(ms); handBase64 = "data:" + handFile.ContentType + ";base64," + Convert.ToBase64String(ms.ToArray()); }
                    if (thumbFile != null) { using var ms2 = new MemoryStream(); await thumbFile.CopyToAsync(ms2); thumbBase64 = "data:" + thumbFile.ContentType + ";base64," + Convert.ToBase64String(ms2.ToArray()); }
                }

                if (!ModelState.IsValid) return View("Start", new OrderStartViewModel { Products = await _context.Products.Where(p => ids.Contains(p.ProductID)).ToListAsync(), Submit = model });

                var order = new Order
                {
                    UserID = user.UserID,
                    Order_Date = DateTime.UtcNow,
                    Full_Name = model.FullName,
                    Social_Account = model.SocialAccount,
                    Delivery_Location = model.DeliveryLocation,
                    Delivery_Method = model.DeliveryMethod,
                    Payment_Method = model.PaymentMethod,

                    // 🌟 FIXED: Passes the form string value directly. If it's null, it falls back to a string "0".
                    Contact_Number = !string.IsNullOrWhiteSpace(model.ContactNumber) ? model.ContactNumber : "0",

                    Hand_Photo = handBase64 ?? string.Empty,
                    Thumb_Photo = thumbBase64 ?? string.Empty,
                    Order_Type = 'P',
                    Status = "Pending"
                };

                _context.Add(order);
                await _context.SaveChangesAsync();

                // create order items
                foreach (var pid in ids)
                {
                    var oi = new OrderItem { OrderID = order.OrderID, ProductID = pid, Quantity = 1 };
                    _context.Add(oi);
                }
                await _context.SaveChangesAsync();

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                var raw2 = Request.Form["ProductIds"].ToString();
                var ids2 = new List<int>();
                if (!string.IsNullOrWhiteSpace(raw2)) ids2 = raw2.Split(new[] { ',', '&' }, StringSplitOptions.RemoveEmptyEntries).Select(s => { int.TryParse(s, out var x); return x; }).Where(x => x > 0).ToList();
                return View("Start", new OrderStartViewModel { Products = await _context.Products.Where(p => ids2.Contains(p.ProductID)).ToListAsync(), Submit = model });
            }
        }
    }
}