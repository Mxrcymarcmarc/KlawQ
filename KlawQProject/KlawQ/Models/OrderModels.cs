using System.ComponentModel.DataAnnotations;

namespace KlawQ.Models
{
    public class OrderSubmitModel
    {
        [Required]
        public int[] ProductIds { get; set; } = [];

        // Quantities aligned with ProductIds by index (optional, defaults to 1)
        public int[] Quantities { get; set; } = [];

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string SocialAccount { get; set; } = string.Empty;

        [Required]
        public string DeliveryLocation { get; set; } = string.Empty;

        [Required]
        public string DeliveryMethod { get; set; } = string.Empty;

        public string PaymentMethod { get; set; } = string.Empty;

        [Required]
        public string ContactNumber { get; set; } = string.Empty;

        public string? HandPhotoBase64 { get; set; }
        public string? ThumbPhotoBase64 { get; set; }
    }

    public class CustomOrderSubmitModel : OrderSubmitModel
    {
        // Allow custom orders without selecting existing products
        public new int[] ProductIds { get; set; } = [];

        [Required]
        public string DesignNotes { get; set; } = string.Empty;
    }

    public class OrderStartViewModel
    {
        public List<Products> Products { get; set; } = [];
        public OrderSubmitModel Submit { get; set; } = new();

        // Map of ProductID -> Quantity for quick access in views
        public Dictionary<int,int> ProductQuantities { get; set; } = [];
    }
}
