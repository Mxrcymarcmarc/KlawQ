using KlawQ.Data;
using KlawQ.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace KlawQ.Controllers
{
    // The data container structure used to send daily availability arrays to the frontend layout
    public class CalendarDayStatus
    {
        public string DateString { get; set; } = string.Empty; // Format: "yyyy-MM-dd"
        public bool IsAvailable { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class CalendarController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        // FIX: Philippine timezone used consistently across all time comparisons
        private static readonly TimeZoneInfo PhilippineTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");

        // Business Rule: If 5 or fewer available days remain, show next month automatically
        private const int ALMOST_FULL_THRESHOLD = 5;

        public CalendarController(ApplicationDbContext context)
        {
            _context = context;
        }

        private static int GetMaxSlotsForDay(DayOfWeek day) => day switch
        {
            DayOfWeek.Tuesday => 0, // Closed
            DayOfWeek.Wednesday => 3, // 10 AM, 2 PM, 10 PM
            DayOfWeek.Saturday => 1, // 6 PM only
            _ => 4  // Mon, Thu, Fri, Sun: 10 AM, 2 PM, 6 PM, 9 PM
        };

        private static int[] GetBusinessHoursForDay(DayOfWeek day) => day switch
        {
            DayOfWeek.Tuesday => Array.Empty<int>(),
            DayOfWeek.Wednesday => new[] { 10, 14, 22 }, // 10 AM, 2 PM, 10 PM
            DayOfWeek.Saturday => new[] { 18 },          // 6 PM only
            _ => new[] { 10, 14, 18, 21 } // 10 AM, 2 PM, 6 PM, 9 PM
        };

        private static string GetFormattedTime(int hour) =>
            new DateTime(2000, 1, 1, hour, 0, 0)
                .ToString("h:mm tt", CultureInfo.InvariantCulture); // e.g., "2:00 PM"

        private static DateTime NowInPH() =>
            TimeZoneInfo.ConvertTime(DateTime.UtcNow, PhilippineTimeZone);

        private static DateTime TodayInPH() => NowInPH().Date;

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

            // Business Rule: If almost full, also attach the next month
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

            if (!DateTime.TryParseExact(
                    chosenDate,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime parsedDate))
            {
                return BadRequest("Invalid date format. Please use yyyy-MM-dd (e.g. 2025-06-15).");
            }

            // Fetch all existing bookings for this specific day in one DB trip
            var bookingsForDay = await _context.Schedulers
                .Where(s => s.Appointment_Date.Date == parsedDate.Date)
                .ToListAsync();

            int[] businessHours = GetBusinessHoursForDay(parsedDate.DayOfWeek);

            // Use Philippine timezone for "is this slot in the past?" check
            DateTime nowPH = NowInPH();

            var hourlySlots = new List<object>();

            foreach (int hour in businessHours)
            {
                DateTime exactSlotTime = parsedDate.Date.AddHours(hour);

                bool isAlreadyBooked = bookingsForDay.Any(b => b.Time_Slot == exactSlotTime);

                // Compare against Philippine time, not server's local time
                bool isPastTime = exactSlotTime < nowPH;

                hourlySlots.Add(new
                {
                    SlotHour = hour,
                    FormattedTime = GetFormattedTime(hour),      // e.g., "2:00 PM"
                    IsAvailable = !isAlreadyBooked && !isPastTime
                });
            }

            return Ok(hourlySlots);
        }


        [HttpPost("book")]
        public async Task<IActionResult> BookSlot([FromBody] Scheduler newBooking)
        {
            // FIX: Guard against past bookings using Philippine time
            if (newBooking.Time_Slot < NowInPH())
            {
                return BadRequest("Booking failed: You cannot select a time slot in the past!");
            }

            DayOfWeek dayOfWeek = newBooking.Appointment_Date.DayOfWeek;

            // Shop is closed on Tuesdays
            if (dayOfWeek == DayOfWeek.Tuesday)
            {
                return BadRequest("Booking failed: The shop is closed on Tuesdays!");
            }

            // Uses shared helper — no duplicated switch block here
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

            // All validation passed — save to SQL Server
            _context.Schedulers.Add(newBooking);
            await _context.SaveChangesAsync();

            return Ok("Appointment locked in and saved to database successfully!");
        }

        // ─────────────────────────────────────────────────────────────────────
        // REUSABLE HELPER: Calculates day-by-day availability for a full month
        // ─────────────────────────────────────────────────────────────────────

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

                // FIX: Uses shared helper — no duplicated switch block here
                int maxSlotsForThisDay = GetMaxSlotsForDay(loopDate.DayOfWeek);
                int totalBookingsForDay = monthlyBookings.Count(b => b.Appointment_Date.Date == loopDate.Date);

                bool isAvailable;

                if (loopDate.Date < todayPH)
                {
                    isAvailable = false; // Past dates are locked
                }
                else if (loopDate.DayOfWeek == DayOfWeek.Tuesday)
                {
                    isAvailable = false; // Shop is closed on Tuesdays
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