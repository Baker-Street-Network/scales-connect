namespace BakerScaleConnect.Controllers.Models
{
    /// <summary>
    /// Request model for PAX credit card payment transactions.
    /// </summary>
    public class PaxCreditRequest
    {
        /// <summary>Transaction amount as a decimal string (e.g. "10.50").</summary>
        public string Amount { get; set; } = "";

        /// <summary>ECR (Electronic Cash Register) reference number for tracking.</summary>
        public string EcrReferenceNumber { get; set; } = "";

        /// <summary>Transaction type (e.g., "Sale", "Return", "Void").</summary>
        public string? TransactionType { get; set; }
    }
}
