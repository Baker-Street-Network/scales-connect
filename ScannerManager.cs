using System.Xml;
using CoreScanner;

namespace BakerScaleConnect
{
    /// <summary>
    /// Manages CoreScanner SDK operations: discovery, host mode switching, keyboard emulation.
    /// </summary>
    public class ScannerManager : IDisposable
    {
        // CoreScanner opcodes
        private const int REGISTER_FOR_EVENTS = 1001;
        private const int DEVICE_SWITCH_HOST_MODE = 6200;
        private const int KEYBOARD_EMULATOR_ENABLE = 6300;
        private const int KEYBOARD_EMULATOR_GET_CONFIG = 6302;
        private const int SCALE_READ_WEIGHT = 7000;
        private const int RSM_ATTR_GET = 5001;
        private const int RSM_ATTR_SET = 5004;

        // RSM attribute IDs
        private const int ATTR_BEEPER_VOLUME = 140;

        // Scanner type constants for Open()
        private const short SCANNER_TYPES_ALL = 1;

        // Event subscription flags
        private const int SUBSCRIBE_BARCODE = 1;
        private const int SUBSCRIBE_IMAGE = 2;
        private const int SUBSCRIBE_VIDEO = 4;
        private const int SUBSCRIBE_RMD = 8;
        private const int SUBSCRIBE_PNP = 16;
        private const int SUBSCRIBE_OTHER = 32;
        private const int NUM_SCANNER_EVENTS = 6;

        // PnP event types
        private const int SCANNER_ATTACHED = 0;
        private const int SCANNER_DETACHED = 1;

        // Status codes
        private const int STATUS_SUCCESS = 0;

        // SNAPI host mode code
        private const string SNAPI_WITH_IMAGING = "XUA-45001-9";

        private CCoreScannerClass? _coreScanner;
        private bool _isOpen;
        private bool _eventsWired;
        private readonly List<ScannerInfo> _discoveredScanners = new();

        /// <summary>
        /// Fired when a scanner is attached or detached (PnP event from CoreScanner).
        /// The bool parameter is true for attach, false for detach.
        /// </summary>
        public event Action<bool>? ScannerPnPChanged;

        /// <summary>
        /// The list of scanners found during the last discovery.
        /// </summary>
        public IReadOnlyList<ScannerInfo> DiscoveredScanners => _discoveredScanners.AsReadOnly();

        /// <summary>
        /// The primary scanner to use for commands (first discovered).
        /// </summary>
        public ScannerInfo? PrimaryScanner => _discoveredScanners.Count > 0 ? _discoveredScanners[0] : null;

        public void InitializeCoreScanner()
        {
            // Initialize CoreScanner COM object
            if (_coreScanner == null)
                try
                {
                    _coreScanner = new CCoreScannerClass();
                }
                catch
                {
                    Thread.Sleep(1000);
                    _coreScanner = new CCoreScannerClass();
                }
        }

