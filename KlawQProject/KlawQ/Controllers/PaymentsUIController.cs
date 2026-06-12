using Microsoft.AspNetCore.Mvc;

namespace KlawQ.Controllers
{
    [Route("Payments")]
    public class PaymentsUIController : Controller
    {
        // GET /Payments/MockPage?paymentId=...&appointmentId=...
        [HttpGet("MockPage")]
        public IActionResult MockPage(string paymentId, int appointmentId)
        {
            ViewData["PaymentId"] = paymentId;
            ViewData["AppointmentId"] = appointmentId;
            // Use the shared MockPage view
            return View("MockPage");
        }
    }
}
