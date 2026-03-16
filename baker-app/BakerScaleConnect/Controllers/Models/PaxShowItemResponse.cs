namespace BakerScaleConnect.Controllers.Models
{
    /// <summary>
    /// Response model for displaying items on the PAX terminal.
    /// </summary>
    public class PaxShowItemResponse
    {
        /// <summary>Whether the display operation was successful.</summary>
        public bool Success { get; set; }

        /// <summary>Response code from the terminal.</summary>
        public string? ResponseCode { get; set; }

        /// <summary>Response message from the terminal.</summary>
        public string? ResponseMessage { get; set; }

        /// <summary>ECR reference number that was used.</summary>
        public string? EcrReferenceNumber { get; set; }

        /// <summary>Error message if the operation failed.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Timestamp of the operation (UTC).</summary>
        public DateTime Timestamp { get; set; }
    }
}
