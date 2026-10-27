namespace BakerScaleConnect.Controllers.Models
{
    /// <summary>
    /// Request model for PAX credit card payment transactions.
    /// </summary>
    public class PaxCreditRequest
    {
        /// <summary>Transaction amount as a decimal string (e.g. "10.50"). Not used for a Void.</summary>
        public string Amount { get; set; } = "";

        /// <summary>ECR (Electronic Cash Register) reference number for tracking.</summary>
        public string EcrReferenceNumber { get; set; } = "";

        /// <summary>Transaction type: "Sale" (default), "Return" or "Void".</summary>
        public string? TransactionType { get; set; }

        /// <summary>Terminal record number of the original sale (TraceInformation.ReferenceNumber). Required for a Void.</summary>
        public string? OriginalReferenceNumber { get; set; }

        /// <summary>ECR reference number of the original sale.</summary>
        public string? OriginalEcrReferenceNumber { get; set; }

        /// <summary>Serial number of the terminal that ran the original sale (TraceInformation.Sn).</summary>
        public string? OriginalTerminalSn { get; set; }

        /// <summary>Original transaction date, YYYYMMDD.</summary>
        public string? OriginalTransactionDate { get; set; }

        /// <summary>Original transaction time, HHMMSS.</summary>
        public string? OriginalTransactionTime { get; set; }

        /// <summary>Amount of the original sale as a decimal string (e.g. "4.36"). On a Void, checked against the terminal's record.</summary>
        public string? OriginalAmount { get; set; }

        /// <summary>Last 4 digits of the card used for the original sale.</summary>
        public string? OriginalLast4 { get; set; }

        /// <summary>Processor reference of the original sale (HostInformation.HostReferenceNumber).</summary>
        public string? OriginalHostReferenceNumber { get; set; }

        /// <summary>Card-network transaction ID of the original sale (CofInformation.TransactionIdentifier).</summary>
        public string? OriginalTransactionIdentifier { get; set; }

        /// <summary>False runs a Return without the card. Null or true is a normal card-present Return.</summary>
        public bool? CardPresent { get; set; }

        /// <summary>Processor token for the card, from an earlier Sale that requested one.</summary>
        public string? Token { get; set; }

        /// <summary>Ask the processor to return a card token with this Sale.</summary>
        public bool RequestToken { get; set; }
    }
}
