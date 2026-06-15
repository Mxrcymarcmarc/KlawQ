using KlawQ.Data;
using KlawQ.Models;
using KlawQ.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace KlawQ.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class CalendarController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly PayMongoService _payMongoService;

        private static readonly TimeZoneInfo PhilippineTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");

        private const int ALMOST_FULL_THRESHOLD = 5;

        public CalendarController(ApplicationDbContext context, PayMongoService payMongoService)
        {
            _context = context;
            _payMongoService = payMongoService;
        }

        private static int GetMaxSlotsForDay(DayOfWeek day) => day switch
        {
            DayOfWeek.Tuesday => 0,
            DayOfWeek.Wednesday => 3,
            DayOfWeek.Saturday => 1,
            _ => 4
        };

        private static int[] GetBusinessHoursForDay(DayOfWeek day) => day switch
        {
            DayOfWeek.Tuesday => Array.Empty<int>(),
            DayOfWeek.Wednesday => new[] { 10, 14, 22 },
            DayOfWeek.Saturday => new[] { 18 },
            _ => new[] { 10, 14, 18, 21 }
        };

        private static string GetFormattedTime(int hour) =>
            new DateTime(2000, 1, 1, hour, 0, 0)
                .ToString("h:mm tt", CultureInfo.InvariantCulture);

        private static DateTime NowInPH() =>
            TimeZoneInfo.ConvertTime(DateTime.UtcNow, PhilippineTimeZone);

        private static DateTime TodayInPH() => NowInPH().Date;

        private static DateTime NormalizeToPH(DateTime dt)
        {
            if (dt.Kind == DateTimeKind.Utc)
                return TimeZoneInfo.ConvertTimeFromUtc(dt, PhilippineTimeZone);
            return DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
        }


        [HttpGet("current-view-status")]
        public async Task<IActionResult> GetCurrentCalendarView()
        {
            DateTime today = TodayInPH();
            var currentMonthDays = await GetDaysStatusForMonth(today.Year, today.Month);

            int availableDaysLeft = currentMonthDays
                .Count(d => d.IsAvailable && DateTime.Parse(d.DateString) >= today);

            var finalResponse = new List<CalendarDayStatus>(currentMonthDays);

            if (availableDaysLeft <= ALMOST_FULL_THRESHOLD)
            {
                DateTime nextMonthDate = today.AddMonths(1);
                var nextMonthDays = await GetDaysStatusForMonth(nextMonthDate.Year, nextMonthDate.Month);
                finalResponse.AddRange(nextMonthDays);
            }

            return Ok(finalResponse);
        }


        [HttpGet("day-slots-status")]
        public async Task<IActionResult> GetDaySlotsStatus([FromQuery] string chosenDate)
        {
            if (!DateTime.TryParseExact(chosenDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime parsedDate))
                return BadRequest("Invalid date format. Please use yyyy-MM-dd.");

            if (parsedDate.Date <= TodayInPH())
                return Ok(new List<object>());

            if (parsedDate.Date == TodayInPH().AddDays(1))
                return Ok(new List<object>());

            var bookingsForDay = await _context.Schedulers
                .Include(s => s.Appointment)
                .Where(s => s.Appointment_Date.Date == parsedDate.Date)
                .ToListAsync();

            int[] businessHours = GetBusinessHoursForDay(parsedDate.DayOfWeek);
            DateTime nowPH = NowInPH();
            var hourlySlots = new List<object>();

            foreach (int hour in businessHours)
            {
                DateTime exactSlotTime = parsedDate.Date.AddHours(hour);

                bool isAlreadyBooked = bookingsForDay.Any(b =>
                    b.Time_Slot == exactSlotTime &&
                    b.Appointment != null &&
                    b.Appointment.Down_Payment_Paid);

                hourlySlots.Add(new
                {
                    SlotHour = hour,
                    FormattedTime = GetFormattedTime(hour),
                    IsAvailable = !isAlreadyBooked && exactSlotTime >= nowPH
                });
            }

            return Ok(hourlySlots);
        }


        [HttpPost("booking")]
        public async Task<IActionResult> BookSlot([FromBody] BookingWithAppointmentRequest request)
        {
            request.Appointment_Date = NormalizeToPH(request.Appointment_Date);
            request.Time_Slot = NormalizeToPH(request.Time_Slot);

            // --- Validation Guards ---
            if (request.Time_Slot < NowInPH())
                return BadRequest("Booking failed: You cannot select a time slot in the past!");

            if (request.Appointment_Date.Date == TodayInPH())
                return BadRequest("Booking failed: You cannot book a time slot for the current day!");

            if (request.Appointment_Date.Date == TodayInPH().AddDays(1))
                return BadRequest("Booking failed: You cannot book a time slot for tomorrow!");

            if (string.IsNullOrWhiteSpace(request.Full_Name))
                return BadRequest("Booking failed: Full Name is required!");

            DayOfWeek dayOfWeek = request.Appointment_Date.DayOfWeek;
            if (dayOfWeek == DayOfWeek.Tuesday)
                return BadRequest("Booking failed: The shop is closed on Tuesdays!");

            int[] validHours = GetBusinessHoursForDay(dayOfWeek);
            if (!validHours.Contains(request.Time_Slot.Hour))
                return BadRequest("Booking failed: The submitted time slot is not a valid business hour for this day!");

            bool isExactSlotTaken = await _context.Schedulers
                .Include(s => s.Appointment)
                .AnyAsync(s =>
                    s.Appointment_Date.Date == request.Appointment_Date.Date &&
                    s.Time_Slot == request.Time_Slot &&
                    s.Appointment != null &&
                    s.Appointment.Down_Payment_Paid);

            if (isExactSlotTaken)
                return BadRequest("Booking failed: This specific time slot has already been reserved and paid for!");

            int maxSlotsForThisDay = GetMaxSlotsForDay(dayOfWeek);
            int totalBookingsForDay = await _context.Schedulers
                .Include(s => s.Appointment)
                .CountAsync(s =>
                    s.Appointment_Date.Date == request.Appointment_Date.Date &&
                    s.Appointment != null &&
                    s.Appointment.Down_Payment_Paid);

            if (totalBookingsForDay >= maxSlotsForThisDay)
                return BadRequest("Booking failed: This date has reached full operational capacity!");
            // --- End of Validation Guards ---


            // Create a PENDING appointment placeholder.
            var pendingAppointment = new Appointment
            {
                Full_Name = request.Full_Name,
                Social_Account = request.Social_Account,
                Phone = request.Phone,
                Additional_Notes = request.Additional_Notes,
                Appointment_Type = request.Appointment_Type,
                Inspiration_Image = request.Inspiration_Image ?? string.Empty,
                Down_Payment_Paid = false,
                Status = 0
            };

            _context.Appointments.Add(pendingAppointment);
            await _context.SaveChangesAsync(); // generates AppId

            var scheduler = new Scheduler
            {
                AppId = pendingAppointment.AppId,
                Appointment_Date = request.Appointment_Date,
                Time_Slot = request.Time_Slot
            };

            _context.Schedulers.Add(scheduler);
            await _context.SaveChangesAsync(); // generates SchedulerID

            string domain = $"{Request.Scheme}://{Request.Host}";
            string successUrl = $"{domain}/api/Calendar/payment-success?schedulerId={scheduler.SchedulerID}";
            string cancelUrl = $"{domain}/api/Calendar/payment-cancelled?schedulerId={scheduler.SchedulerID}";

            try
            {
                string checkoutUrl = await _payMongoService.CreateCheckoutSessionAsync(
                    amountInPhp: 150.00m,
                    description: $"₱150 Reservation Deposit for {pendingAppointment.Full_Name}",
                    successUrl: successUrl,
                    cancelUrl: cancelUrl
                );

                return Ok(new { CheckoutUrl = checkoutUrl, Message = "Reservation down-payment initiated." });
            }
            catch (Exception ex)
            {
                _context.Schedulers.Remove(scheduler);
                _context.Appointments.Remove(pendingAppointment);
                await _context.SaveChangesAsync();
                return StatusCode(500, $"Payment gateway communication failed: {ex.Message}");
            }
        }


        // Payment confirmed by PayMongo : mark the appointment as paid and show success card
        [HttpGet("payment-success")]
        public async Task<IActionResult> HandlePaymentSuccess([FromQuery] int schedulerId)
        {
            var schedulerRecord = await _context.Schedulers
                .Include(s => s.Appointment)
                .FirstOrDefaultAsync(s => s.SchedulerID == schedulerId);

            if (schedulerRecord == null)
                return NotFound("Reservation reference records are missing.");

            if (schedulerRecord.Appointment != null)
            {
                schedulerRecord.Appointment.Down_Payment_Paid = true;
                await _context.SaveChangesAsync();
            }

            // Returns the styled layout, then routes back to the home index route
            string htmlContent = @"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset=""utf-8"">
                <title>Payment Successful</title>
                <style>
                    body {
                        background-color: #fff5f6;
                        font-family: 'Segoe UI', Roboto, Arial, sans-serif;
                        display: flex;
                        justify-content: center;
                        align-items: center;
                        height: 100vh;
                        margin: 0;
                    }
                    .card {
                        background-color: #fdeef0;
                        padding: 40px;
                        border-radius: 16px;
                        box-shadow: 0 10px 30px rgba(0,0,0,0.05);
                        border: 1px solid #fde0e2;
                        text-align: center;
                        max-width: 450px;
                        width: 90%;
                        box-sizing: border-box;
                    }
                    .emoji {
                        font-size: 48px; 
                        margin-bottom: 16px;
                    }
                    h3 {
                        color: #8b4b3b;
                        font-size: 24px;
                        margin-bottom: 12px;
                        margin-top: 0;
                    }
                    p {
                        color: #8a6a62;
                        font-size: 15px;
                        line-height: 1.6;
                        margin-bottom: 20px;
                    }
                    .redirect-text {
                        font-size: 13px; 
                        color: #a08077; 
                        font-style: italic; 
                        margin-bottom: 28px;
                    }
                    .btn {
                        background-color: #88b04b;
                        color: #ffffff;
                        padding: 12px 32px;
                        border-radius: 24px;
                        text-decoration: none;
                        font-weight: 600;
                        display: inline-block;
                        box-shadow: 0 6px 12px rgba(136,176,75,0.18);
                        transition: background 0.2s ease;
                    }
                    .btn:hover {
                        background-color: #76993f;
                    }
                </style>
                <script>
                    setTimeout(function() {
                        window.location.href = '/Home/Index';
                    }, 5000);
                </script>
            </head>
            <body>
                <div class=""card"">
                    <div class=""emoji"">✨</div>
                    <h3>Payment Received!</h3>
                    <p>Your ₱150 reservation downpayment via GCash has been processed successfully. Your appointment slot is officially secured!</p>
                    <p class=""redirect-text"">Redirecting you back home automatically in 5 seconds...</p>
                    <a href=""/Home/Index"" class=""btn"">Return Home Immediately</a>
                </div>
            </body>
            </html>";

            return Content(htmlContent, "text/html; charset=utf-8");
        }


        // 🌟 MODIFIED: FORCE CLEAN DEPENDENCY DROPS FOR FAILED/ABORTED PAYMENTS
        [HttpGet("payment-cancelled")]
        public async Task<IActionResult> HandlePaymentCancelled([FromQuery] int schedulerId)
        {
            var schedulerRecord = await _context.Schedulers
                .Include(s => s.Appointment)
                .FirstOrDefaultAsync(s => s.SchedulerID == schedulerId);

            if (schedulerRecord != null)
            {
                var pendingAppointment = schedulerRecord.Appointment;

                // 1. Remove the child scheduler entity first to satisfy relational layouts
                _context.Schedulers.Remove(schedulerRecord);

                // 2. Remove the parent appointment entity only if it was never paid for
                if (pendingAppointment != null && !pendingAppointment.Down_Payment_Paid)
                {
                    _context.Appointments.Remove(pendingAppointment);
                }

                try
                {
                    // 3. Force commit changes to your SQL Express instance cleanly
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Cascading transaction clean-up fallback notice: {ex.Message}");
                }
            }

            return Content("<h3>Payment was cancelled. Your reservation attempt was not processed.</h3>", "text/html");
        }


        private async Task<List<CalendarDayStatus>> GetDaysStatusForMonth(int year, int month)
        {
            var monthlyBookings = await _context.Schedulers
                .Include(s => s.Appointment)
                .Where(s => s.Appointment_Date.Year == year && s.Appointment_Date.Month == month)
                .ToListAsync();

            DateTime todayPH = TodayInPH();
            int daysInMonth = DateTime.DaysInMonth(year, month);
            var daysList = new List<CalendarDayStatus>(daysInMonth);

            for (int day = 1; day <= daysInMonth; day++)
            {
                var loopDate = new DateTime(year, month, day);
                int maxSlotsForThisDay = GetMaxSlotsForDay(loopDate.DayOfWeek);

                int totalBookingsForDay = monthlyBookings.Count(b =>
                    b.Appointment_Date.Date == loopDate.Date &&
                    b.Appointment != null &&
                    b.Appointment.Down_Payment_Paid);

                bool isAvailable;
                if (loopDate.Date < todayPH) isAvailable = false;
                else if (loopDate.Date == todayPH) isAvailable = false;
                else if (loopDate.Date == todayPH.AddDays(1)) isAvailable = false;
                else if (loopDate.DayOfWeek == DayOfWeek.Tuesday) isAvailable = false;
                else if (totalBookingsForDay >= maxSlotsForThisDay) isAvailable = false;
                else isAvailable = true;

                daysList.Add(new CalendarDayStatus
                {
                    DateString = loopDate.ToString("yyyy-MM-dd"),
                    IsAvailable = isAvailable
                });
            }

            return daysList;
        }

        [HttpGet("month-status")]
        public async Task<IActionResult> GetMonthStatus([FromQuery] int year, [FromQuery] int month)
        {
            if (year <= 0 || month < 1 || month > 12)
                return BadRequest("Invalid year or month");

            var days = await GetDaysStatusForMonth(year, month);
            return Ok(days);
        }
    }
}