using System.ComponentModel.DataAnnotations;

namespace KlawQ.Models
{
    public class Products
    {
        public int ProductID { get; set; }
        public required string Product_Name { get; set; } = string.Empty;
        public required decimal Product_Price { get; set; }
        public required string Product_Description { get; set; } = string.Empty;
        public required int Product_Stock { get; set; } = 0;
        public required string Product_Image { get; set; } = string.Empty;
    }

    public class OrderItem
    {
        public int OrderItemID { get; set; }
        public int OrderID { get; set; }
        public int ProductID { get; set; }
        public required int Quantity { get; set; } = 1;
        public required int Subtotal { get; set; }
    }
}
