namespace BakerScaleConnect
{
    /// <summary>
    /// Response model for connectivity status.
    /// </summary>
    public class ConnectivityStatusResponse
    {
        public bool IsConnected { get; set; }
        public int ScannersFound { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime LastChecked { get; set; }
    }
}
