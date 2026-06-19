using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KlawQ.Models
{
    /// <summary>
    /// Model representing a user's favorite product reference.
    /// Covers Encapsulation: Wraps database key links and favorite timestamp details while holding a reference to the related Product.
    /// </summary>
    public class Favorite
    {
        [Key]
        public int FavoriteID { get; set; }
        public int ProductID { get; set; }
        public int UserID { get; set; }
        public DateTime CreatedAt { get; set; }

        public Products? Product { get; set; }
    }

    /// <summary>
    /// Model representing a user's shopping cart container.
    /// Covers Encapsulation: Bundles shopping basket identifier details and manages the aggregated CartItem list.
    /// </summary>
    public class Cart
    {
        [Key]
        public int CartId { get; set; }
        public int UserID { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<CartItem> Items { get; set; } = [];
    }

    /// <summary>
    /// Model representing an individual line item inside a shopping cart.
    /// Covers Encapsulation: Encapsulates line item quantity and keeps relational references to parent Cart and Product models.
    /// </summary>
    public class CartItem
    {
        [Key]
        public int CartItemId { get; set; }
        public int CartId { get; set; }
        public int ProductID { get; set; }
        public int Quantity { get; set; } = 1;

        public Cart? Cart { get; set; }
        public Products? Product { get; set; }
    }
}
