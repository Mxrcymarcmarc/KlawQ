using System.ComponentModel.DataAnnotations;

namespace KlawQ.Models
{
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
        public required int Contact_Number { get; set; }
        public required string Hand_Photo { get; set; } = string.Empty;
        public required string Thumb_Photo { get; set; } = string.Empty;
        public required char Order_Type { get; set; } // 'P' for press-ons and 'C' for custom requests
        public required string Status { get; set; } = string.Empty;

        // Navigation
        public List<OrderItem> Items { get; set; } = new();
    }

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
