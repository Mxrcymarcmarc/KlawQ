using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KlawQ.Data;
using KlawQ.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KlawQ.Controllers
{
    [Route("[controller]")]
    [Authorize]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private static readonly char[] SplitChars = { ',', '&' };

        public OrderController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpGet("Start")]
        public async Task<IActionResult> Start([FromQuery] int[] productIds, [FromQuery] int[] qtys, int productId = 0)
        {
            var ids = productIds?.Where(i => i > 0).ToList() ?? [];
            if (productId > 0) ids.Insert(0, productId);

            List<Products> products = [];
            if (ids.Count > 0) products = await _context.Products.Where(p => ids.Contains(p.ProductID)).ToListAsync();

            var vm = new OrderStartViewModel { Products = products };

            // build quantities map from query param 'qtys' (aligned by index with productIds)
            var quantities = new Dictionary<int, int>();
            if (ids.Count > 0)
            {
                for (int i = 0; i < ids.Count; i++)
                {
                    var pid = ids[i];
                    int qty = 1;
                    if (qtys != null && qtys.Length > i && qtys[i] > 0) qty = qtys[i];
                    quantities[pid] = qty;
                }
            }
            vm.ProductQuantities = quantities;

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

                var raw = Request.Form["ProductIds"].ToString();
                List<int> ids = [];
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    ids = raw.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.TryParse(s, out var x) ? x : 0).Where(x => x > 0).ToList();
                }

                string? handBase64 = null;
                string? thumbBase64 = null;
                if (Request.Form.Files?.Count > 0)
                {
                    var handFile = Request.Form.Files.GetFile("HandPhoto");
                    var thumbFile = Request.Form.Files.GetFile("ThumbPhoto");
                    if (handFile != null) { await using var ms = new MemoryStream(); await handFile.CopyToAsync(ms); handBase64 = "data:" + handFile.ContentType + ";base64," + Convert.ToBase64String(ms.ToArray()); }
                    if (thumbFile != null) { await using var ms2 = new MemoryStream(); await thumbFile.CopyToAsync(ms2); thumbBase64 = "data:" + thumbFile.ContentType + ";base64," + Convert.ToBase64String(ms2.ToArray()); }
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
                    Contact_Number = !string.IsNullOrWhiteSpace(model.ContactNumber) ? model.ContactNumber : "0",
                    Hand_Photo = handBase64 ?? string.Empty,
                    Thumb_Photo = thumbBase64 ?? string.Empty,
                    Order_Type = 'P',
                    Status = "Pending"
                };

                _context.Add(order);
                await _context.SaveChangesAsync();

                // read quantities from form if provided (aligned order with ProductIds)
                var qtysRaw = Request.Form["Quantities"].ToString();
                List<int> qtys = [];
                if (!string.IsNullOrWhiteSpace(qtysRaw))
                {
                    qtys = qtysRaw.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries).Select(s => int.TryParse(s, out var x) ? x : 0).Where(x => x > 0).ToList();
                }

                if (ids.Count > 0)
                {
                    for (int i = 0; i < ids.Count; i++)
                    {
                        var pid = ids[i];
                        int qty = 1;
                        if (i < qtys.Count) qty = qtys[i];
                        var oi = new OrderItem { OrderID = order.OrderID, ProductID = pid, Quantity = qty };
                        _context.Add(oi);
                    }
                }
                await _context.SaveChangesAsync();

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                var raw2 = Request.Form["ProductIds"].ToString();
                List<int> ids2 = [];
                if (!string.IsNullOrWhiteSpace(raw2)) ids2 = raw2.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries).Select(s => int.TryParse(s, out var x) ? x : 0).Where(x => x > 0).ToList();
                return View("Start", new OrderStartViewModel { Products = await _context.Products.Where(p => ids2.Contains(p.ProductID)).ToListAsync(), Submit = model });
            }
        }

        [HttpGet("CheckoutStandard")]
        public async Task<IActionResult> CheckoutStandard([FromQuery] int[] productIds, [FromQuery] int[] qtys, int productId = 0)
        {
            var ids = productIds?.Where(i => i > 0).ToList() ?? [];
            if (productId > 0) ids.Insert(0, productId);

            List<Products> products = [];
            if (ids.Count > 0) products = await _context.Products.Where(p => ids.Contains(p.ProductID)).ToListAsync();

            var vm = new OrderStartViewModel { Products = products };

            // populate quantities map if provided
            var quantities = new Dictionary<int, int>();
            if (ids.Count > 0)
            {
                for (int i = 0; i < ids.Count; i++)
                {
                    var pid = ids[i];
                    int qty = 1;
                    if (qtys != null && qtys.Length > i && qtys[i] > 0) qty = qtys[i];
                    quantities[pid] = qty;
                }
            }
            vm.ProductQuantities = quantities;

            return View(vm);
        }

        [HttpPost("CheckoutStandard")]
        public async Task<IActionResult> CheckoutStandardPost([FromForm] OrderSubmitModel model)
        {
            try
            {
                var email = User.Identity?.Name;
                var user = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null) return Unauthorized();

                if (model.ProductIds == null || model.ProductIds.Length == 0)
                {
                    ModelState.AddModelError("", "No products selected.");
                    return View("CheckoutStandard", new OrderStartViewModel { Products = [], Submit = model });
                }

                if (!ModelState.IsValid) return View("CheckoutStandard", new OrderStartViewModel { Products = await _context.Products.Where(p => model.ProductIds.Contains(p.ProductID)).ToListAsync(), Submit = model });

                string? handBase64 = null;
                var handFile = Request.Form.Files.GetFile("HandPhoto") ?? (Request.Form.Files.Count > 0 ? Request.Form.Files[0] : null);
                if (handFile != null)
                {
                    await using var ms = new MemoryStream();
                    await handFile.CopyToAsync(ms);
                    handBase64 = "data:" + handFile.ContentType + ";base64," + Convert.ToBase64String(ms.ToArray());
                }

                var order = new Order
                {
                    UserID = user.UserID,
                    Order_Date = DateTime.UtcNow,
                    Full_Name = model.FullName,
                    Social_Account = model.SocialAccount,
                    Delivery_Location = model.DeliveryLocation,
                    Delivery_Method = model.DeliveryMethod,
                    Payment_Method = string.IsNullOrWhiteSpace(model.PaymentMethod) ? "PayMongo" : model.PaymentMethod,
                    Contact_Number = !string.IsNullOrWhiteSpace(model.ContactNumber) ? model.ContactNumber : "0",
                    Hand_Photo = handBase64 ?? string.Empty,
                    Thumb_Photo = string.Empty,
                    Order_Type = 'P',
                    Status = "Pending"
                };

                _context.Add(order);
                await _context.SaveChangesAsync();

                // create order items honoring submitted quantities
                if (model.ProductIds != null && model.ProductIds.Length > 0)
                {
                    for (int i = 0; i < model.ProductIds.Length; i++)
                    {
                        var pid = model.ProductIds[i];
                        int qty = 1;
                        if (model.Quantities != null && model.Quantities.Length > i && model.Quantities[i] > 0) qty = model.Quantities[i];
                        var oi = new OrderItem { OrderID = order.OrderID, ProductID = pid, Quantity = qty };
                        _context.Add(oi);
                    }
                }
                await _context.SaveChangesAsync();

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View("CheckoutStandard", new OrderStartViewModel { Products = await _context.Products.Where(p => model.ProductIds.Contains(p.ProductID)).ToListAsync(), Submit = model });
            }
        }

        [HttpGet("CheckoutCustom")]
        public async Task<IActionResult> CheckoutCustom([FromQuery] int[] productIds, int productId = 0)
        {
            var ids = productIds?.Where(i => i > 0).ToList() ?? [];
            if (productId > 0) ids.Insert(0, productId);

            List<Products> products = [];
            if (ids.Count > 0) products = await _context.Products.Where(p => ids.Contains(p.ProductID)).ToListAsync();

            var vm = new OrderStartViewModel { Products = products };
            return View(vm);
        }

        [HttpPost("CheckoutCustom")]
        public async Task<IActionResult> CheckoutCustomPost([FromForm] CustomOrderSubmitModel model)
        {
            try
            {
                var email = User.Identity?.Name;
                var user = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null) return Unauthorized();

                if (model.ProductIds == null || model.ProductIds.Length == 0)
                {
                    ModelState.AddModelError("", "No products selected.");
                    return View("CheckoutCustom", new OrderStartViewModel { Products = [], Submit = model });
                }

                if (!ModelState.IsValid) return View("CheckoutCustom", new OrderStartViewModel { Products = await _context.Products.Where(p => model.ProductIds.Contains(p.ProductID)).ToListAsync(), Submit = model });

                string? handBase64 = null;
                var handFile = Request.Form.Files.GetFile("HandPhoto") ?? (Request.Form.Files.Count > 0 ? Request.Form.Files[0] : null);
                if (handFile != null)
                {
                    await using var ms = new MemoryStream();
                    await handFile.CopyToAsync(ms);
                    handBase64 = "data:" + handFile.ContentType + ";base64," + Convert.ToBase64String(ms.ToArray());
                }

                var inspirations = new List<string>();
                var inspFiles = Request.Form.Files.Where(f => f.Name == "InspirationImages").ToList();
                foreach (var f in inspFiles)
                {
                    await using var ms2 = new MemoryStream();
                    await f.CopyToAsync(ms2);
                    inspirations.Add("data:" + f.ContentType + ";base64," + Convert.ToBase64String(ms2.ToArray()));
                }

                var payload = new { designNotes = model.DesignNotes, inspirations = inspirations };
                var inspJson = System.Text.Json.JsonSerializer.Serialize(payload);

                var order = new Order
                {
                    UserID = user.UserID,
                    Order_Date = DateTime.UtcNow,
                    Full_Name = model.FullName,
                    Social_Account = model.SocialAccount,
                    Delivery_Location = model.DeliveryLocation,
                    Delivery_Method = model.DeliveryMethod,
                    Payment_Method = string.IsNullOrWhiteSpace(model.PaymentMethod) ? "PayMongo" : model.PaymentMethod,
                    Contact_Number = !string.IsNullOrWhiteSpace(model.ContactNumber) ? model.ContactNumber : "0",
                    Hand_Photo = handBase64 ?? string.Empty,
                    Thumb_Photo = inspJson,
                    Order_Type = 'C',
                    Status = "Pending"
                };

                _context.Add(order);
                await _context.SaveChangesAsync();

                if (model.ProductIds != null && model.ProductIds.Length > 0)
                {
                    for (int i = 0; i < model.ProductIds.Length; i++)
                    {
                        var pid = model.ProductIds[i];
                        int qty = 1;
                        if (model.Quantities != null && model.Quantities.Length > i && model.Quantities[i] > 0) qty = model.Quantities[i];
                        var oi = new OrderItem { OrderID = order.OrderID, ProductID = pid, Quantity = qty };
                        _context.Add(oi);
                    }
                }
                await _context.SaveChangesAsync();

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View("CheckoutCustom", new OrderStartViewModel { Products = await _context.Products.Where(p => model.ProductIds.Contains(p.ProductID)).ToListAsync(), Submit = model });
            }
        }

        [HttpGet("OrderDetails/{id}")]
        public async Task<IActionResult> OrderDetails(int id)
        {
            var email = User.Identity?.Name;
            var user = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return RedirectToAction("Login", "Account");

            var order = await _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.OrderID == id && o.UserID == user.UserID);

            if (order == null) return NotFound();

            ViewBag.UserEmail = user.Email;
            return View(order);
        }
    }
}
