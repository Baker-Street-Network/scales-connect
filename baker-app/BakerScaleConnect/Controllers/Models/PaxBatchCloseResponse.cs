namespace BakerScaleConnect.Controllers.Models
{
    /// <summary>
    /// Response model for PAX terminal batch close operations.
    /// </summary>
    public class PaxBatchCloseResponse
    {
        /// <summary>Whether the batch close was successful.</summary>
        public bool Success { get; set; }

        /// <summary>Response code from the terminal.</summary>
        public string ResponseCode { get; set; } = "";

        /// <summary>Response message from the terminal.</summary>
        public string ResponseMessage { get; set; } = "";

        /// <summary>ECR reference number that was used.</summary>
        public string? EcrReferenceNumber { get; set; }

        /// <summary>Batch number that was closed.</summary>
        public string? BatchNumber { get; set; }

        /// <summary>Host response information.</summary>
        public string? HostResponse { get; set; }

        /// <summary>Error message if the batch close failed.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Timestamp of the operation (UTC).</summary>
        public DateTime Timestamp { get; set; }
    }
}
