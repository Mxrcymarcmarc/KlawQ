using KlawQ.Data;
using KlawQ.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace KlawQ.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminCalendarController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        private static readonly TimeZoneInfo PhilippineTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");

        public AdminCalendarController(ApplicationDbContext context)
        {
            _context = context;
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


        // Fetches an entire month status grid for the Admin layout view.
        // It strictly follows the User Calendar rules for IsAvailable, but allows infinite month browsing.
        [HttpGet("month-admin-status")]
        public async Task<IActionResult> GetMonthAdminStatus([FromQuery] int year, [FromQuery] int month)
        {
            if (year <= 0 || month < 1 || month > 12)
                return BadRequest("Invalid year or month");

            var monthlyBookings = await _context.Schedulers
                .Include(s => s.Appointment)
                .Where(s => s.Appointment_Date.Year == year && s.Appointment_Date.Month == month)
                .ToListAsync();

            var adminBlocks = await _context.CalendarConfigures
                .Where(o => o.TargetDate.Year == year && o.TargetDate.Month == month)
                .ToListAsync();

            DateTime todayPH = TodayInPH();
            int daysInMonth = DateTime.DaysInMonth(year, month);
            var adminDaysGrid = new List<object>(daysInMonth);

            for (int day = 1; day <= daysInMonth; day++)
            {
                var loopDate = new DateTime(year, month, day);
                int maxSlotsForThisDay = GetMaxSlotsForDay(loopDate.DayOfWeek);

                int totalBookingsForDay = monthlyBookings.Count(b =>
                    b.Appointment_Date.Date == loopDate.Date &&
                    b.Appointment != null &&
                    b.Appointment.Down_Payment_Paid);

                bool isDayBlockedByAdmin = adminBlocks.Any(o => o.TargetDate.Date == loopDate.Date && o.BlockedHour == null);

                // Replicating your user-facing rule tree logic explicitly
                bool isAvailable;
                string dayStatus;

                if (loopDate.Date < todayPH)
                {
                    isAvailable = false;
                    dayStatus = "Past Date";
                }
                else if (loopDate.Date == todayPH)
                {
                    isAvailable = false;
                    dayStatus = "Today";
                }
                else if (loopDate.Date == todayPH.AddDays(1))
                {
                    isAvailable = false;
                    dayStatus = "Tomorrow";
                }
                else if (loopDate.DayOfWeek == DayOfWeek.Tuesday)
                {
                    isAvailable = false;
                    dayStatus = "Shop Closed";
                }
                else if (isDayBlockedByAdmin)
                {
                    isAvailable = false;
                    dayStatus = "Admin Blocked";
                }
                else if (totalBookingsForDay >= maxSlotsForThisDay)
                {
                    isAvailable = false;
                    dayStatus = "Fully Booked";
                }
                else
                {
                    isAvailable = true;
                    dayStatus = "Available";
                }

                adminDaysGrid.Add(new
                {
                    DateString = loopDate.ToString("yyyy-MM-dd"),
                    DayOfWeek = loopDate.DayOfWeek.ToString(),
                    IsAvailable = isAvailable, // Syncs with frontend disabling rules
                    Status = dayStatus,        // Drives individual color coding types
                    BookingsCount = totalBookingsForDay,
                    MaxSlotsAvailable = maxSlotsForThisDay
                });
            }

            return Ok(adminDaysGrid);
        }


        // Gives the admin a specialized view of time slots for a selected date
        [HttpGet("day-slots-admin-status")]
        public async Task<IActionResult> GetDaySlotsAdminStatus([FromQuery] string chosenDate)
        {
            if (!DateTime.TryParseExact(chosenDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime parsedDate))
                return BadRequest("Invalid date format. Please use yyyy-MM-dd.");

            // Fetch real customer bookings that have been paid for
            var paidBookings = await _context.Schedulers
                .Include(s => s.Appointment)
                .Where(s => s.Appointment_Date.Date == parsedDate.Date &&
                            s.Appointment != null &&
                            s.Appointment.Down_Payment_Paid)
                .ToListAsync();

            // Fetch existing manual admin configurations/blocks for this date
            var adminBlocks = await _context.CalendarConfigures
                .Where(o => o.TargetDate.Date == parsedDate.Date)
                .ToListAsync();

            int[] businessHours = GetBusinessHoursForDay(parsedDate.DayOfWeek);
            var adminSlotsPayload = new List<object>();

            foreach (int hour in businessHours)
            {
                DateTime exactSlotTime = parsedDate.Date.AddHours(hour);

                bool isBookedByCustomer = paidBookings.Any(b => b.Time_Slot == exactSlotTime);
                bool isBlockedByAdmin = adminBlocks.Any(o => o.BlockedHour == null || o.BlockedHour == hour);

                // Determine the exact current status to drive the admin dashboard UI element switches
                string calculatedStatus = "Available";
                if (isBookedByCustomer) calculatedStatus = "Booked";
                else if (isBlockedByAdmin) calculatedStatus = "Blocked";

                adminSlotsPayload.Add(new
                {
                    SlotHour = hour,
                    FormattedTime = GetFormattedTime(hour),
                    Status = calculatedStatus // "Available", "Booked", or "Blocked"
                });
            }

            return Ok(adminSlotsPayload);
        }


        // Block a slot (with a guard to prevent blocking a real booking)
        [HttpPost("block-slot")]
        public async Task<IActionResult> BlockSlot([FromQuery] DateTime date, [FromQuery] int? hour)
        {
            // Prevent blocking if a customer already reserved and paid for this exact coordinate
            if (hour.HasValue)
            {
                DateTime exactSlotTime = date.Date.AddHours(hour.Value);
                bool isAlreadyBooked = await _context.Schedulers
                    .Include(s => s.Appointment)
                    .AnyAsync(s => s.Time_Slot == exactSlotTime &&
                                   s.Appointment != null &&
                                   s.Appointment.Down_Payment_Paid);

                if (isAlreadyBooked)
                    return BadRequest("Action Denied: You cannot block a slot that has an active, paid booking!");
            }
            else
            {
                // If blocking a whole day, check if ANY paid appointments exist on that date
                bool dayHasBookings = await _context.Schedulers
                    .Include(s => s.Appointment)
                    .AnyAsync(s => s.Appointment_Date.Date == date.Date &&
                                   s.Appointment != null &&
                                   s.Appointment.Down_Payment_Paid);

                if (dayHasBookings)
                    return BadRequest("Action Denied: Cannot block this day because it already contains active bookings!");
            }

            // Check if block rule configuration already exists
            bool exists = await _context.CalendarConfigures
                .AnyAsync(o => o.TargetDate.Date == date.Date && o.BlockedHour == hour);

            if (exists) return BadRequest("This configuration block is already active.");

            var customBlock = new CalendarConfigure
            {
                TargetDate = date.Date,
                BlockedHour = hour
            };

            _context.CalendarConfigures.Add(customBlock);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Slot successfully blocked." });
        }


        // Undo a block (Make it available again)
        [HttpDelete("unblock-slot")]
        public async Task<IActionResult> UnblockSlot([FromQuery] DateTime date, [FromQuery] int? hour)
        {
            var targetBlock = await _context.CalendarConfigures
                .FirstOrDefaultAsync(o => o.TargetDate.Date == date.Date && o.BlockedHour == hour);

            if (targetBlock == null) return NotFound("No matching configuration block was found.");

            _context.CalendarConfigures.Remove(targetBlock);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Slot is available for bookings again!" });
        }
    }
}