namespace BakerScaleConnect.Controllers.Models
{
    /// <summary>
    /// Settings for PAX terminal connection.
    /// </summary>
    public class PaxTerminalSettings
    {
        /// <summary>Connection method (TCP or USB).</summary>
        public string ConnectionMethod { get; set; } = "TCP";

        /// <summary>Terminal IP address (for TCP).</summary>
        public string Ip { get; set; } = "127.0.0.1";

        /// <summary>Terminal port (for TCP).</summary>
        public int Port { get; set; } = 10009;

        /// <summary>Connection timeout in milliseconds.</summary>
        public int Timeout { get; set; } = 60000;

        /// <summary>Serial port name (for USB).</summary>
        public string SerialPort { get; set; } = "";
    }
}
