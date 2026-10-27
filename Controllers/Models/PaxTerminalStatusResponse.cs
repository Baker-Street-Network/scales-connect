namespace BakerScaleConnect.Controllers.Models
{
    /// <summary>
    /// Response model for the PAX terminal status endpoint.
    /// </summary>
    public class PaxTerminalStatusResponse
    {
        /// <summary>Scales Connect version, e.g. "1.0.9".</summary>
        public string Version { get; set; } = "";

        /// <summary>Capabilities of this Scales Connect build. Callers should check these instead of comparing versions.</summary>
        public string[] Features { get; set; } = [];

        /// <summary>Whether the terminal answered. True while a transaction is in progress.</summary>
        public bool Connected { get; set; }

        /// <summary>Whether a transaction or other terminal command is in progress. Terminal details are then the last known values.</summary>
        public bool Busy { get; set; }

        /// <summary>Serial number of the connected terminal.</summary>
        public string? TerminalSn { get; set; }

        /// <summary>Terminal model name.</summary>
        public string? TerminalModel { get; set; }

        /// <summary>Payment application on the terminal (e.g. BroadPOS).</summary>
        public string? TerminalAppName { get; set; }

        /// <summary>Payment application version.</summary>
        public string? TerminalAppVersion { get; set; }

        /// <summary>Whether the terminal is in demo mode. Null: POSLink does not report this, so it is unknown.</summary>
        public bool? DemoMode { get; set; }

        /// <summary>Last batch close run through this Scales Connect since it started (UTC). Null if none.</summary>
        public DateTime? LastBatchCloseUtc { get; set; }

        /// <summary>When the terminal details were last read from the terminal (UTC).</summary>
        public DateTime? CheckedAtUtc { get; set; }

        /// <summary>Why the terminal could not be reached, if it could not.</summary>
        public string? ErrorMessage { get; set; }
    }
}
