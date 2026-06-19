using System.ComponentModel.DataAnnotations;

namespace KlawQ.Models
{
    /// <summary>
    /// Model representing a customer's product order or custom nail design request.
    /// Covers Encapsulation: Bundles shipping, payment, contact parameters, and aggregates child line items (Items list).
    /// </summary>
    public class Order
    {
        public int OrderID { get; set; }
        public int UserID { get; set; }
        public DateTime Order_Date { get; set; }
        public required string Full_Name { get; set; } = string.Empty;
        public required string Social_Account { get; set; } = string.Empty;
        public required string Delivery_Location { get; set; } = string.Empty;
        public required string Delivery_Method { get; set; } = string.Empty;
        public required string Payment_Method { get; set; } = string.Empty;
        public required string Contact_Number { get; set; } = string.Empty;
        public required string Hand_Photo { get; set; } = string.Empty;
        public required string Thumb_Photo { get; set; } = string.Empty;
        public required char Order_Type { get; set; } // 'P' for press-ons and 'C' for custom requests
        public required string Status { get; set; } = string.Empty;

        // Navigation
        public List<OrderItem> Items { get; set; } = [];
    }

    /// <summary>
    /// Model representing an individual line item linked to a parent Order.
    /// Covers Encapsulation: Controls raw item specifications and holds navigation reference properties.
    /// </summary>
    public class OrderItem
    {
        public int OrderItemID { get; set; }
        public int OrderID { get; set; }
        public int ProductID { get; set; }
        public int Quantity { get; set; } = 1;

        public Order? Order { get; set; }
        public Products? Product { get; set; }
    }
}
