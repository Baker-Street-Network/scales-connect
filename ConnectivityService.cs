namespace BakerScaleConnect.Services
{
    /// <summary>
    /// Service for checking scanner connectivity status.
    /// </summary>
    public class ConnectivityService(ScannerManager scannerManager)
    {
        /// <summary>
        /// Gets the overall connectivity status.
        /// </summary>
        public ConnectivityStatusResponse GetConnectivityStatus()
        {
            var result = scannerManager.DiscoverScanners();
            
            return new()
            {
                IsConnected = result.Success && result.ScannersFound > 0,
                ScannersFound = result.ScannersFound,
                ErrorMessage = result.Success ? null : result.ErrorMessage,
                LastChecked = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Gets detailed information about all discovered scanners.
        /// </summary>
        public List<ScannerResponse> GetScanners()
        {
            var result = scannerManager.DiscoverScanners();
            
            if (!result.Success || result.ScannersFound == 0)
                return [];

            return [.. scannerManager.DiscoveredScanners.Select(s => new ScannerResponse
            {
                ScannerID = s.ScannerID,
                ScannerType = s.ScannerType,
                FriendlyType = s.GetFriendlyType(),
                SerialNumber = s.SerialNo,
                ModelNumber = s.ModelNo,
                GUID = s.GUID,
                Firmware = s.Firmware,
                Port = s.Port
            })];
        }
    }
}