        /// <summary>
        /// Initializes the CoreScanner COM object and opens communication.
        /// Discovers all connected scanners and registers for events.
        /// </summary>
        /// <returns>A result with scanner count or error message.</returns>
        public ScannerDiscoveryResult DiscoverScanners()
        {
            try
            {
                InitializeCoreScanner();

                // Open the CoreScanner with all scanner types
                if (!_isOpen)
                {
                    int appHandle = 0;
                    short[] scannerTypes = { SCANNER_TYPES_ALL };
                    short numberOfTypes = 1;

                    _coreScanner.Open(appHandle, scannerTypes, numberOfTypes, out int status);
                    if (status != STATUS_SUCCESS)
                        return new ScannerDiscoveryResult
                        {
                            Success = false,
                            ErrorMessage = $"Failed to open CoreScanner (status: {status})"
                        };
                    _isOpen = true;
                }

                // Register for events
                RegisterForEvents();

                // Get connected scanners
                _discoveredScanners.Clear();
                int[] scannerIdList = new int[255];

                _coreScanner.GetScanners(out short numberOfScanners, scannerIdList, out string outXml, out int getStatus);
                if (getStatus != STATUS_SUCCESS)
                    return new ScannerDiscoveryResult
                    {
                        Success = false,
                        ErrorMessage = $"Failed to get scanners (status: {getStatus})"
                    };

                // Parse the XML response to extract scanner info
                ParseScannersXml(outXml, numberOfScanners);

                return new ScannerDiscoveryResult
                {
                    Success = true,
                    ScannersFound = _discoveredScanners.Count
                };
            }
            catch (Exception ex)
            {
                return new ScannerDiscoveryResult
                {
                    Success = false,
                    ErrorMessage = $"Scanner discovery error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Switches the specified scanner to SNAPI host mode.
        /// </summary>
        public (bool Success, string Message) SwitchToSnapi(string scannerId)
        {
            if (_coreScanner == null || !_isOpen)
                InitializeCoreScanner();

            try
            {
                string inXml = "<inArgs>" +
                    "<scannerID>" + scannerId + "</scannerID>" +
                    "<cmdArgs>" +
                    "<arg-string>" + SNAPI_WITH_IMAGING + "</arg-string>" +
                    "<arg-bool>TRUE</arg-bool>" +   // Silent switch
                    "<arg-bool>TRUE</arg-bool>" +    // Permanent change
                    "</cmdArgs>" +
                    "</inArgs>";

                _coreScanner.ExecCommand(DEVICE_SWITCH_HOST_MODE, ref inXml, out string outXml, out int status);

                if (status == STATUS_SUCCESS)
                    return (true, "Switched to SNAPI mode successfully");
                else
                    return (false, $"Switch to SNAPI failed (status: {status})");
            }
            catch (Exception ex)
            {
                return (false, $"Switch to SNAPI error: {ex.Message}");
            }
        }

        /// <summary>
        /// Enables or disables keyboard emulation.
        /// </summary>
        public (bool Success, string Message) SetKeyboardEmulation(bool enable)
        {
            if (_coreScanner == null || !_isOpen)
                InitializeCoreScanner();

            try
            {
                string inXml = "<inArgs><cmdArgs><arg-bool>" +
                    (enable ? "TRUE" : "FALSE") +
                    "</arg-bool></cmdArgs></inArgs>";

                _coreScanner.ExecCommand(KEYBOARD_EMULATOR_ENABLE, ref inXml, out string outXml, out int status);

                if (status == STATUS_SUCCESS)
                    return (true, enable ? "Keyboard emulation enabled" : "Keyboard emulation disabled");
                else
                    return (false, $"Keyboard emulation failed (status: {status})");
            }
            catch (Exception ex)
            {
                return (false, $"Keyboard emulation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Registers for all scanner events (barcode, image, video, RMD, PnP, other).
        /// </summary>
        private void RegisterForEvents()
        {
            if (_coreScanner == null || !_isOpen) return;

            try
            {
                string eventIds = $"{SUBSCRIBE_BARCODE},{SUBSCRIBE_IMAGE},{SUBSCRIBE_VIDEO},{SUBSCRIBE_RMD},{SUBSCRIBE_PNP},{SUBSCRIBE_OTHER}";
                string inXml = "<inArgs>" +
                    "<cmdArgs>" +
                    "<arg-int>" + NUM_SCANNER_EVENTS + "</arg-int>" +
                    "<arg-int>" + eventIds + "</arg-int>" +
                    "</cmdArgs>" +
                    "</inArgs>";

                _coreScanner.ExecCommand(REGISTER_FOR_EVENTS, ref inXml, out string outXml, out int status);

                // Wire up the COM event handler once
                if (!_eventsWired)
                {
                    _coreScanner.PNPEvent += OnPNPEvent;
                    _eventsWired = true;
                }
            }
            catch
            {
                // Non-critical failure — events just won't fire
            }
        }

        /// <summary>
        /// Handles PnP events from the CoreScanner SDK (scanner attach / detach).
        /// </summary>
        private void OnPNPEvent(short eventType, ref string pnpData)
        {
            bool attached = eventType == SCANNER_ATTACHED;
            ScannerPnPChanged?.Invoke(attached);
        }

        /// <summary>
        /// Queries the keyboard emulation configuration from CoreScanner.
        /// Returns whether emulation is enabled and the current locale index.
        /// </summary>
        public KeyboardEmulationStatus GetKeyboardEmulationConfig()
        {
            if (_coreScanner == null || !_isOpen)
                InitializeCoreScanner();

            try
            {
                string inXml = "<inArgs></inArgs>";
                _coreScanner.ExecCommand(KEYBOARD_EMULATOR_GET_CONFIG, ref inXml, out string outXml, out int status);

                if (status != STATUS_SUCCESS)
                    return new KeyboardEmulationStatus { Success = false, ErrorMessage = $"Get config failed (status: {status})" };

                // Parse response XML: <KeyEnumState>0|1</KeyEnumState> <KeyEnumLocale>n</KeyEnumLocale>
                var xmlDoc = new XmlDocument();
                xmlDoc.XmlResolver = null;
                xmlDoc.LoadXml(outXml);

                string stateStr = xmlDoc.DocumentElement?.GetElementsByTagName("KeyEnumState").Item(0)?.InnerText ?? "0";
                string localeStr = xmlDoc.DocumentElement?.GetElementsByTagName("KeyEnumLocale").Item(0)?.InnerText ?? "0";

                return new KeyboardEmulationStatus
                {
                    Success = true,
                    Enabled = stateStr == "1",
                    LocaleIndex = int.TryParse(localeStr, out int loc) ? loc : 0
                };
            }
            catch (Exception ex)
            {
                return new KeyboardEmulationStatus { Success = false, ErrorMessage = $"Get config error: {ex.Message}" };
            }
        }

        /// <summary>
        /// Parses the XML returned by GetScanners to populate the scanner list.
        /// Follows the same pattern as zebra-example XmlReader.ReadXmlString_GetScanners.
        /// </summary>
        private void ParseScannersXml(string xml, int expectedCount)
        {
            if (string.IsNullOrEmpty(xml) || expectedCount < 1) return;

            try
            {
                using var reader = new XmlTextReader(new StringReader(xml));
                reader.XmlResolver = null;
                reader.WhitespaceHandling = WhitespaceHandling.Significant;

                string elementName = "";
                ScannerInfo? current = null;
                bool inScanner = false;

                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            elementName = reader.Name;
                            if (elementName == "scanner")
                            {
                                inScanner = false;
                                string? scannerType = reader.GetAttribute("type");
                                if (reader.HasAttributes && !string.IsNullOrEmpty(scannerType))
                                {
                                    inScanner = true;
                                    current = new ScannerInfo { ScannerType = scannerType };
                                    _discoveredScanners.Add(current);
                                }
                            }
                            break;

                        case XmlNodeType.Text:
                            if (inScanner && current != null)
                            {
                                string value = reader.Value;
                                switch (elementName)
                                {
                                    case "scannerID": current.ScannerID = value; break;
                                    case "serialnumber": current.SerialNo = value; break;
                                    case "modelnumber": current.ModelNo = value; break;
                                    case "GUID": current.GUID = value; break;
                                    case "port": current.Port = value; break;
                                    case "firmware": current.Firmware = value; break;
                                }
                            }
                            break;
                    }
                }
            }
            catch
            {
                // XML parse errors are non-fatal; we keep whatever scanners were parsed
            }
        }

        /// <summary>
        /// Closes the CoreScanner connection.
        /// </summary>
        public void Close()
        {
            if (_coreScanner != null && _isOpen)
            {
                try
                {
                    int appHandle = 0;
                    _coreScanner.Close(appHandle, out int status);
                }
                catch { }
                _isOpen = false;
            }
        }

        public void Dispose() => Close();

        /// <summary>
        /// Gets the current beeper volume from the scanner via RSM attribute 140.
        /// Returns: 0 = High, 1 = Medium, 2 = Low.
        /// </summary>
        public (bool Success, int Volume, string ErrorMessage) GetBeeperVolume()
        {
            if (_coreScanner == null || !_isOpen)
                return (false, -1, "CoreScanner not initialized");

            var scanner = PrimaryScanner;
            if (scanner == null)
                return (false, -1, "No scanner connected");

            try
            {
                string inXml = "<inArgs>" +
                    "<scannerID>" + scanner.ScannerID + "</scannerID>" +
                    "<cmdArgs>" +
                    "<arg-xml>" +
                    "<attrib_list>" + ATTR_BEEPER_VOLUME + "</attrib_list>" +
                    "</arg-xml>" +
                    "</cmdArgs>" +
                    "</inArgs>";

                _coreScanner.ExecCommand(RSM_ATTR_GET, ref inXml, out string outXml, out int status);

                if (status != STATUS_SUCCESS)
                    return (false, -1, $"Get beeper volume failed (status: {status})");

                // Parse the value from <value>N</value>
                var xmlDoc = new XmlDocument();
                xmlDoc.XmlResolver = null;
                xmlDoc.LoadXml(outXml);
                string valStr = xmlDoc.DocumentElement?.GetElementsByTagName("value").Item(0)?.InnerText ?? "-1";
                int volume = int.TryParse(valStr, out int v) ? v : -1;
                return (true, volume, "");
            }
            catch (Exception ex)
            {
                return (false, -1, $"Get beeper volume error: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets the beeper volume via RSM attribute 140.
        /// Values: 0 = High, 1 = Medium, 2 = Low.
        /// </summary>
        public (bool Success, string Message) SetBeeperVolume(int volume)
        {
            if (_coreScanner == null || !_isOpen)
                return (false, "CoreScanner not initialized");

            var scanner = PrimaryScanner;
            if (scanner == null)
                return (false, "No scanner connected");

            try
            {
                string inXml = "<inArgs>" +
                    "<scannerID>" + scanner.ScannerID + "</scannerID>" +
                    "<cmdArgs>" +
                    "<arg-xml>" +
                    "<attrib_list>" +
                    "<attribute>" +
                    "<id>" + ATTR_BEEPER_VOLUME + "</id>" +
                    "<datatype>B</datatype>" +
                    "<value>" + volume + "</value>" +
                    "</attribute>" +
                    "</attrib_list>" +
                    "</arg-xml>" +
                    "</cmdArgs>" +
                    "</inArgs>";

                _coreScanner.ExecCommand(RSM_ATTR_SET, ref inXml, out string outXml, out int status);

                if (status == STATUS_SUCCESS)
                    return (true, "Beeper volume set successfully");
                else
                    return (false, $"Set beeper volume failed (status: {status})");
            }
            catch (Exception ex)
            {
                return (false, $"Set beeper volume error: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads the current weight from the scanner's embedded scale.
        /// Uses CoreScanner opcode 7000 (SCALE_READ_WEIGHT).
        /// </summary>
        public ScaleWeightResult ReadWeight()
        {
            if (_coreScanner == null || !_isOpen)
                InitializeCoreScanner();

            var scanner = PrimaryScanner;
            if (scanner == null)
                return new ScaleWeightResult { Success = false, ErrorMessage = "No scanner connected" };

            try
            {
                string inXml = "<inArgs>" +
                    "<scannerID>" + scanner.ScannerID + "</scannerID>" +
                    "</inArgs>";

                _coreScanner.ExecCommand(SCALE_READ_WEIGHT, ref inXml, out string outXml, out int status);

                if (status != STATUS_SUCCESS)
                    return new ScaleWeightResult { Success = false, ErrorMessage = $"Scale read failed (status: {status})" };

                return ParseScaleXml(outXml);
            }
            catch (Exception ex)
            {
                return new ScaleWeightResult { Success = false, ErrorMessage = $"Scale read error: {ex.Message}" };
            }
        }

        /// <summary>
        /// Parses the XML response from SCALE_READ_WEIGHT.
        /// Expected tags: weight, weight_mode, status.
        /// </summary>
        private static ScaleWeightResult ParseScaleXml(string xml)
        {
            if (string.IsNullOrEmpty(xml))
                return new ScaleWeightResult { Success = false, ErrorMessage = "Empty scale response" };

            try
            {
                using var reader = new XmlTextReader(new StringReader(xml));
                reader.XmlResolver = null;
                reader.WhitespaceHandling = WhitespaceHandling.Significant;

                string elementName = "";
                string weight = "";
                string weightMode = "";
                int scaleStatus = -1;

                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            elementName = reader.Name;
                            break;
                        case XmlNodeType.Text:
                            switch (elementName)
                            {
                                case "weight":
                                    weight = reader.Value;
                                    break;
                                case "weight_mode":
                                    weightMode = reader.Value;
                                    break;
                                case "status":
                                    if (int.TryParse(reader.Value, out int s))
                                        scaleStatus = s;
                                    break;
                            }
                            break;
                    }
                }

                return new ScaleWeightResult
                {
                    Success = true,
                    Weight = weight,
                    WeightUnit = weightMode,
                    ScaleStatus = scaleStatus,
                    ScaleStatusDescription = GetScaleStatusDescription(scaleStatus)
                };
            }
            catch (Exception ex)
            {
                return new ScaleWeightResult { Success = false, ErrorMessage = $"Scale XML parse error: {ex.Message}" };
            }
        }

        private static string GetScaleStatusDescription(int status) => status switch
        {
            0 => "Scale Not Enabled",
            1 => "Scale Not Ready",
            2 => "Stable Weight Over Limit",
            3 => "Stable Weight Under Zero",
            4 => "Non-Stable Weight",
            5 => "Stable Zero Weight",
            6 => "Stable Non-Zero Weight",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Result of a scanner discovery operation.
    /// </summary>
    public class ScannerDiscoveryResult
    {
        public bool Success { get; set; }
        public int ScannersFound { get; set; }
        public string ErrorMessage { get; set; } = "";
    }

    /// <summary>
    /// Result of querying keyboard emulation configuration.
    /// </summary>
    public class KeyboardEmulationStatus
    {
        public bool Success { get; set; }
        public bool Enabled { get; set; }
        public int LocaleIndex { get; set; }
        public string ErrorMessage { get; set; } = "";
    }

    /// <summary>
    /// Result of a scale weight reading.
    /// </summary>
    public class ScaleWeightResult
    {
        public bool Success { get; set; }
        public string Weight { get; set; } = "";
        public string WeightUnit { get; set; } = "";
        public int ScaleStatus { get; set; } = -1;
        public string ScaleStatusDescription { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
    }
}
