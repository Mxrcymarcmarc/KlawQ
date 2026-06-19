using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KlawQ.Data;
using KlawQ.Models;
using KlawQ.Services; // Injected to reference your PayMongo service mappings
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KlawQ.Controllers
{
    /// <summary>
    /// Controller managing client orders and transaction checkouts.
    /// Covers Inheritance: Inherits from base Controller class to share basic action rendering behaviors.
    /// Covers Abstraction: Interfaces with the PayMongoService and ApplicationDbContext abstractions.
    /// </summary>
    [Route("[controller]")]
    [Authorize]
    public class OrderController(ApplicationDbContext context, PayMongoService payMongoService) : Controller
    {
        private readonly ApplicationDbContext _context = context;
        private readonly PayMongoService _payMongoService = payMongoService; // Private tracking reference descriptor
        private static readonly char[] SplitChars = [ ',', '&' ];


        [HttpGet("Start")]
        public async Task<IActionResult> Start([FromQuery] int[] productIds, [FromQuery] int[] qtys, int productId = 0)
        {
            var ids = productIds?.Where(i => i > 0).ToList() ?? [];
            if (productId > 0) ids.Insert(0, productId);

            List<Products> products = [];
            if (ids.Count > 0) products = await _context.Products.Where(p => ids.Contains(p.ProductID)).ToListAsync();

            var vm = new OrderStartViewModel { Products = products };

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
                    if (handFile != null)
                    {
                        var ext = Path.GetExtension(handFile.FileName).ToLowerInvariant();
                        if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                        {
                            ModelState.AddModelError("", "Only .png, .jpg, and .jpeg files are allowed for hand photo.");
                        }
                    }
                    if (thumbFile != null)
                    {
                        var ext = Path.GetExtension(thumbFile.FileName).ToLowerInvariant();
                        if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                        {
                            ModelState.AddModelError("", "Only .png, .jpg, and .jpeg files are allowed for thumb photo.");
                        }
                    }
                }

                if (!ModelState.IsValid) return View("Start", new OrderStartViewModel { Products = await _context.Products.Where(p => ids.Contains(p.ProductID)).ToListAsync(), Submit = model });

                if (Request.Form.Files?.Count > 0 && ModelState.IsValid)
                {
                    var handFile = Request.Form.Files.GetFile("HandPhoto");
                    var thumbFile = Request.Form.Files.GetFile("ThumbPhoto");
                    if (handFile != null) { await using var ms = new MemoryStream(); await handFile.CopyToAsync(ms); handBase64 = "data:" + handFile.ContentType + ";base64," + Convert.ToBase64String(ms.ToArray()); }
                    if (thumbFile != null) { await using var ms2 = new MemoryStream(); await thumbFile.CopyToAsync(ms2); thumbBase64 = "data:" + thumbFile.ContentType + ";base64," + Convert.ToBase64String(ms2.ToArray()); }
                }

                // Calculate complete purchase cost matrix fields to pass over onto the PayMongo API gateway
                var selectedProducts = await _context.Products.Where(p => ids.Contains(p.ProductID)).ToListAsync();
                var qtysRaw = Request.Form["Quantities"].ToString();
                List<int> qtys = [];
                if (!string.IsNullOrWhiteSpace(qtysRaw))
                {
                    qtys = qtysRaw.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries).Select(s => int.TryParse(s, out var x) ? x : 0).Where(x => x > 0).ToList();
                }

                decimal calculatedBillTotal = 0;
                for (int i = 0; i < ids.Count; i++)
                {
                    var matchingProduct = selectedProducts.FirstOrDefault(p => p.ProductID == ids[i]);
                    if (matchingProduct != null)
                    {
                        int targetQty = (i < qtys.Count && qtys[i] > 0) ? qtys[i] : 1;
                        calculatedBillTotal += (matchingProduct.Product_Price * targetQty);
                    }
                }

                var order = new Order
                {
                    UserID = user.UserID,
                    Order_Date = DateTime.UtcNow,
                    Full_Name = model.FullName,
                    Social_Account = model.SocialAccount,
                    Delivery_Location = model.DeliveryLocation,
                    Delivery_Method = model.DeliveryMethod,
                    Payment_Method = "PayMongo", // Force transaction classification field explicitly
                    Contact_Number = !string.IsNullOrWhiteSpace(model.ContactNumber) ? model.ContactNumber : "0",
                    Hand_Photo = handBase64 ?? string.Empty,
                    Thumb_Photo = thumbBase64 ?? string.Empty,
                    Order_Type = 'P',
                    Status = "Payment Pending" // Hidden block until verification completes successfully
                };

                _context.Add(order);
                await _context.SaveChangesAsync();

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

                // PayMongo redirect initiation route generator
                string domain = $"{Request.Scheme}://{Request.Host}";
                string successUrl = $"{domain}/Order/OrderSuccess?orderId={order.OrderID}";
                string cancelUrl = $"{domain}/Order/OrderCancelled?orderId={order.OrderID}";

                string checkoutUrl = await _payMongoService.CreateCheckoutSessionAsync(
                    amountInPhp: calculatedBillTotal,
                    description: $"Nail Order #{order.OrderID} Checkout Checkout Deposit for {order.Full_Name}",
                    successUrl: successUrl,
                    cancelUrl: cancelUrl
                );

                return Redirect(checkoutUrl); // Bounce customer out onto GCash / PayMongo checkout interfaces
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Payment processing interruption exception notice: {ex.Message}");
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

                var handFile = Request.Form.Files.GetFile("HandPhoto") ?? (Request.Form.Files.Count > 0 ? Request.Form.Files[0] : null);
                if (handFile != null)
                {
                    var ext = Path.GetExtension(handFile.FileName).ToLowerInvariant();
                    if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                    {
                        ModelState.AddModelError("", "Only .png, .jpg, and .jpeg files are allowed.");
                    }
                }

                if (!ModelState.IsValid) return View("CheckoutStandard", new OrderStartViewModel { Products = await _context.Products.Where(p => model.ProductIds.Contains(p.ProductID)).ToListAsync(), Submit = model });

                string? handBase64 = null;
                if (handFile != null && ModelState.IsValid)
                {
                    await using var ms = new MemoryStream();
                    await handFile.CopyToAsync(ms);
                    handBase64 = "data:" + handFile.ContentType + ";base64," + Convert.ToBase64String(ms.ToArray());
                }

                // Calculate cumulative checkout price
                var selectedProducts = await _context.Products.Where(p => model.ProductIds.Contains(p.ProductID)).ToListAsync();
                decimal calculatedBillTotal = 0;
                for (int i = 0; i < model.ProductIds.Length; i++)
                {
                    var matchingProduct = selectedProducts.FirstOrDefault(p => p.ProductID == model.ProductIds[i]);
                    if (matchingProduct != null)
                    {
                        int targetQty = (model.Quantities != null && model.Quantities.Length > i && model.Quantities[i] > 0) ? model.Quantities[i] : 1;
                        calculatedBillTotal += (matchingProduct.Product_Price * targetQty);
                    }
                }

                var order = new Order
                {
                    UserID = user.UserID,
                    Order_Date = DateTime.UtcNow,
                    Full_Name = model.FullName,
                    Social_Account = model.SocialAccount,
                    Delivery_Location = model.DeliveryLocation,
                    Delivery_Method = model.DeliveryMethod,
                    Payment_Method = "PayMongo",
                    Contact_Number = !string.IsNullOrWhiteSpace(model.ContactNumber) ? model.ContactNumber : "0",
                    Hand_Photo = handBase64 ?? string.Empty,
                    Thumb_Photo = string.Empty,
                    Order_Type = 'P',
                    Status = "Payment Pending"
                };

                _context.Add(order);
                await _context.SaveChangesAsync();

                if (model.ProductIds?.Length > 0)
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

                // PayMongo redirect initiation route generator
                string domain = $"{Request.Scheme}://{Request.Host}";
                string successUrl = $"{domain}/Order/OrderSuccess?orderId={order.OrderID}";
                string cancelUrl = $"{domain}/Order/OrderCancelled?orderId={order.OrderID}";

                string checkoutUrl = await _payMongoService.CreateCheckoutSessionAsync(
                    amountInPhp: calculatedBillTotal,
                    description: $"Standard Press-On Purchase Order #{order.OrderID} for {order.Full_Name}",
                    successUrl: successUrl,
                    cancelUrl: cancelUrl
                );

                return Redirect(checkoutUrl);
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

                var selectedProductIds = model.ProductIds ?? [];

                if (!ModelState.IsValid) return View("CheckoutCustom", new OrderStartViewModel { Products = await _context.Products.Where(p => selectedProductIds.Contains(p.ProductID)).ToListAsync(), Submit = model });

                var handFile = Request.Form.Files.GetFile("HandPhoto") ?? (Request.Form.Files.Count > 0 ? Request.Form.Files[0] : null);
                if (handFile != null)
                {
                    var ext = Path.GetExtension(handFile.FileName).ToLowerInvariant();
                    if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                    {
                        ModelState.AddModelError("", "Only .png, .jpg, and .jpeg files are allowed for hand photo.");
                    }
                }

                var inspFiles = Request.Form.Files.Where(f => f.Name == "InspirationImages").ToList();
                foreach (var f in inspFiles)
                {
                    var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
                    if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                    {
                        ModelState.AddModelError("", $"Only .png, .jpg, and .jpeg files are allowed for inspiration images. Invalid file: {f.FileName}");
                    }
                }

                if (!ModelState.IsValid) return View("CheckoutCustom", new OrderStartViewModel { Products = await _context.Products.Where(p => selectedProductIds.Contains(p.ProductID)).ToListAsync(), Submit = model });

                string? handBase64 = null;
                if (handFile != null && ModelState.IsValid)
                {
                    await using var ms = new MemoryStream();
                    await handFile.CopyToAsync(ms);
                    handBase64 = "data:" + handFile.ContentType + ";base64," + Convert.ToBase64String(ms.ToArray());
                }

                var inspirations = new List<string>();
                if (ModelState.IsValid)
                {
                    foreach (var f in inspFiles)
                    {
                        await using var ms2 = new MemoryStream();
                        await f.CopyToAsync(ms2);
                        inspirations.Add("data:" + f.ContentType + ";base64," + Convert.ToBase64String(ms2.ToArray()));
                    }
                }

                var payload = new { designNotes = model.DesignNotes, inspirations };
                var inspJson = JsonSerializer.Serialize(payload);

                var order = new Order
                {
                    UserID = user.UserID,
                    Order_Date = DateTime.UtcNow,
                    Full_Name = model.FullName,
                    Social_Account = model.SocialAccount,
                    Delivery_Location = model.DeliveryLocation,
                    Delivery_Method = model.DeliveryMethod,
                    Payment_Method = "PayMongo",
                    Contact_Number = !string.IsNullOrWhiteSpace(model.ContactNumber) ? model.ContactNumber : "0",
                    Hand_Photo = handBase64 ?? string.Empty,
                    Thumb_Photo = inspJson,
                    Order_Type = 'C',
                    Status = "Pending"
                };

                _context.Add(order);
                await _context.SaveChangesAsync();

                if (model.ProductIds?.Length > 0)
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

                // Clean up cart items that were submitted in the custom request
                var cart = await _context.Carts.FirstOrDefaultAsync(c => c.UserID == order.UserID);
                if (cart != null && model.ProductIds?.Length > 0)
                {
                    var cartItemsToRemove = await _context.CartItems
                        .Where(ci => ci.CartId == cart.CartId && model.ProductIds.Contains(ci.ProductID))
                        .ToListAsync();

                    if (cartItemsToRemove.Count > 0)
                    {
                        _context.CartItems.RemoveRange(cartItemsToRemove);
                        await _context.SaveChangesAsync();
                    }
                }

                return RedirectToAction("OrderSuccessCustom", new { orderId = order.OrderID });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View("CheckoutCustom", new OrderStartViewModel { Products = await _context.Products.Where(p => model.ProductIds.Contains(p.ProductID)).ToListAsync(), Submit = model });
            }
        }

        [HttpGet("OrderSuccessCustom")]
        public async Task<IActionResult> OrderSuccessCustom([FromQuery] int orderId)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == orderId);
            if (order == null) return NotFound();

            const string htmlContent = @"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset=""utf-8"">
                <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                <title>Custom Request Submitted</title>
                <link rel=""preconnect"" href=""https://fonts.googleapis.com"">
                <link rel=""preconnect"" href=""https://fonts.gstatic.com"" crossorigin>
                <link href=""https://fonts.googleapis.com/css2?family=Montserrat:wght@400;500;600;700&display=swap"" rel=""stylesheet"">
                <link rel=""stylesheet"" href=""https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css"">
                <style>
                    body {
                        background-color: #fdf5f6;
                        font-family: 'Montserrat', sans-serif;
                        display: flex;
                        justify-content: center;
                        align-items: center;
                        height: 100vh;
                        margin: 0;
                    }
                    .card {
                        background-color: #ffffff;
                        padding: 40px;
                        border-radius: 20px;
                        box-shadow: 0 10px 30px rgba(121, 85, 72, 0.08);
                        border: 1.5px solid #fce4ec;
                        text-align: center;
                        max-width: 450px;
                        width: 90%;
                        box-sizing: border-box;
                    }
                    .success-icon {
                        color: #7a5046;
                        font-size: 54px;
                        margin-bottom: 24px;
                        display: inline-block;
                    }
                    h3 {
                        color: #7a5046;
                        font-size: 24px;
                        font-weight: 700;
                        margin-top: 0;
                        margin-bottom: 12px;
                    }
                    p {
                        color: #8a6a62;
                        font-size: 15px;
                        line-height: 1.6;
                        margin-top: 0;
                        margin-bottom: 20px;
                    }
                    .redirect-text {
                        font-size: 13px;
                        color: #a08077;
                        font-style: italic;
                        margin-bottom: 28px;
                    }
                    .btn {
                        background-color: #7a5046;
                        color: #ffffff;
                        padding: 12px 32px;
                        border-radius: 24px;
                        text-decoration: none;
                        font-weight: 600;
                        font-size: 15px;
                        display: inline-block;
                        box-shadow: 0 6px 12px rgba(122,80,70,0.15);
                        transition: all 0.2s ease;
                    }
                    .btn:hover {
                        background-color: #5d3a31;
                        transform: translateY(-2px);
                    }
                </style>
                <script>
                    setTimeout(function() {
                        window.location.href = '/Gallery';
                    }, 4000);
                </script>
            </head>
            <body>
                <div class=""card"">
                    <i class=""fas fa-paper-plane success-icon""></i>
                    <h3>Request Submitted!</h3>
                    <p>Your custom press-on design request has been successfully recorded. The studio will review your request details shortly!</p>
                    <p class=""redirect-text"">Redirecting you to the Gallery in 4 seconds...</p>
                    <a href=""/Gallery"" class=""btn"">Return to Gallery</a>
                </div>
            </body>
            </html>";

            return Content(htmlContent, "text/html; charset=utf-8");
        }

        // Web-hook endpoint: transaction success verification card
        [HttpGet("OrderSuccess")]
        public async Task<IActionResult> OrderSuccess([FromQuery] int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.OrderID == orderId);
            if (order == null) return NotFound();

            order.Status = "Pending"; // Update state context from pending to processing cleanly

            // Clean up cart items that were paid for
            var cart = await _context.Carts.FirstOrDefaultAsync(c => c.UserID == order.UserID);
            if (cart != null)
            {
                var productIds = order.Items.Select(oi => oi.ProductID);
                var cartItemsToRemove = await _context.CartItems
                    .Where(ci => ci.CartId == cart.CartId && productIds.Contains(ci.ProductID))
                    .ToListAsync();

                if (cartItemsToRemove.Count > 0)
                {
                    _context.CartItems.RemoveRange(cartItemsToRemove);
                }
            }

            await _context.SaveChangesAsync();

            const string htmlContent = @"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset=""utf-8"">
                <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                <title>Order Confirmed</title>
                <link rel=""preconnect"" href=""https://fonts.googleapis.com"">
                <link rel=""preconnect"" href=""https://fonts.gstatic.com"" crossorigin>
                <link href=""https://fonts.googleapis.com/css2?family=Montserrat:wght@400;500;600;700&display=swap"" rel=""stylesheet"">
                <link rel=""stylesheet"" href=""https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css"">
                <style>
                    body {
                        background-color: #fdf5f6;
                        font-family: 'Montserrat', sans-serif;
                        display: flex;
                        justify-content: center;
                        align-items: center;
                        height: 100vh;
                        margin: 0;
                    }
                    .card {
                        background-color: #ffffff;
                        padding: 40px;
                        border-radius: 20px;
                        box-shadow: 0 10px 30px rgba(121, 85, 72, 0.08);
                        border: 1.5px solid #fce4ec;
                        text-align: center;
                        max-width: 450px;
                        width: 90%;
                        box-sizing: border-box;
                    }
                    .success-icon {
                        color: #2e7d32;
                        font-size: 54px;
                        margin-bottom: 24px;
                        display: inline-block;
                    }
                    h3 {
                        color: #7a5046;
                        font-size: 24px;
                        font-weight: 700;
                        margin-top: 0;
                        margin-bottom: 12px;
                    }
                    p {
                        color: #8a6a62;
                        font-size: 15px;
                        line-height: 1.6;
                        margin-top: 0;
                        margin-bottom: 20px;
                    }
                    .redirect-text {
                        font-size: 13px;
                        color: #a08077;
                        font-style: italic;
                        margin-bottom: 28px;
                    }
                    .btn {
                        background-color: #7a5046;
                        color: #ffffff;
                        padding: 12px 32px;
                        border-radius: 24px;
                        text-decoration: none;
                        font-weight: 600;
                        font-size: 15px;
                        display: inline-block;
                        box-shadow: 0 6px 12px rgba(122,80,70,0.15);
                        transition: all 0.2s ease;
                    }
                    .btn:hover {
                        background-color: #5d3a31;
                        transform: translateY(-2px);
                    }
                </style>
                <script>
                    setTimeout(function() {
                        window.location.href = '/Home/Index';
                    }, 4000);
                </script>
            </head>
            <body>
                <div class=""card"">
                    <i class=""fas fa-check-circle success-icon""></i>
                    <h3>Payment Successful!</h3>
                    <p>Your custom press-on order downpayment has been received securely. The studio has locked in your order profile!</p>
                    <p class=""redirect-text"">Redirecting you back home automatically in 4 seconds...</p>
                    <a href=""/Home/Index"" class=""btn"">Return Home</a>
                </div>
            </body>
            </html>";

            return Content(htmlContent, "text/html; charset=utf-8");
        }

        // Web-hook endpoint: cascading transaction clean-up on failure
        [HttpGet("OrderCancelled")]
        public async Task<IActionResult> OrderCancelled([FromQuery] int orderId)
        {
            var order = await _context.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.OrderID == orderId);
            if (order != null)
            {
                bool isCustom = !string.IsNullOrWhiteSpace(order.Thumb_Photo) && order.Thumb_Photo.TrimStart().StartsWith('{');
                if (!isCustom)
                {
                    // Wipe the phantom record from database to prevent order inflation stubs
                    _context.OrderItems.RemoveRange(order.Items);
                    _context.Orders.Remove(order);
                    await _context.SaveChangesAsync();
                    return Content("<h3 style='color:#8b4b3b; text-align:center; margin-top:50px;'>Checkout session closed. Your purchase request was not filed.</h3>", "text/html");
                }
            }

            return RedirectToAction("OrderHistory", "Home");
        }

        [HttpGet("OrderDetails/{id}")]
        public async Task<IActionResult> OrderDetails(int id)
        {
            var email = User.Identity?.Name;
            var user = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return RedirectToAction("Login", "Account");

            var order = await _context.Orders
                .IgnoreQueryFilters()
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.OrderID == id && o.UserID == user.UserID);

            if (order == null) return NotFound();

            ViewBag.UserEmail = user.Email;
            return View(order);
        }

        [HttpPost("ConfirmReceived")]
        public async Task<IActionResult> ConfirmReceived([FromForm] int orderId)
        {
            if (User.Identity?.IsAuthenticated is not true)
            {
                return Unauthorized();
            }

            var email = User.Identity.Name;
            var user = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                return Unauthorized();
            }

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == orderId && o.UserID == user.UserID);
            if (order == null)
            {
                return NotFound("Order not found.");
            }

            order.Status = "Completed";
            await _context.SaveChangesAsync();

            return Ok();
        }

        [Authorize(Roles = "User")]
        [HttpGet("PayOrder/{orderId}")]
        public async Task<IActionResult> PayOrder(int orderId)
        {
            var order = await _context.Orders
                .IgnoreQueryFilters()
                .Include(o => o.Items)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderID == orderId);

            if (order == null)
            {
                return NotFound("Order not found.");
            }

            var email = User.Identity?.Name;
            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Email == email);
            if (userProfile == null || order.UserID != userProfile.UserID)
            {
                return Unauthorized();
            }

            if (order.Status != "Payment Pending")
            {
                return BadRequest("Order has already been paid or cancelled.");
            }

            decimal customPrice = 0;
            if (!string.IsNullOrWhiteSpace(order.Thumb_Photo) && order.Thumb_Photo.TrimStart().StartsWith('{'))
            {
                try
                {
                    var doc = JsonDocument.Parse(order.Thumb_Photo);
                    if (doc.RootElement.TryGetProperty("price", out var priceProp))
                    {
                        if (priceProp.ValueKind == JsonValueKind.Number) customPrice = priceProp.GetDecimal();
                        else if (priceProp.ValueKind == JsonValueKind.String && decimal.TryParse(priceProp.GetString(), out var parsedPrice)) customPrice = parsedPrice;
                    }
                }
                catch {}
            }

            decimal totalAmount = customPrice + (order.Items != null ? order.Items.Sum(item => (item.Product?.Product_Price ?? 0) * item.Quantity) : 0);

            if (totalAmount <= 0)
            {
                totalAmount = 150.00m;
            }

            string domain = $"{Request.Scheme}://{Request.Host}";
            string successUrl = $"{domain}/Order/OrderSuccess?orderId={order.OrderID}";
            string cancelUrl = $"{domain}/Order/OrderCancelled?orderId={order.OrderID}";

            string checkoutUrl = await _payMongoService.CreateCheckoutSessionAsync(
                amountInPhp: totalAmount,
                description: $"Nail Order #{order.OrderID} Checkout Deposit for {order.Full_Name}",
                successUrl: successUrl,
                cancelUrl: cancelUrl
            );

            return Redirect(checkoutUrl);
        }
    }
}