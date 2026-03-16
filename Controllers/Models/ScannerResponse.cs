namespace BakerScaleConnect
{
    /// <summary>
    /// Response model for scanner information.
    /// </summary>
    public class ScannerResponse
    {
        public string ScannerID { get; set; } = "";
        public string ScannerType { get; set; } = "";
        public string FriendlyType { get; set; } = "";
        public string SerialNumber { get; set; } = "";
        public string ModelNumber { get; set; } = "";
        public string GUID { get; set; } = "";
        public string Firmware { get; set; } = "";
        public string Port { get; set; } = "";
    }
}
