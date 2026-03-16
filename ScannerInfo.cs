namespace BakerScaleConnect
{
    /// <summary>
    /// Stores discovered scanner information needed for connection and commands.
    /// </summary>
    public class ScannerInfo
    {
        public string ScannerID { get; set; } = "";
        public string ScannerType { get; set; } = "";
        public string SerialNo { get; set; } = "";
        public string ModelNo { get; set; } = "";
        public string GUID { get; set; } = "";
        public string Firmware { get; set; } = "";
        public string Port { get; set; } = "";

        /// <summary>
        /// Returns a friendly display name for the scanner type.
        /// </summary>
        public string GetFriendlyType()
        {
            return ScannerType switch
            {
                "SNAPI" => "SNAPI",
                "SSI" => "SSI",
                "USBHIDKB" => "HID Keyboard",
                "USBIBMHID" => "IBM HID",
                "USBOPOS" => "USB OPOS",
                "USBIBMTT" => "IBM Table-Top",
                "USBIBMSCALE" => "IBM Scale",
                "SSI_BT" => "SSI Bluetooth",
                "SSI_IP" => "SSI IP",
                "UVC_CAMERA" => "UVC Camera",
                "NIXMODB" => "Nixdorf Mode B",
                _ => ScannerType
            };
        }

        public override string ToString() =>
            $"ID:{ScannerID} Type:{GetFriendlyType()} Model:{ModelNo} SN:{SerialNo}";
    }
}
