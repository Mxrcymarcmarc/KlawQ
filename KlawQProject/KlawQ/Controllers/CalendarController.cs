using KlawQ.Data;
using KlawQ.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace KlawQ.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class CalendarController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        // Philippine timezone is used consistently across all time comparisons
        private static readonly TimeZoneInfo PhilippineTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");

        // Threshold to determine when to show next month's availability in the calendar view
        private const int ALMOST_FULL_THRESHOLD = 5;

        public CalendarController(ApplicationDbContext context)
        {
            _context = context;
        }

        // To determine max slots for a given day of the week
        private static int GetMaxSlotsForDay(DayOfWeek day) => day switch
        {
            DayOfWeek.Tuesday => 0, // Closed
            DayOfWeek.Wednesday => 3, // 10 AM, 2 PM, 10 PM
            DayOfWeek.Saturday => 1, // 6 PM only
            _ => 4  // Mon, Thu, Fri, Sun: 10 AM, 2 PM, 6 PM, 9 PM
        };

        // To determine business hours for a given day of the week
        private static int[] GetBusinessHoursForDay(DayOfWeek day) => day switch
        {
            DayOfWeek.Tuesday => Array.Empty<int>(),
            DayOfWeek.Wednesday => new[] { 10, 14, 22 }, // 10 AM, 2 PM, 10 PM
            DayOfWeek.Saturday => new[] { 18 },          // 6 PM only
            _ => new[] { 10, 14, 18, 21 } // 10 AM, 2 PM, 6 PM, 9 PM
        };

        // To format hour integers into user-friendly time strings (e.g. 14 -> "2:00 PM")
        private static string GetFormattedTime(int hour) =>
            new DateTime(2000, 1, 1, hour, 0, 0)
                .ToString("h:mm tt", CultureInfo.InvariantCulture); // Example: "2:00 PM"

        // To get current time in Philippine timezone
        private static DateTime NowInPH() =>
            TimeZoneInfo.ConvertTime(DateTime.UtcNow, PhilippineTimeZone);

        // To get today's date in Philippine timezone (time component set to 00:00:00)
        private static DateTime TodayInPH() => NowInPH().Date;


        // Endpoint to get availability status for each day of the current month (and next month if almost full)
        [HttpGet("current-view-status")]
        public async Task<IActionResult> GetCurrentCalendarView()
        {
            DateTime today = TodayInPH();
            int currentYear = today.Year;
            int currentMonth = today.Month;

            // Fetch availability for the current month
            var currentMonthDays = await GetDaysStatusForMonth(currentYear, currentMonth);

            // Count remaining available days from today onwards
            int availableDaysLeft = currentMonthDays
                .Count(d => d.IsAvailable && DateTime.Parse(d.DateString) >= today);

            var finalResponse = new List<CalendarDayStatus>(currentMonthDays);

            // If almost full, also attach the next month
            if (availableDaysLeft <= ALMOST_FULL_THRESHOLD)
            {
                DateTime nextMonthDate = today.AddMonths(1);
                var nextMonthDays = await GetDaysStatusForMonth(nextMonthDate.Year, nextMonthDate.Month);
                finalResponse.AddRange(nextMonthDays);
            }

            return Ok(finalResponse);
        }


        // Endpoint to get hourly slot availability for a specific day
        [HttpGet("day-slots-status")]
        public async Task<IActionResult> GetDaySlotsStatus([FromQuery] string chosenDate)
        {

            // Validate date format first (expects "yyyy-MM-dd")
            if (!DateTime.TryParseExact(
                    chosenDate,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime parsedDate))
            {
                return BadRequest("Invalid date format. Please use yyyy-MM-dd (e.g. 2025-06-15).");
            }

            // If the chosen date is in the past or today (compared to Philippine time), return empty array
            if (parsedDate.Date <= TodayInPH())
            {
                return Ok(new List<object>()); // Return empty array []
            }

            // Fetch all existing bookings for this specific day in one DB trip
            var bookingsForDay = await _context.Schedulers
                .Where(s => s.Appointment_Date.Date == parsedDate.Date)
                .ToListAsync();

            // Get the business hours for this day of the week
            int[] businessHours = GetBusinessHoursForDay(parsedDate.DayOfWeek);

            // Use Philippine timezone for "is this slot in the past?" check
            DateTime nowPH = NowInPH();

            // Build the response array with availability status for each hourly slot
            var hourlySlots = new List<object>();

            // For each business hour, determine if it's already booked or in the past
            foreach (int hour in businessHours)
            {
                DateTime exactSlotTime = parsedDate.Date.AddHours(hour);

                bool isAlreadyBooked = bookingsForDay.Any(b => b.Time_Slot == exactSlotTime);

                // Compare against Philippine time, not server's local time
                bool isPastTime = exactSlotTime < nowPH;

                hourlySlots.Add(new
                {
                    SlotHour = hour,
                    FormattedTime = GetFormattedTime(hour),      // Example: "2:00 PM"
                    IsAvailable = !isAlreadyBooked && !isPastTime
                });
            }

            return Ok(hourlySlots);
        }


        // Endpoint to attempt booking a specific time slot on a specific day
        [HttpPost("booking")]
        public async Task<IActionResult> BookSlot([FromBody] Scheduler newBooking)
        {
            // Guard against past bookings using Philippine time
            if (newBooking.Time_Slot < NowInPH())
            {
                return BadRequest("Booking failed: You cannot select a time slot in the past!");
            }

            // Guard against same-day bookings using Philippine time
            if (newBooking.Appointment_Date.Date == TodayInPH())
            {
                return BadRequest("Booking failed: You cannot book a time slot for the current day!");
            }

            DayOfWeek dayOfWeek = newBooking.Appointment_Date.DayOfWeek;

            // Appointment is closed on Tuesdays
            if (dayOfWeek == DayOfWeek.Tuesday)
            {
                return BadRequest("Booking failed: The shop is closed on Tuesdays!");
            }

            // Check if the submitted time slot is a valid business hour
            int[] validHours = GetBusinessHoursForDay(newBooking.Appointment_Date.DayOfWeek);
            int submittedHour = newBooking.Time_Slot.Hour;

            if (!validHours.Contains(submittedHour))
            {
                return BadRequest("Booking failed: The submitted time slot is not a valid business hour for this day!");
            }

            // Uses shared helper : no duplicated switch block here
            int maxSlotsForThisDay = GetMaxSlotsForDay(dayOfWeek);

            // Exact time slot is already taken (race-condition safety)
            bool isExactSlotTaken = await _context.Schedulers.AnyAsync(s =>
                s.Appointment_Date.Date == newBooking.Appointment_Date.Date &&
                s.Time_Slot == newBooking.Time_Slot);

            if (isExactSlotTaken)
            {
                return BadRequest("Booking failed: This specific time slot has already been reserved!");
            }

            // Daily capacity limit reached
            int totalBookingsForDay = await _context.Schedulers
                .CountAsync(s => s.Appointment_Date.Date == newBooking.Appointment_Date.Date);

            if (totalBookingsForDay >= maxSlotsForThisDay)
            {
                return BadRequest("Booking failed: This date has reached full operational capacity!");
            }

            // All validation passed : save to SQL Server
            _context.Schedulers.Add(newBooking);
            await _context.SaveChangesAsync();

            return Ok("Appointment locked in and saved to database successfully!");
        }

        // To get availability status for each day of a given month and year
        private async Task<List<CalendarDayStatus>> GetDaysStatusForMonth(int year, int month)
        {
            // Pull all entries for this month in one DB trip (avoids N+1 queries)
            var monthlyBookings = await _context.Schedulers
                .Where(s => s.Appointment_Date.Year == year && s.Appointment_Date.Month == month)
                .ToListAsync();

            DateTime todayPH = TodayInPH();
            int daysInMonth = DateTime.DaysInMonth(year, month);
            var daysList = new List<CalendarDayStatus>(daysInMonth);

            for (int day = 1; day <= daysInMonth; day++)
            {
                var loopDate = new DateTime(year, month, day);

                // Uses shared helper : no duplicated switch block here
                int maxSlotsForThisDay = GetMaxSlotsForDay(loopDate.DayOfWeek);
                int totalBookingsForDay = monthlyBookings.Count(b => b.Appointment_Date.Date == loopDate.Date);

                bool isAvailable;

                if (loopDate.Date < todayPH)
                {
                    isAvailable = false; // Past dates are locked
                }
                else if (loopDate.Date == todayPH)
                {
                    isAvailable = false; // Same-day bookings are not allowed
                }
                else if (loopDate.DayOfWeek == DayOfWeek.Tuesday)
                {
                    isAvailable = false; // Appointment is closed on Tuesdays
                }
                else if (totalBookingsForDay >= maxSlotsForThisDay)
                {
                    isAvailable = false; // Day is fully booked
                }
                else
                {
                    isAvailable = true;
                }

                daysList.Add(new CalendarDayStatus
                {
                    DateString = loopDate.ToString("yyyy-MM-dd"),
                    IsAvailable = isAvailable
                });
            }

            return daysList;
        }
    }
}