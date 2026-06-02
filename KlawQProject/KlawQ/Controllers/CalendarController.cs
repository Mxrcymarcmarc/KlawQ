using KlawQ.Models;
using KlawQ.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace KlawQ.Controllers
{
    public class CalendarDayStatus
    {
        public string DateString { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]

    public class CalendarController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public calendarcontroller(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("current-view-status")]
        public async Task<IActionResult> GetCurrentCalendarView()
        {
            DateTime today = DateTime.Now;
            int currentYear = today.Year;
            int currentMonth = today.Month;

            var currentMonthDays = await GetDaysStatusForMonth(currentYear, currentMonth);

            int availableDaysLeft = currentMonthDays.Count(d => d.IsAvailable && DateTime.Parse(d.DateString) >= today);

            const int Almost_full_treshold = 5;

            var finalResponse = new List<CalendarDayStatus>();
            finalResponse.AddRange(currentMonthDays);

        }
    }
}
