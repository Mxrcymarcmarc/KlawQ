using System.ComponentModel.DataAnnotations;

namespace KlawQ.Models
{
    public class OrderSubmitModel
    {
        public int[] ProductIds { get; set; } = new int[0];
        public string FullName { get; set; } = string.Empty;
        public string SocialAccount { get; set; } = string.Empty;
        public string DeliveryLocation { get; set; } = string.Empty;
        public string DeliveryMethod { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string? HandPhotoBase64 { get; set; }
        public string? ThumbPhotoBase64 { get; set; }
    }

    public class OrderStartViewModel
    {
        public List<Products> Products { get; set; } = new();
        public OrderSubmitModel Submit { get; set; } = new();
    }
}
