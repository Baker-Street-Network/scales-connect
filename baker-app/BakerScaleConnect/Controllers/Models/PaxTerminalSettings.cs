namespace BakerScaleConnect.Controllers.Models
{
    /// <summary>
    /// Settings for PAX terminal connection.
    /// </summary>
    public class PaxTerminalSettings
    {
        /// <summary>Terminal IP address.</summary>
        public string Ip { get; set; } = "127.0.0.1";

        /// <summary>Terminal port.</summary>
        public int Port { get; set; } = 10009;

        /// <summary>Connection timeout in milliseconds.</summary>
        public int Timeout { get; set; } = 60000;
    }
}
