using System.ComponentModel.DataAnnotations;

namespace KlawQ.Models
{
    public class Products
    {
        [Key]
        public int ProductID { get; set; }
        public required string Product_Name { get; set; } = string.Empty;
        public required decimal Product_Price { get; set; }
        public required string Product_Description { get; set; } = string.Empty;
        public required int Product_Stock { get; set; } = 0;
        public required string Product_Image { get; set; } = string.Empty;
        // Type: "Original" (appointment) or "PressOn" (order)
        public required string Product_Type { get; set; } = "Original";
    }

}
