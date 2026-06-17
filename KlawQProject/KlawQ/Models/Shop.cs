using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KlawQ.Models
{
    public class Favorite
    {
        [Key]
        public int FavoriteID { get; set; }
        public int ProductID { get; set; }
        public int UserID { get; set; }
        public DateTime CreatedAt { get; set; }

        public Products? Product { get; set; }
    }

    public class Cart
    {
        [Key]
        public int CartId { get; set; }
        public int UserID { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<CartItem> Items { get; set; } = [];
    }

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
