using KlawQ.Data;
using KlawQ.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace KlawQ.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("")]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("PortfolioManager")]
        public async Task<IActionResult> PortfolioManager()
        {
            var products = await _context.Products.OrderBy(p => p.ProductID).ToListAsync();
            return View(products);
        }

        [HttpPost("PortfolioManager/create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePortfolioItem(Products product, IFormFile imageFile)
        {
            if (imageFile != null && imageFile.Length > 0)
            {
                var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "gallery");
                Directory.CreateDirectory(uploads);
                var fileName = System.Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                var filePath = Path.Combine(uploads, fileName);
                await using var fs = new FileStream(filePath, FileMode.Create);
                await imageFile.CopyToAsync(fs);
                product.Product_Image = "/images/gallery/" + fileName;
                ModelState.Remove(nameof(Products.Product_Image));
            }
            else
            {
                ModelState.AddModelError("imageFile", "Please upload an image file.");
            }

            if (!ModelState.IsValid)
            {
                var currentProducts = await _context.Products.OrderBy(p => p.ProductID).ToListAsync();
                return View("PortfolioManager", currentProducts);
            }

            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            return RedirectToAction("PortfolioManager");
        }

        [HttpPost("PortfolioManager/delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePortfolioItem(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrEmpty(product.Product_Image) && product.Product_Image.StartsWith("/images/gallery/"))
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", product.Product_Image.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            return RedirectToAction("PortfolioManager");
        }
    }
}
