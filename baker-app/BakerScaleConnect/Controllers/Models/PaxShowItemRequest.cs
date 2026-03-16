namespace BakerScaleConnect.Controllers.Models
{
    /// <summary>
    /// Request model for displaying items on the PAX terminal.
    /// </summary>
    public class PaxShowItemRequest
    {
        /// <summary>List of items to display on the terminal.</summary>
        public List<PaxDisplayItem> Items { get; set; } = new();

        /// <summary>Optional ECR reference number for tracking.</summary>
        public string? EcrReferenceNumber { get; set; }
    }

    /// <summary>
    /// Represents an item to be displayed on the PAX terminal.
    /// </summary>
    public class PaxDisplayItem
    {
        /// <summary>Item name/description.</summary>
        public string Name { get; set; } = "";

        /// <summary>Item price (e.g., "10.50").</summary>
        public string Price { get; set; } = "";

        /// <summary>Item quantity.</summary>
        public int Quantity { get; set; } = 1;

        /// <summary>Optional SKU or item code.</summary>
        public string? Sku { get; set; }
    }
}
