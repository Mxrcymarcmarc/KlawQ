using KlawQ.Data;
using KlawQ.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Threading.Tasks;

namespace KlawQ.Controllers
{
    /// <summary>
    /// Controller managing administrative product creation, listing, modification, and deletion.
    /// Covers Inheritance: Inherits from the base Controller class.
    /// Covers Abstraction: Obtains database and hosting environment interfaces via constructor injection.
    /// </summary>
    [Route("admin/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminProductsController(ApplicationDbContext context, IWebHostEnvironment env) : Controller
    {
        private readonly ApplicationDbContext _context = context;
        private readonly IWebHostEnvironment _env = env;


        // WEB VIEW ENDPOINT: List all products.
        // Covers Abstraction: Asynchronously queries database entities via EF Core.
        // Covers Polymorphism: Returns IActionResult, dynamically instantiated as ViewResult.
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var list = await _context.Products.ToListAsync();
            return View(list);
        }

        // WEB VIEW ENDPOINT: Render standard product creation form.
        [HttpGet("create")]
        public IActionResult Create() => View();

        // POST ACTION: Creates a new product, validates image parameters and processes physical storage.
        // Covers Encapsulation: Restricts uploaded file extensions to prevent executing binaries (.exe), enforcing field validation state before persistence.
        // Covers Abstraction: Uses file system directory helpers and stream writers without exposing absolute OS path details.
        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Products product, IFormFile? imageFile)
        {
            ModelState.Remove(nameof(Products.Product_Image));

            if (imageFile?.Length > 0)
            {
                var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                {
                    ModelState.AddModelError("imageFile", "Only .png, .jpg, and .jpeg files are allowed.");
                }
            }

            if (!ModelState.IsValid) return View(product);
            if (imageFile?.Length > 0)
            {
                var uploads = Path.Combine(_env.WebRootPath, "images", "gallery");
                Directory.CreateDirectory(uploads);
                var fileName = System.Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                var filePath = Path.Combine(uploads, fileName);
                await using (var fs = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(fs);
                }
                product.Product_Image = "/images/gallery/" + fileName;
            }
            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        // WEB VIEW ENDPOINT: Render single product configurations by ID for editing.
        [HttpGet("edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var p = await _context.Products.FindAsync(id);
            if (p == null) return NotFound();
            return View(p);
        }

        // POST ACTION: Updates existing product specifications and updates product photo file storage if necessary.
        // Covers Encapsulation: Protects product state by checking matching identifier properties and verifying extension rules before updating fields.
        [HttpPost("edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Products product, IFormFile? imageFile)
        {
            ModelState.Remove(nameof(Products.Product_Image));

            if (id != product.ProductID) return BadRequest();
            var existing = await _context.Products.FindAsync(id);
            if (existing == null) return NotFound();

            if (imageFile?.Length > 0)
            {
                var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                {
                    ModelState.AddModelError("imageFile", "Only .png, .jpg, and .jpeg files are allowed.");
                }
            }

            if (!ModelState.IsValid) return View(product);

            existing.Product_Name = product.Product_Name;
            existing.Product_Description = product.Product_Description;
            existing.Product_Price = product.Product_Price;
            existing.Product_Stock = product.Product_Stock;

            if (imageFile?.Length > 0)
            {
                var uploads = Path.Combine(_env.WebRootPath, "images", "gallery");
                Directory.CreateDirectory(uploads);
                var fileName = System.Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                var filePath = Path.Combine(uploads, fileName);
                await using (var fs = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(fs);
                }
                // Optionally delete old file if it is in images/gallery
                if (!string.IsNullOrEmpty(existing.Product_Image) && existing.Product_Image.StartsWith("/images/gallery/"))
                {
                    var oldPath = Path.Combine(_env.WebRootPath, existing.Product_Image.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }
                existing.Product_Image = "/images/gallery/" + fileName;
            }

            _context.Products.Update(existing);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        // POST ACTION: Soft deletes product by marking isDeleted flag true.
        // Covers Encapsulation: Controls deletion state modification on the product entity instance.
        [HttpPost("delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var p = await _context.Products.FindAsync(id);
            if (p == null) return NotFound();
            p.IsDeleted = true;
            _context.Products.Update(p);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }
    }
}
