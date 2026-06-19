using System.Collections.Generic;

namespace KlawQ.Models
{
    /// <summary>
    /// ViewModel representing orders in the Admin panel.
    /// Covers Encapsulation: Bundles data fields (Orders list, filters) into a single object, shielding internal representation and managing access via public properties.
    /// </summary>
    public class AdminOrdersViewModel
    {
        public List<Order> Orders { get; set; } = [];
        public string SelectedStatus { get; set; } = "All"; // "All", "Pending", "Completed", "Cancelled"
        public int SelectedMonthsFilter { get; set; } = 3;  // Default to 3 months to prevent clutter
    }
}
