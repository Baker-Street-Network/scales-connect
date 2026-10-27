namespace BakerScaleConnect.Controllers.Models
{
    /// <summary>
    /// Response model for PAX credit card payment transactions.
    /// </summary>
    public class PaxCreditResponse
    {
        /// <summary>Whether the transaction was successful.</summary>
        public bool Success { get; set; }

        /// <summary>Response code from the terminal.</summary>
        public string ResponseCode { get; set; } = "";

        /// <summary>Response message from the terminal.</summary>
        public string ResponseMessage { get; set; } = "";

        /// <summary>ECR reference number that was used.</summary>
        public string? EcrReferenceNumber { get; set; }

        /// <summary>Error message if the transaction failed.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Timestamp of the transaction (UTC).</summary>
        public DateTime Timestamp { get; set; }
        public string? CardBin { get; set; }
        public string? AuthorizationCode {get;set;}
        public string? BatchNumber {get;set;}
        public string? ControlNumber {get;set;}
        public string? GatewayTransactionId {get;set;}
        public string? HostDetailedMessage {get;set;}
        public string? HostReferenceNumber {get;set;}

        /// <summary>Terminal record number (TraceInformation.ReferenceNumber). Needed to void this transaction.</summary>
        public string? ReferenceNumber { get; set; }

        /// <summary>Unique transaction ID (TraceInformation.GlobalUid).</summary>
        public string? GlobalUid { get; set; }

        /// <summary>Serial number of the terminal that ran the transaction (TraceInformation.Sn).</summary>
        public string? TerminalSn { get; set; }

        /// <summary>Terminal local date and time of the transaction, YYYYMMDDhhmmss.</summary>
        public string? TerminalTimestamp { get; set; }

        /// <summary>Approved amount in cents (AmountInformation.ApprovedAmount).</summary>
        public string? ApprovedAmount { get; set; }

        /// <summary>Processor card token (CofInformation.Token), when the processor returns one.</summary>
        public string? Token { get; set; }

        public string RawResponse { get; set; }
    }
}
