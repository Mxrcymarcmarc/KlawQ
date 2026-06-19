using System.ComponentModel.DataAnnotations;

namespace KlawQ.Models
{
    /// <summary>
    /// Model capturing standard order submissions.
    /// Covers Encapsulation: Restricts field assignment with required validations and email formatting annotations.
    /// </summary>
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

    /// <summary>
    /// Model capturing custom nail design order submissions.
    /// Covers Inheritance: Inherits from OrderSubmitModel to reuse baseline details.
    /// Covers Polymorphism: Shadows standard ProductIds property to support custom requests.
    /// </summary>
    public class CustomOrderSubmitModel : OrderSubmitModel
    {
        // Allow custom orders without selecting existing products
        public new int[] ProductIds { get; set; } = [];

        [Required]
        public string DesignNotes { get; set; } = string.Empty;
    }

    /// <summary>
    /// ViewModel for rendering checkout initialization page state.
    /// Covers Encapsulation: Bundles checkout products list, customer submission sub-model, and quantity key maps together.
    /// </summary>
    public class OrderStartViewModel
    {
        public List<Products> Products { get; set; } = [];
        public OrderSubmitModel Submit { get; set; } = new();

        // Map of ProductID -> Quantity for quick access in views
        public Dictionary<int,int> ProductQuantities { get; set; } = [];
    }
}
