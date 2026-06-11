using KlawQ.Data;
using Microsoft.AspNetCore.Mvc;

namespace KlawQ.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public PaymentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public class MockPaymentRequest
        {
            public int AppointmentId { get; set; }
            public decimal Amount { get; set; }
        }

        [HttpPost("gcash-mock")]
        public IActionResult CreateMockPayment([FromBody] MockPaymentRequest req)
        {
            // Return a fake payment URL that points back to confirm endpoint
            var tid = System.Guid.NewGuid().ToString();
            var paymentUrl = $"/api/payments/gcash-mock/confirm?tid={tid}&appointmentId={req.AppointmentId}";
            return Ok(new { success = true, transactionId = tid, paymentUrl });
        }

        [HttpGet("gcash-mock/confirm")]
        public async Task<IActionResult> ConfirmMockPayment([FromQuery] string tid, [FromQuery] int appointmentId)
        {
            // Simulate immediate success: mark the appointment down payment as paid
            var ap = await _context.Appointments.FindAsync(appointmentId);
            if (ap == null) return BadRequest(new { success = false, error = "Appointment not found" });
            ap.Down_Payment_Paid = true;
            await _context.SaveChangesAsync();
            return Ok(new { success = true, transactionId = tid });
        }
    }
}
