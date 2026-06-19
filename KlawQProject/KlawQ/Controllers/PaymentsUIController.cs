using Microsoft.AspNetCore.Mvc;

namespace KlawQ.Controllers
{
    /// <summary>
    /// Controller managing user interface rendering for development payment simulations.
    /// Covers Inheritance: Inherits from the base Controller class.
    /// </summary>
    [Route("Payments")]
    public class PaymentsUIController : Controller
    {
        // GET /Payments/MockPage?paymentId=...&appointmentId=...
        // Covers Abstraction: Employs standard ViewData property transfers to pass routing data to the user layout without leaking concrete database context details.
        // Covers Polymorphism: Returns IActionResult (resolving to ViewResult).
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
