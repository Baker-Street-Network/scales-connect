namespace BakerScaleConnect.Controllers.Models
{
    /// <summary>
    /// Request model for PAX terminal batch close operations.
    /// </summary>
    public class PaxBatchCloseRequest
    {
        /// <summary>ECR (Electronic Cash Register) reference number for tracking.</summary>
        public string EcrReferenceNumber { get; set; } = "";
    }
}
