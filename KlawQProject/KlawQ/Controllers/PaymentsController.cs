using KlawQ.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;

namespace KlawQ.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private static readonly ConcurrentDictionary<string, bool> _paymentStatus = new();

        public PaymentsController(ApplicationDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        public class CreatePaymentRequest
        {
            public int AppointmentId { get; set; }
            public decimal Amount { get; set; }
            public string? ReturnUrl { get; set; }
        }

        public class CreatePaymentResponse
        {
            public bool Success { get; set; }
            public string? PaymentId { get; set; }
            public string? PaymentUrl { get; set; }
            public string? Error { get; set; }
        }

        // Create payment: uses real GCash endpoint if configured, otherwise falls back to mock
        [HttpPost("create")]
        public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest req)
        {
            // Basic validation
            if (req.AppointmentId <= 0 || req.Amount <= 0) return BadRequest(new CreatePaymentResponse { Success = false, Error = "invalid request" });

            // If no GCash config present, return a mock payment URL that calls our confirm endpoint
            var gcashEndpoint = _config["GCash:CreatePaymentUrl"];
            if (string.IsNullOrWhiteSpace(gcashEndpoint))
            {
                var pid = Guid.NewGuid().ToString();
                // Use a friendly mock payment UI page instead of raw JSON endpoint
                var paymentUrl = $"/Payments/MockPage?paymentId={pid}&appointmentId={req.AppointmentId}";
                _paymentStatus[pid] = false; // not paid yet
                return Ok(new CreatePaymentResponse { Success = true, PaymentId = pid, PaymentUrl = paymentUrl });
            }

            // Otherwise attempt to call configured GCash API (caller must set credentials in config)
            try
            {
                using var client = new HttpClient();
                // Example request body; real GCash API will differ. Caller should populate GCash:ApiKey etc in config.
                var payload = new
                {
                    amount = req.Amount,
                    reference = req.AppointmentId,
                    return_url = req.ReturnUrl ?? $"/Appointment/PaymentReturn?appointmentId={req.AppointmentId}"
                };

                // Add authorization header if configured
                var apiKey = _config["GCash:ApiKey"];
                if (!string.IsNullOrWhiteSpace(apiKey)) client.DefaultRequestHeaders.Add("Authorization", apiKey);

                var res = await client.PostAsJsonAsync(gcashEndpoint, payload);
                if (!res.IsSuccessStatusCode)
                {
                    var txt = await res.Content.ReadAsStringAsync();
                    return StatusCode(502, new CreatePaymentResponse { Success = false, Error = "GCash error: " + txt });
                }

                // Expect GCash to return JSON with payment_url and id (this is an example)
                var obj = await res.Content.ReadFromJsonAsync<JsonElement>();
                string? paymentUrl = null, paymentId = null;
                if (obj.ValueKind != JsonValueKind.Undefined)
                {
                    if (obj.TryGetProperty("payment_url", out var pu) && pu.ValueKind == JsonValueKind.String) paymentUrl = pu.GetString();
                    if (obj.TryGetProperty("id", out var pi) && pi.ValueKind == JsonValueKind.String) paymentId = pi.GetString();
                }

                if (string.IsNullOrWhiteSpace(paymentUrl) || string.IsNullOrWhiteSpace(paymentId))
                    return StatusCode(502, new CreatePaymentResponse { Success = false, Error = "Unexpected GCash response" });

                _paymentStatus[paymentId] = false;
                return Ok(new CreatePaymentResponse { Success = true, PaymentId = paymentId, PaymentUrl = paymentUrl });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new CreatePaymentResponse { Success = false, Error = ex.Message });
            }
        }

        // Mock confirm endpoint for local dev/testing
        [HttpGet("mock-confirm")]
        public async Task<IActionResult> MockConfirm([FromQuery] string paymentId, [FromQuery] int appointmentId)
        {
            if (string.IsNullOrWhiteSpace(paymentId)) return BadRequest(new { success = false, error = "missing paymentId" });
            _paymentStatus[paymentId] = true;

            var ap = await _context.Appointments.FindAsync(appointmentId);
            if (ap != null)
            {
                ap.Down_Payment_Paid = true;
                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true, paymentId });
        }

        // Polling endpoint for client to check payment status
        [HttpGet("status")] 
        public IActionResult Status([FromQuery] string paymentId)
        {
            if (string.IsNullOrWhiteSpace(paymentId)) return BadRequest(new { success = false, error = "missing paymentId" });
            var paid = _paymentStatus.TryGetValue(paymentId, out var v) && v;
            return Ok(new { success = true, paid });
        }

        // Webhook endpoint for real GCash to POST payment notifications
        [HttpPost("notify")]
        public async Task<IActionResult> Notify([FromBody] JsonElement payload)
        {
            // This endpoint should validate signature and parse payload according to GCash docs.
            // For now, attempt to extract paymentId and appointmentId
            try
            {
                string? pid = null;
                int? appId = null;
                if (payload.ValueKind == JsonValueKind.Object)
                {
                    if (payload.TryGetProperty("payment_id", out var p) && p.ValueKind == JsonValueKind.String) pid = p.GetString();
                    if (payload.TryGetProperty("reference", out var r) && r.ValueKind == JsonValueKind.Number) appId = r.GetInt32();
                }

                if (!string.IsNullOrWhiteSpace(pid)) _paymentStatus[pid] = true;

                if (appId.HasValue)
                {
                    var ap = await _context.Appointments.FindAsync(appId.Value);
                    if (ap != null)
                    {
                        ap.Down_Payment_Paid = true;
                        await _context.SaveChangesAsync();
                    }
                }

                return Ok(new { success = true });
            }
            catch
            {
                return BadRequest(new { success = false });
            }
        }
    }
}
