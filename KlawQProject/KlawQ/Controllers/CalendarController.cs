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
    /// <summary>
    /// Controller for managing calendar-related operations.
    /// Covers Inheritance: Inherits from the ControllerBase class.
    /// Covers Abstraction: Interacts with PayMongo API services and Entity Framework Core db context to coordinate slot scheduling and payments.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CalendarController(ApplicationDbContext context, PayMongoService payMongoService) : ControllerBase
    {
        // Dependencies for database access and payment processing
        private readonly ApplicationDbContext _context = context;
        // Service for handling PayMongo payment processing related to bookings
        private readonly PayMongoService _payMongoService = payMongoService;
        // Time zone information for Philippine Time, used for all date and time operations to ensure consistency across the application
        private static readonly TimeZoneInfo PhilippineTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");

        private const int ALMOST_FULL_THRESHOLD = 5;

        // Determines the maximum number of booking slots available for a given day of the week.
        private static int GetMaxSlotsForDay(DayOfWeek day) => day switch
        {
            DayOfWeek.Tuesday => 0,
            DayOfWeek.Wednesday => 3,
            DayOfWeek.Saturday => 1,
            _ => 4
        };

        // Returns the business hours available for booking on a specific day of the week.
        private static int[] GetBusinessHoursForDay(DayOfWeek day) => day switch
        {
            DayOfWeek.Tuesday => [],
            DayOfWeek.Wednesday => [ 10, 14, 22 ],
            DayOfWeek.Saturday => [ 18 ],
            _ => [ 10, 14, 18, 21 ]
        };

        // Formats an hour into a human-readable time string (e.g., "10:00 AM").
        private static string GetFormattedTime(int hour) =>
            new DateTime(2000, 1, 1, hour, 0, 0)
                .ToString("h:mm tt", CultureInfo.InvariantCulture);

        // Returns the current date and time in Philippine Time.
        private static DateTime NowInPH() =>
            TimeZoneInfo.ConvertTime(DateTime.UtcNow, PhilippineTimeZone);

        // Returns the current date (without time) in Philippine Time.
        private static DateTime TodayInPH() => NowInPH().Date;

        // Normalizes a given DateTime to Philippine Time, ensuring consistent date comparisons and storage.
        private static DateTime NormalizeToPH(DateTime dt)
        {
            if (dt.Kind == DateTimeKind.Utc)
                return TimeZoneInfo.ConvertTimeFromUtc(dt, PhilippineTimeZone);
            return DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
        }

        // Provides the current availability status for each day in the calendar view, factoring in both existing bookings and admin-configured blocks.
        // Covers Abstraction: Employs nested helper calls to simplify look-ahead calculations for availability logic.
        // Covers Polymorphism: Returns IActionResult, allowing dynamic API response variants.
        [HttpGet("current-view-status")]
        public async Task<IActionResult> GetCurrentCalendarView()
        {
            DateTime todayPH = TodayInPH(); // Base look-ahead logic completely on PH localized time coordinates

            // Always include the current month in the response to ensure the calendar view can render at least one month of data, even if it is fully booked or blocked
            var currentMonthDays = await GetDaysStatusForMonth(todayPH.Year, todayPH.Month);
            var finalResponse = new List<CalendarDayStatus>(currentMonthDays);

            // Proactively include the next month in the response if the current month is almost fully booked, to allow users to see and plan for upcoming availability without needing to click "next month" in the UI
            DateTime nextMonthDate = todayPH.AddMonths(1);
            var nextMonthDays = await GetDaysStatusForMonth(nextMonthDate.Year, nextMonthDate.Month);
            finalResponse.AddRange(nextMonthDays);

            /*DateTime nextnextMonthDate = todayPH.AddMonths(2);
            var nextnextMonthDays = await GetDaysStatusForMonth(nextnextMonthDate.Year, nextnextMonthDate.Month);
            finalResponse.AddRange(nextnextMonthDays);*/ // For testing only: force show 2nd month in the calendar view regardless of availability status

            // Evaluates IsAvailable (which already factors in Admin Blocks) using the exact same PH Date boundary line
            int availableDaysLeft = currentMonthDays
                .Count(d => d.IsAvailable && DateTime.ParseExact(d.DateString, "yyyy-MM-dd", CultureInfo.InvariantCulture) >= todayPH);

            // If the current month is almost fully booked, also include the month after next in the response to give users more visibility into future availability without needing to click "next month" multiple times in the UI
            if (availableDaysLeft <= ALMOST_FULL_THRESHOLD)
            {
                DateTime nextNextMonthDate = todayPH.AddMonths(2);
                var nextNextMonthDays = await GetDaysStatusForMonth(nextNextMonthDate.Year, nextNextMonthDate.Month);
                finalResponse.AddRange(nextNextMonthDays);
            }

            return Ok(finalResponse);
        }

        // Checks for both existing paid bookings and admin configuration blocks.
        // Covers Abstraction: Conceals database querying, business hour structures, and time zone calculations behind a single clean endpoint.
        [HttpGet("day-slots-status")]
        public async Task<IActionResult> GetDaySlotsStatus([FromQuery] string chosenDate)
        {
            // Validate the date format and ensure it is in the correct "yyyy-MM-dd" format
            if (!DateTime.TryParseExact(chosenDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime parsedDate))
            {
                return BadRequest("Invalid date format. Please use yyyy-MM-dd.");
            }

            // Enforce the same PH date boundary logic for slot availability as used in the month view, to prevent any discrepancies between what users see in the calendar and what they can actually book when they select a specific date
            if (parsedDate.Date <= TodayInPH())
                return Ok(new List<object>());

            // Prevent any bookings for the next day as well, to maintain a consistent buffer period and avoid last-minute bookings that could disrupt scheduling and operations
            if (parsedDate.Date == TodayInPH().AddDays(1))
                return Ok(new List<object>());

            // Prevent any bookings on Tuesdays, as the shop is closed on that day
            if (parsedDate.DayOfWeek == DayOfWeek.Tuesday)
                return Ok(new List<object>());

            // Fetch all existing bookings for the specified date, including their associated appointment details
            var bookingsForDay = await _context.Schedulers
                .Include(s => s.Appointment)
                .Where(s => s.Appointment_Date.Date == parsedDate.Date)
                .ToListAsync();

            // Fetch admin configuration constraints for this exact date layout
            var adminHourlyBlocks = await _context.CalendarConfigures
                .Where(o => o.TargetDate.Date == parsedDate.Date)
                .ToListAsync();

            // Determine the valid business hours for the specified day of the week, based on the shop's operational schedule
            int[] businessHours = GetBusinessHoursForDay(parsedDate.DayOfWeek);
            DateTime nowPH = NowInPH();
            var hourlySlots = new List<object>();

            // Iterate through each valid business hour and determine its availability status based on existing bookings, admin blocks, and whether the slot is in the past
            foreach (int hour in businessHours)
            {
                // Construct the exact DateTime for the current hour slot on the specified date
                DateTime exactSlotTime = parsedDate.Date.AddHours(hour);

                // Check if this specific hour slot has already been booked and paid for, considering only appointments that are either pending or confirmed
                bool isAlreadyBooked = bookingsForDay.Any(b =>
                    b.Time_Slot == exactSlotTime &&
                    b.Appointment?.Down_Payment_Paid is true &&
                    (b.Appointment?.Status == 0 || b.Appointment?.Status == 1));

                // Check if admin blocked either the entire day (null) or this specific business hour slot
                bool isSlotBlockedByAdmin = adminHourlyBlocks.Any(o => o.BlockedHour == null || o.BlockedHour == hour);

                // Determine the availability of the slot based on whether it is already booked, blocked by admin, or in the past relative to the current Philippine time
                hourlySlots.Add(new
                {
                    SlotHour = hour,
                    FormattedTime = GetFormattedTime(hour),
                    // Drop option if it is booked, explicitly blocked by an admin setting, or in the past
                    IsAvailable = !isAlreadyBooked && !isSlotBlockedByAdmin && exactSlotTime >= nowPH
                });
            }

            return Ok(hourlySlots);
        }

        // Endpoint to handle booking requests, with comprehensive validation guards and integration with PayMongo for payment processing.
        // Covers Encapsulation: Validates booking constraints, slot capacity, closed days, and past slots before mutating the database or launching payment requests.
        // Covers Abstraction: Leverages PayMongoService to abstract payment session generation.
        [HttpPost("booking")]
        public async Task<IActionResult> BookSlot([FromBody] BookingRequest request)
        {
            // Validate the incoming booking request to ensure it contains a valid appointment reference (AppId) and that the requested date and time slot are in the future and not on restricted days
            if (request == null || request.AppId <= 0)
                return BadRequest("Booking failed: Invalid appointment reference data!");

            // Normalize the incoming date and time slot to Philippine Time to ensure consistent comparisons and storage
            request.Appointment_Date = NormalizeToPH(request.Appointment_Date);
            request.Time_Slot = NormalizeToPH(request.Time_Slot);

            // --- Validation Guards ---
            if (request.Time_Slot < NowInPH())
                return BadRequest("Booking failed: You cannot select a time slot in the past!");

            if (request.Appointment_Date.Date == TodayInPH())
                return BadRequest("Booking failed: You cannot book a time slot for the current day!");

            if (request.Appointment_Date.Date == TodayInPH().AddDays(1))
                return BadRequest("Booking failed: You cannot book a time slot for tomorrow!");

            var pendingAppointment = await _context.Appointments.FindAsync(request.AppId);
            if (pendingAppointment == null)
                return BadRequest("Booking failed: Appointment reference not found!");

            if (string.IsNullOrWhiteSpace(pendingAppointment.Full_Name))
                return BadRequest("Booking failed: Full Name is required on the appointment!");

            DayOfWeek dayOfWeek = request.Appointment_Date.DayOfWeek;
            if (dayOfWeek == DayOfWeek.Tuesday)
                return BadRequest("Booking failed: The shop is closed on Tuesdays!");

            int[] validHours = GetBusinessHoursForDay(dayOfWeek);
            if (!validHours.Contains(request.Time_Slot.Hour))
                return BadRequest("Booking failed: The submitted time slot is not a valid business hour for this day!");

            // Check if the exact time slot has already been booked and paid for, considering only appointments that are either pending or confirmed
            bool isExactSlotTaken = await _context.Schedulers
                .Include(s => s.Appointment)
                .AnyAsync(s =>
                    s.Appointment_Date.Date == request.Appointment_Date.Date &&
                    s.Time_Slot == request.Time_Slot &&
                    s.Appointment != null &&
                    s.Appointment.Down_Payment_Paid &&
                    (s.Appointment.Status == 0 || s.Appointment.Status == 1));

            // If the exact time slot is already taken, return a bad request response indicating that the booking cannot proceed
            if (isExactSlotTaken)
                return BadRequest("Booking failed: This specific time slot has already been reserved and paid for!");

            // Check if the total number of bookings for the day has reached the maximum operational capacity, based on the shop's schedule and any admin-configured blocks
            int maxSlotsForThisDay = GetMaxSlotsForDay(dayOfWeek);
            int totalBookingsForDay = await _context.Schedulers
                .Include(s => s.Appointment)
                .CountAsync(s =>
                    s.Appointment_Date.Date == request.Appointment_Date.Date &&
                    s.Appointment != null &&
                    s.Appointment.Down_Payment_Paid &&
                    (s.Appointment.Status == 0 || s.Appointment.Status == 1));

            // If the total bookings for the day have reached or exceeded the maximum slots, return a bad request response indicating that the booking cannot proceed
            if (totalBookingsForDay >= maxSlotsForThisDay)
                return BadRequest("Booking failed: This date has reached full operational capacity!");
            // --- End of Validation Guards ---

            // Create a new Scheduler record to track the booking attempt, associating it with the pending appointment and the requested date and time slot
            var scheduler = new Scheduler
            {
                AppId = pendingAppointment.AppId,
                Appointment_Date = request.Appointment_Date,
                Time_Slot = request.Time_Slot
            };

            // Save the scheduler record to the database to generate a unique SchedulerID, which will be used for tracking the payment process
            _context.Schedulers.Add(scheduler);
            await _context.SaveChangesAsync(); // generates SchedulerID

            // Construct the success and cancel URLs for PayMongo to redirect to after payment processing, including the SchedulerID as a query parameter for tracking
            string domain = $"{Request.Scheme}://{Request.Host}";
            string successUrl = $"{domain}/api/Calendar/payment-success?schedulerId={scheduler.SchedulerID}";
            string cancelUrl = $"{domain}/api/Calendar/payment-cancelled?schedulerId={scheduler.SchedulerID}";

            try
            {
                //
                string checkoutUrl = await _payMongoService.CreateCheckoutSessionAsync(
                    amountInPhp: 150.00m,
                    description: $"₱150 Reservation Deposit for {pendingAppointment.Full_Name}",
                    successUrl: successUrl,
                    cancelUrl: cancelUrl
                );

                // Return the checkout URL to the client so they can proceed with the payment process
                return Ok(new { CheckoutUrl = checkoutUrl, Message = "Reservation down-payment initiated." });
            }

            // If there is an exception during the communication with PayMongo, perform a clean-up by removing the scheduler record and the pending appointment (if it hasn't been paid for) to prevent orphaned records and ensure data integrity. Then return a 500 status code with an error message indicating that the payment gateway communication failed.
            catch (Exception ex)
            {
                _context.Schedulers.Remove(scheduler);
                _context.Appointments.Remove(pendingAppointment);
                await _context.SaveChangesAsync();
                return StatusCode(500, $"Payment gateway communication failed: {ex.Message}");
            }
        }

        // Payment confirmed by PayMongo : mark the appointment as paid and show success card.
        // Covers Encapsulation: Updates the appointment paid status property internally upon verification.
        // Covers Abstraction: Hides GCash integration session handling from the customer.
        [HttpGet("payment-success")]
        public async Task<IActionResult> HandlePaymentSuccess([FromQuery] int schedulerId)
        {
            // Retrieve the scheduler record associated with the provided SchedulerID, including its related appointment details
            var schedulerRecord = await _context.Schedulers
                .Include(s => s.Appointment)
                .FirstOrDefaultAsync(s => s.SchedulerID == schedulerId);

            // If the scheduler record is not found, return a 404 Not Found response indicating that the reservation reference records are missing
            if (schedulerRecord == null)
                return NotFound("Reservation reference records are missing.");

            // If the scheduler record is found and it has an associated appointment, mark the appointment as paid by setting the Down_Payment_Paid property to true and save the changes to the database
            if (schedulerRecord.Appointment != null)
            {
                schedulerRecord.Appointment.Down_Payment_Paid = true;
                await _context.SaveChangesAsync();
            }

            const string htmlContent = @"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset=""utf-8"">
                <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                <title>Payment Successful</title>
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
                    }, 5000);
                </script>
            </head>
            <body>
                <div class=""card"">
                    <i class=""fas fa-check-circle success-icon""></i>
                    <h3>Payment Received!</h3>
                    <p>Your ₱150 reservation downpayment via GCash has been processed successfully. Your appointment slot is officially secured!</p>
                    <p class=""redirect-text"">Redirecting you back home automatically in 5 seconds...</p>
                    <a href=""/Home/Index"" class=""btn"">Return Home Immediately</a>
                </div>
            </body>
            </html>";

            return Content(htmlContent, "text/html; charset=utf-8");
        }

        // Payment cancelled by PayMongo : clean up scheduler and appointment records if necessary.
        // Covers Encapsulation: Protects data integrity by checking state conditions (Down_Payment_Paid status) before removing entities.
        [HttpGet("payment-cancelled")]
        public async Task<IActionResult> HandlePaymentCancelled([FromQuery] int schedulerId)
        {
            // Retrieve the scheduler record associated with the provided SchedulerID, including its related appointment details
            var schedulerRecord = await _context.Schedulers
                .Include(s => s.Appointment)
                .FirstOrDefaultAsync(s => s.SchedulerID == schedulerId);

            // If the scheduler record is found, remove it from the database. Additionally, if there is an associated appointment that has not been paid for, also remove that appointment to prevent orphaned records and ensure data integrity. Save the changes to the database after performing the clean-up operations.
            if (schedulerRecord != null)
            {
                var pendingAppointment = schedulerRecord.Appointment;

                _context.Schedulers.Remove(schedulerRecord);

                // If the associated appointment exists and has not been paid for, remove it from the database to prevent orphaned records and ensure data integrity.
                if (pendingAppointment?.Down_Payment_Paid is false)
                {
                    _context.Appointments.Remove(pendingAppointment);
                }

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Cascading transaction clean-up fallback notice: {ex.Message}");
                }
            }

            // Return a simple HTML response indicating that the payment was cancelled and the reservation attempt was not processed. This provides clear feedback to the user about the status of their booking attempt.
            return Content("<h3>Payment was cancelled. Your reservation attempt was not processed.</h3>", "text/html");
        }

        // Syncs month grid views with active table override rules.
        // Covers Abstraction: Encapsulates query logic, daily booking tallies, and admin blocks into list collections, presenting a simplified status list.
        private async Task<List<CalendarDayStatus>> GetDaysStatusForMonth(int year, int month)
        {
            // Fetch all existing bookings for the specified month, including their associated appointment details, to determine how many slots have been booked for each day
            var monthlyBookings = await _context.Schedulers
                .Include(s => s.Appointment)
                .Where(s => s.Appointment_Date.Year == year && s.Appointment_Date.Month == month)
                .ToListAsync();

            // Fetch admin configuration rules targeting this month context layout
            var adminBlocks = await _context.CalendarConfigures
                .Where(o => o.TargetDate.Year == year && o.TargetDate.Month == month)
                .ToListAsync();

            // Get the current date in Philippine Time to ensure that availability status is determined based on the correct local date, preventing any discrepancies for users accessing the system from different time zones
            DateTime todayPH = TodayInPH();
            int daysInMonth = DateTime.DaysInMonth(year, month);
            var daysList = new List<CalendarDayStatus>(daysInMonth);

            // Iterate through each day of the specified month and determine its availability status based on existing bookings, admin blocks, and whether the day is in the past relative to the current Philippine date
            for (int day = 1; day <= daysInMonth; day++)
            {
                // Construct the exact DateTime for the current day in the loop, using the provided year and month parameters. This will be used for accurate comparisons against booking records and admin blocks, ensuring that the availability status is determined based on the correct local date.
                var loopDate = new DateTime(year, month, day);
                int maxSlotsForThisDay = GetMaxSlotsForDay(loopDate.DayOfWeek);

                // Count the total number of bookings for this specific day that have been paid for, considering only appointments that are either pending or confirmed. This will help determine if the day has reached its maximum booking capacity.
                int totalBookingsForDay = monthlyBookings.Count(b =>
                    b.Appointment_Date.Date == loopDate.Date &&
                    b.Appointment?.Down_Payment_Paid is true &&
                    (b.Appointment?.Status == 0 || b.Appointment?.Status == 1));

                // Identify if an admin profile explicitly blocked out this entire date row (BlockedHour is null)
                bool isDayBlockedByAdmin = adminBlocks.Any(o => o.TargetDate.Date == loopDate.Date && o.BlockedHour == null);

                // Determine the availability of the day based on whether it is in the past, if it is a Tuesday (shop closed), if it has been blocked by an admin rule, or if it has reached its maximum booking capacity. This logic ensures that the calendar view accurately reflects the true availability of each day for users when they are planning their appointments.
                bool isAvailable;
                if (loopDate.Date < todayPH) isAvailable = false;
                else if (loopDate.Date == todayPH) isAvailable = false;
                else if (loopDate.Date == todayPH.AddDays(1)) isAvailable = false;
                else if (loopDate.DayOfWeek == DayOfWeek.Tuesday) isAvailable = false;
                else if (isDayBlockedByAdmin) isAvailable = false; // Successfully sets availability false when blocked by an admin rule
                else if (totalBookingsForDay >= maxSlotsForThisDay) isAvailable = false;
                else isAvailable = true;

                // Add the day's availability status to the list that will be returned to the client, ensuring that the date is formatted as "yyyy-MM-dd" for consistency and ease of parsing on the client side.
                daysList.Add(new CalendarDayStatus
                {
                    DateString = loopDate.ToString("yyyy-MM-dd"),
                    IsAvailable = isAvailable
                });
            }

            return daysList;
        }

        // Endpoint to retrieve the availability status for each day in a specific month, allowing the calendar view to display accurate availability information based on existing bookings, admin blocks, and the current date in Philippine Time.
        // Covers Polymorphism: Evaluates input parameters dynamically and returns BadRequest or Ok responses polymorphically.
        [HttpGet("month-status")]
        public async Task<IActionResult> GetMonthStatus([FromQuery] int year, [FromQuery] int month)
        {
            // Validate the year and month parameters to ensure they represent a valid date. This prevents any potential errors or exceptions that could arise from invalid date inputs, ensuring that the endpoint can reliably return availability information for valid month and year combinations.
            if (year <= 0 || month < 1 || month > 12)
                return BadRequest("Invalid year or month");

            // Retrieve the availability status for each day in the specified month by calling the GetDaysStatusForMonth method, which factors in existing bookings, admin blocks, and the current date in Philippine Time to determine the true availability of each day. This allows the calendar view on the client side to accurately reflect which days are available for booking appointments.
            var days = await GetDaysStatusForMonth(year, month);
            return Ok(days);
        }
    }
}