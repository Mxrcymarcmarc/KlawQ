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
    [Route("admin/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        public AdminProductsController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var list = await _context.Products.ToListAsync();
            return View(list);
        }

        [HttpGet("create")]
        public IActionResult Create() => View();

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Products product, IFormFile imageFile)
        {
            if (!ModelState.IsValid) return View(product);
            if (imageFile != null && imageFile.Length > 0)
            {
                var uploads = Path.Combine(_env.WebRootPath, "images", "gallery");
                Directory.CreateDirectory(uploads);
                var fileName = System.Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                var filePath = Path.Combine(uploads, fileName);
                using (var fs = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(fs);
                }
                product.Product_Image = "/images/gallery/" + fileName;
            }
            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        [HttpGet("edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var p = await _context.Products.FindAsync(id);
            if (p == null) return NotFound();
            return View(p);
        }

        [HttpPost("edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Products product, IFormFile imageFile)
        {
            if (id != product.ProductID) return BadRequest();
            var existing = await _context.Products.FindAsync(id);
            if (existing == null) return NotFound();
            existing.Product_Name = product.Product_Name;
            existing.Product_Description = product.Product_Description;
            existing.Product_Price = product.Product_Price;
            existing.Product_Stock = product.Product_Stock;

            if (imageFile != null && imageFile.Length > 0)
            {
                var uploads = Path.Combine(_env.WebRootPath, "images", "gallery");
                Directory.CreateDirectory(uploads);
                var fileName = System.Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                var filePath = Path.Combine(uploads, fileName);
                using (var fs = new FileStream(filePath, FileMode.Create))
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

        [HttpPost("delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var p = await _context.Products.FindAsync(id);
            if (p == null) return NotFound();
            if (!string.IsNullOrEmpty(p.Product_Image) && p.Product_Image.StartsWith("/images/gallery/"))
            {
                var path = Path.Combine(_env.WebRootPath, p.Product_Image.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
            _context.Products.Remove(p);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }
    }
}
