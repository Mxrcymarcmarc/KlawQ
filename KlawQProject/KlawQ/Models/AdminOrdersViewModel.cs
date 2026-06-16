using System.Collections.Generic;

namespace KlawQ.Models
{
    public class AdminOrdersViewModel
    {
        public List<Order> Orders { get; set; } = new();
        public string SelectedStatus { get; set; } = "All"; // "All", "Pending", "Completed", "Cancelled"
        public int SelectedMonthsFilter { get; set; } = 3;  // Default to 3 months to prevent clutter
    }
}
