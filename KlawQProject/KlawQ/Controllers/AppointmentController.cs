using KlawQ.Data;
using KlawQ.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KlawQ.Controllers
{
    [Route("[controller]")]
    [Authorize(Roles = "User,Admin")]
    public class AppointmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AppointmentController> _logger;

        public AppointmentController(ApplicationDbContext context, ILogger<AppointmentController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [Route("create")]
        public IActionResult CreateAppointment([FromForm] Appointment appointment)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            _context.Appointments.Add(appointment);
            _context.SaveChanges();

            return RedirectToAction("Index");
        }

        // Accepts JSON and returns created appointment id for AJAX flow
        [HttpPost]
        [Route("create-json")]
        public async Task<IActionResult> CreateAppointmentJson([FromBody] Appointment appointment)
        {
            try
            {
                // Get current user's ID from Users table
                int userId = 0;
                if (User.Identity?.IsAuthenticated == true)
                {
                    var userEmail = User.Identity.Name;
                    var user = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Email == userEmail);
                    if (user != null)
                        userId = user.UserID;
                }

                // If we couldn't find user in custom Users table, use a default or skip FK
                if (userId > 0)
                    appointment.UserId = userId;

                // Check ModelState validity
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList();
                    return BadRequest(new { error = "Validation failed", details = errors });
                }

                _context.Appointments.Add(appointment);
                await _context.SaveChangesAsync();
                return Ok(new { AppId = appointment.AppId });
            }
            catch (Exception ex)
            {
                var innerError = ex.InnerException?.Message ?? ex.Message;
                return BadRequest(new { error = innerError });
            }
        }

        [HttpPost("UpdateDownPayment")]
        public async Task<IActionResult> UpdateDownPayment(int id)
        {
            try
            {
                var appointment = await _context.Appointments.FindAsync(id);
                if (appointment == null)
                {
                    _logger?.LogWarning("UpdateDownPayment: appointment {Id} not found", id);
                    return NotFound(new { success = false, error = "Appointment not found" });
                }

                appointment.Down_Payment_Paid = true;
                _context.Appointments.Update(appointment);
                await _context.SaveChangesAsync();

                _logger?.LogInformation("UpdateDownPayment: appointment {Id} marked as paid", id);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "UpdateDownPayment failed for id {Id}", id);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

    [HttpGet("debug")]
    public IActionResult DebugUser()
    {
        var userName = User.Identity?.Name ?? "Not authenticated";
        return Ok(new { authenticatedUser = userName });
    }

    [HttpPost("fetch-image")]
    public async Task<IActionResult> FetchImageFromUrl([FromBody] FetchImageRequest body)
    {
        if (body == null || string.IsNullOrWhiteSpace(body.Url))
            return BadRequest(new { error = "url required" });

        try
        {
            using var http = new System.Net.Http.HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("KlawQ-App/1.0");
            var resp = await http.GetAsync(body.Url);
            if (!resp.IsSuccessStatusCode) return BadRequest(new { error = "Failed to fetch image" });

            var contentType = resp.Content.Headers.ContentType?.MediaType ?? "";
            if (!contentType.StartsWith("image/")) return BadRequest(new { error = "URL is not an image" });

            var bytes = await resp.Content.ReadAsByteArrayAsync();
            if (bytes.Length > 2 * 1024 * 1024) return BadRequest(new { error = "Image too large" });

            var parts = contentType.Split('/');
            var ext = parts.Length > 1 ? parts[1] : "jpg";
            if (ext.Contains("jpeg")) ext = "jpg";

            var uploads = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot", "uploads", "appointment_refs");
            System.IO.Directory.CreateDirectory(uploads);
            var fname = System.Guid.NewGuid().ToString() + "." + ext;
            var fpath = System.IO.Path.Combine(uploads, fname);
            await System.IO.File.WriteAllBytesAsync(fpath, bytes);

            var url = "/uploads/appointment_refs/" + fname;
            return Ok(new { url });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "FetchImage failed for url {Url}", body?.Url);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    public class FetchImageRequest { public string Url { get; set; } = string.Empty; }
}
}
