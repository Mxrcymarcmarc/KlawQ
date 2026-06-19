namespace KlawQ.Models
{
    /// <summary>
    /// ViewModel representing application runtime errors and tracking requests.
    /// Covers Encapsulation: Restricts request details via set-once properties and exposes computed Boolean check fields (ShowRequestId).
    /// </summary>
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
