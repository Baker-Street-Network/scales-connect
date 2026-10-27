namespace BakerScaleConnect.Controllers.Models
{
    /// <summary>
    /// Response model for looking up a transaction in the terminal's local report.
    /// When found, it carries the same fields as <see cref="PaxCreditResponse"/>.
    /// </summary>
    public class PaxTransactionLookupResponse : PaxCreditResponse
    {
        /// <summary>Whether an approved transaction with this ECR reference is in the terminal's local report.</summary>
        public bool Found { get; set; }

        /// <summary>Number of matching records the terminal reported.</summary>
        public string? TotalRecord { get; set; }

        /// <summary>Transaction type of the record, e.g. "Sale" or "Return".</summary>
        public string? TransactionType { get; set; }

        /// <summary>Last 4 digits of the card, when the terminal reports them.</summary>
        public string? Last4 { get; set; }

        /// <summary>Last batch close run through this Scales Connect since it started (UTC). A close clears the local report.</summary>
        public DateTime? LastBatchCloseUtc { get; set; }
    }
}
