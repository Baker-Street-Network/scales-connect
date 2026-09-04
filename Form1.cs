using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BakerScaleConnect.Services;
using System.IO.Ports;
using System.Reflection;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace BakerScaleConnect
{
    public partial class Form1 : Form
    {
        private NotifyIcon? _notifyIcon;
        private Icon? _trayIcon;
        private IntPtr _trayIconHandle;
        private ContextMenuStrip? _contextMenu;
        private readonly IHost _host;
        private readonly ScannerManager _scannerManager;
        private readonly PaxService _paxService;
        private readonly UpdateService _updateService;
        private System.Windows.Forms.Timer? _updateStatusTimer;
        private System.Windows.Forms.Timer? _retryTimer;
        private int _retryCount;
        private const int RETRY_INTERVAL_MS = 5000; // 5 seconds between retries
        private AppSettings _settings;
        private bool _isLoadingPaxSettings;
        private bool _isExiting;

        public Form1(IHost host)
        {
            InitializeComponent();
            _host = host;
            _scannerManager = host.Services.GetRequiredService<ScannerManager>();
            _paxService = host.Services.GetRequiredService<PaxService>();

            // Use the shared AppSettings singleton from DI
            _settings = host.Services.GetRequiredService<AppSettings>();

            _updateService = host.Services.GetRequiredService<UpdateService>();

            SetupSystemTray();
            SetupForm();
            SetupUpdateStatus();
            WireButtonEvents();
            LoadPaxSettings();
            LoadCashDrawerSettings();

            // Discover scanners immediately on startup
            this.Load += Form1_Load;
        }

        private void WireButtonEvents()
        {
            button1.Click += BtnSetSnapi_Click;
            button2.Click += BtnSetEmulation_Click;
            comboVolume.SelectedIndexChanged += ComboVolume_SelectedIndexChanged;

            // PAX terminal events
            button4.Click += BtnTestConnection_Click;
            btnTestTransaction.Click += BtnTestTransaction_Click;
            connectionMethodComboBox.SelectedIndexChanged += ConnectionMethod_Changed;
            terminalIp.TextChanged += PaxSettings_Changed;
            portNumber.TextChanged += PaxSettings_Changed;
            timeoutTextBox.TextChanged += PaxSettings_Changed;
            serialPortComboBox.SelectedIndexChanged += PaxSettings_Changed;

            // Cash drawer events
            cashDrawerPortComboBox.SelectedIndexChanged += CashDrawerSettings_Changed;
            btnReloadCashDrawerPorts.Click += (s, e) => PopulateCashDrawerPorts();
        }

        private void Form1_Load(object? sender, EventArgs e)
        {
            // Hide buttons and volume until we know the scanner state
            button1.Visible = false;
            button2.Visible = false;
            comboVolume.Visible = false;
            labelVolume.Visible = false;

            // Listen for scanner attach/detach events
            _scannerManager.ScannerPnPChanged += OnScannerPnPChanged;

            DiscoverAndUpdateUI();
        }

        /// <summary>
        /// Called by the CoreScanner PnP event when a scanner is attached or detached.
        /// Marshals back to the UI thread and refreshes state.
        /// </summary>
        private void OnScannerPnPChanged(bool attached)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => OnScannerPnPChanged(attached));
                return;
            }

            if (attached)
            {
                label4.Text = "🔌 Scanner attached — refreshing...";
            }
            else
            {
                label4.Text = "⚠️ Scanner disconnected — waiting for reconnect...";
                label5.Text = "";
                label6.Text = "";
                button1.Visible = false;
                button2.Visible = false;
                comboVolume.Visible = false;
                labelVolume.Visible = false;
            }

            // Small delay to let the device fully enumerate before re-querying
            var delayTimer = new System.Windows.Forms.Timer { Interval = 1500 };
            delayTimer.Tick += (s, e) =>
            {
                delayTimer.Stop();
                delayTimer.Dispose();
                DiscoverAndUpdateUI();
            };
            delayTimer.Start();
        }

        /// <summary>
        /// Starts a periodic timer that retries scanner discovery.
        /// </summary>
        private void StartRetryTimer()
        {
            if (_retryTimer != null) return; // already running

            _retryCount = 0;
            _retryTimer = new System.Windows.Forms.Timer { Interval = RETRY_INTERVAL_MS };
            _retryTimer.Tick += RetryTimer_Tick;
            _retryTimer.Start();
        }

        private void StopRetryTimer()
        {
            if (_retryTimer == null) return;
            _retryTimer.Stop();
            _retryTimer.Dispose();
            _retryTimer = null;
            _retryCount = 0;
        }

        private void RetryTimer_Tick(object? sender, EventArgs e)
        {
            _retryCount++;
            label4.Text = $"🔄 Retrying scanner discovery (attempt {_retryCount})...";
            DiscoverAndUpdateUI();
        }

        /// <summary>
        /// Runs scanner discovery and updates all UI labels based on results.
        /// Queries scanner status (interface type, keyboard emulation) and
        /// enables/disables buttons accordingly.
        /// </summary>
        private void DiscoverAndUpdateUI()
        {
            var result = _scannerManager.DiscoverScanners();

            if (!result.Success)
            {
                label4.Text = "🚫 " + result.ErrorMessage;
                label5.Text = "";
                label6.Text = "";
                button1.Visible = false;
                button2.Visible = false;
                comboVolume.Visible = false;
                labelVolume.Visible = false;
                StartRetryTimer();
                return;
            }

            var scanners = _scannerManager.DiscoveredScanners;

            if (scanners.Count == 0)
            {
                label4.Text = "🔍 No scanner found — retrying automatically...";
                label5.Text = "";
                label6.Text = "";
                button1.Visible = false;
                button2.Visible = false;
                comboVolume.Visible = false;
                labelVolume.Visible = false;
                StartRetryTimer();
                return;
            }

            // Scanner(s) found — stop retrying
            StopRetryTimer();

            var primary = scanners[0];
            if (scanners.Count == 1)
                label4.Text = $"✅️ Scanner found: {primary.ModelNo} (SN: {primary.SerialNo})";
            else
                label4.Text = $"⚠️ {scanners.Count} scanners found — using first (ID {primary.ScannerID})";

            // --- Interface type & SNAPI button ---
            bool isSnapi = string.Equals(primary.ScannerType, "SNAPI", StringComparison.OrdinalIgnoreCase);
            label5.Text = isSnapi
                ? $"✅️ Interface: {primary.GetFriendlyType()}"
                : $"⚠️ Interface: {primary.GetFriendlyType()} (not SNAPI)";
            button1.Visible = !isSnapi;
            button1.Enabled = !isSnapi;
            button1.Text = isSnapi ? "Already SNAPI" : "Set SNAPI";

            // --- Keyboard emulation status & button ---
            var kbStatus = _scannerManager.GetKeyboardEmulationConfig();
            if (kbStatus.Success)
            {
                if (kbStatus.Enabled)
                {
                    label6.Text = "✅️ Keyboard Emulation: On";
                    button2.Visible = false;
                    button2.Enabled = false;
                    button2.Text = "Already Enabled";
                }
                else
                {
                    label6.Text = "⏳ Keyboard Emulation: Off";
                    button2.Visible = true;
                    button2.Enabled = true;
                    button2.Text = "Enable Emulation";
                }
            }
            else
            {
                label6.Text = "⚠️ Keyboard Emulation: Unknown";
                button2.Visible = true;
                button2.Enabled = true;
                button2.Text = "Enable Emulation";
            }

            // --- Beeper volume ---
            labelVolume.Visible = true;
            comboVolume.Visible = true;
            var volResult = _scannerManager.GetBeeperVolume();
            // Temporarily unhook the event so programmatic selection doesn't trigger a SET
            comboVolume.SelectedIndexChanged -= ComboVolume_SelectedIndexChanged;
            if (volResult.Success)
            {
                // SDK values: 0=High, 1=Medium, 2=Low  |  ComboBox: 0=Low, 1=Medium, 2=High
                comboVolume.SelectedIndex = volResult.Volume switch
                {
                    0 => 2, // High
                    1 => 1, // Medium
                    2 => 0, // Low
                    _ => 2  // Default to High if unknown
                };
            }
            else
            {
                comboVolume.SelectedIndex = 2; // Default to High
            }
            comboVolume.SelectedIndexChanged += ComboVolume_SelectedIndexChanged;
        }

        /// <summary>
        /// Called when the user changes the volume dropdown.
        /// </summary>
        private void ComboVolume_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // ComboBox: 0=Low, 1=Medium, 2=High  ->  SDK: 2=Low, 1=Medium, 0=High
            int sdkValue = comboVolume.SelectedIndex switch
            {
                0 => 2, // Low
                1 => 1, // Medium
                2 => 0, // High
                _ => 0  // Default to High if unknown
            };

            var (success, message) = _scannerManager.SetBeeperVolume(sdkValue);
            if (!success)
            {
                MessageBox.Show(message, "Baker Scale Connect", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Switch the primary scanner to SNAPI host mode.
        /// </summary>
        private void BtnSetSnapi_Click(object? sender, EventArgs e)
        {
            var scanner = _scannerManager.PrimaryScanner;
            if (scanner == null)
            {
                MessageBox.Show("No scanner discovered. Cannot switch host mode.",
                    "Baker Scale Connect", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            button1.Enabled = false;
            button1.Text = "Switching...";
            var (success, message) = _scannerManager.SwitchToSnapi(scanner.ScannerID);
            if (success)
            {
                label5.Text = "✅️ Switched to SNAPI — scanner will reconnect...";
                // After host mode switch the scanner re-enumerates; start retry to pick it up
                StartRetryTimer();
            }
            else
            {
                button1.Enabled = true;
                button1.Text = "Set SNAPI";
                MessageBox.Show(message, "Baker Scale Connect", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Enable keyboard emulation.
        /// </summary>
        private void BtnSetEmulation_Click(object? sender, EventArgs e)
        {
            button2.Enabled = false;
            button2.Text = "Enabling...";
            var (success, message) = _scannerManager.SetKeyboardEmulation(true);
            if (success)
            {
                label6.Text = "✅️ Keyboard Emulation: On";
                button2.Text = "Already Enabled";
            }
            else
            {
                button2.Enabled = true;
                button2.Text = "Enable Emulation";
                MessageBox.Show(message, "Baker Scale Connect", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetupForm()
        {
            // Minimize to tray instead of taskbar
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;

            // Handle form closing to minimize to tray instead
            this.FormClosing += Form1_FormClosing;
            this.Resize += Form1_Resize;

            ShowVersion();
        }

        /// <summary>
        /// Stamps the running version in the top-right of the window. Support needs to
        /// be able to read it off a customer's screen without digging through folders.
        /// </summary>
        private void ShowVersion()
        {
            labelVersion.Text = $"v{GetDisplayVersion()}";

            // AutoSize means the width isn't known until the text is set, so pin the
            // label to the right edge here rather than at a fixed design-time point.
            labelVersion.Left = this.ClientSize.Width - labelVersion.Width - 10;

            _notifyIcon!.Text = $"Baker Scale Connect v{GetDisplayVersion()}";
        }

        /// <summary>
        /// Shows what the auto-updater is doing and when it next runs. The service
        /// raises an event on each phase change; the timer only re-renders the
        /// countdown text between those changes.
        /// </summary>
        private void SetupUpdateStatus()
        {
            _updateService.StatusChanged += OnUpdateStatusChanged;

            _updateStatusTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
            _updateStatusTimer.Tick += (s, e) => RenderUpdateStatus();
            _updateStatusTimer.Start();

            RenderUpdateStatus();
        }

        private void OnUpdateStatusChanged(object? sender, EventArgs e)
        {
            // Raised from the updater's background thread.
            if (IsDisposed) return;

            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(RenderUpdateStatus);
                    return;
                }

                RenderUpdateStatus();
            }
            catch (InvalidOperationException)
            {
                // Handle not created yet, or the form is tearing down.
            }
        }

        private void RenderUpdateStatus()
        {
            labelUpdateStatus.Text = _updateService.Phase switch
            {
                UpdatePhase.Checking => "Checking for updates...",
                UpdatePhase.Downloading => $"{_updateService.LastResult}...",
                UpdatePhase.Disabled => _updateService.LastResult ?? "Auto-updates disabled",
                _ => DescribeNextCheck()
            };
        }

        private string DescribeNextCheck()
        {
            var next = _updateService.NextCheckUtc;
            if (next is null)
                return _updateService.LastResult ?? "Update check pending";

            string when = FormatCountdown(next.Value - DateTimeOffset.UtcNow);

            return _updateService.LastResult is null
                ? $"Next update check {when}"
                : $"{_updateService.LastResult} - next check {when}";
        }

        private static string FormatCountdown(TimeSpan remaining)
        {
            if (remaining <= TimeSpan.Zero) return "now";
            if (remaining.TotalMinutes < 1) return "in under a minute";
            if (remaining.TotalHours < 1) return $"in {remaining.Minutes}m";

            return $"in {(int)remaining.TotalHours}h {remaining.Minutes}m";
        }

        private static string GetDisplayVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();

            var informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informational))
            {
                // Strip the "+<commit hash>" build metadata the SDK appends.
                int plus = informational.IndexOf('+');
                return plus >= 0 ? informational[..plus] : informational;
            }

            return assembly.GetName().Version?.ToString(3) ?? "unknown";
        }

        private void SetupSystemTray()
        {
            // Create context menu for the tray icon
            _contextMenu = new ContextMenuStrip();
            _contextMenu.Items.Add("Show", null, ShowApplication);
            _contextMenu.Items.Add("Hide", null, HideApplication);
            _contextMenu.Items.Add("-"); // Separator
            _contextMenu.Items.Add("Exit", null, ExitApplication);

            // Create the NotifyIcon, reusing the window's own icon so the tray matches
            // the title bar and the taskbar.
            _notifyIcon = new NotifyIcon()
            {
                Icon = CreateTrayIcon(),
                ContextMenuStrip = _contextMenu,
                Text = "Baker Scale Connect",
                Visible = true
            };

            // Handle double-click to show/hide the application
            _notifyIcon.DoubleClick += (s, e) => ToggleApplicationVisibility();
        }

        /// <summary>
        /// Builds the tray icon from the form's own icon, so the notification area
        /// matches the title bar and the taskbar.
        ///
        /// pos.ico ships a single 256x256 frame, and <c>new Icon(icon, size)</c> only
        /// selects the nearest frame that already exists — it does not rescale — so it
        /// would hand the shell a 256px image to squeeze into a 16px slot. Render the
        /// tray-sized copy ourselves instead.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private Icon CreateTrayIcon()
        {
            if (this.Icon is null)
                return SystemIcons.Application;

            try
            {
                Size size = SystemInformation.SmallIconSize;

                using var source = this.Icon.ToBitmap();
                using var scaled = new Bitmap(size.Width, size.Height);

                using (var g = Graphics.FromImage(scaled))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(source, 0, 0, size.Width, size.Height);
                }

                // Icon.FromHandle does not take ownership of the HICON, so hold onto
                // the handle and destroy it explicitly in Dispose.
                _trayIconHandle = scaled.GetHicon();
                _trayIcon = Icon.FromHandle(_trayIconHandle);
                return _trayIcon;
            }
            catch (Exception ex) when (ex is ArgumentException or ExternalException)
            {
                // Unreadable icon resource or a GDI+ failure — falling back to the
                // stock icon beats failing startup over a piece of chrome.
                return SystemIcons.Application;
            }
        }

        private void Form1_Resize(object? sender, EventArgs e)
        {
            // Hide to system tray when minimized
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Hide();
                this.ShowInTaskbar = false;
                _notifyIcon!.ShowBalloonTip(2000, "Baker Scale Connect",
                    "Application minimized to system tray", ToolTipIcon.Info);
            }
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // Prevent closing and minimize to tray instead
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.WindowState = FormWindowState.Minimized;
            }
        }

        private void ShowApplication(object? sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            this.BringToFront();
        }

        private void HideApplication(object? sender, EventArgs e)
        {
            this.Hide();
            this.ShowInTaskbar = false;
        }

        private void ToggleApplicationVisibility()
        {
            if (this.Visible)
            {
                HideApplication(null, EventArgs.Empty);
            }
            else
            {
                ShowApplication(null, EventArgs.Empty);
            }
        }

        private async void ExitApplication(object? sender, EventArgs e)
        {
            _isExiting = true;

            // Stop the background service gracefully
            await _host.StopAsync();

            // Clean up the notify icon
            _notifyIcon?.Dispose();

            // Exit the application
            Application.Exit();
        }

        /// <summary>
        /// Called when the generic host stops. On a normal exit that is just the tail
        /// of <see cref="ExitApplication"/> and there is nothing left to do; otherwise
        /// a hosted service failed fatally and the UI has to come down with it rather
        /// than leave a windowless process holding the device handles.
        /// </summary>
        internal void ShutdownFromHost()
        {
            if (_isExiting) return;

            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(ShutdownFromHost);
                    return;
                }
            }
            catch (InvalidOperationException)
            {
                // The window handle doesn't exist yet — the failure happened before the
                // message loop started. Nothing to marshal to, so exit outright.
                Environment.Exit(1);
                return;
            }

            _isExiting = true;
            _notifyIcon?.Dispose();
            Application.Exit();
        }

        /// <summary>
        /// Called from the single-instance listener thread when a duplicate launch
        /// asks us to surface. Marshals onto the UI thread before touching the form.
        /// </summary>
        internal void ShowFromSecondInstance()
        {
            if (_isExiting) return;

            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(ShowFromSecondInstance);
                    return;
                }

                ShowApplication(null, EventArgs.Empty);
            }
            catch (InvalidOperationException)
            {
                // Handle not created yet, or the form is being torn down. Ignore —
                // failing to raise the window is never worth crashing over.
            }
        }

        #region PAX Terminal Methods

        /// <summary>
        /// Load PAX terminal settings from file and populate the UI.
        /// </summary>
        private void LoadPaxSettings()
        {
            // Suppress auto-save while we populate controls from disk — otherwise
            // assigning each .Text fires TextChanged/SelectedIndexChanged and
            // SavePaxSettings() would clobber _settings with the still-default UI values.
            _isLoadingPaxSettings = true;
            try
            {
                System.Diagnostics.Debug.WriteLine($"LoadPaxSettings: Loading settings...");
                System.Diagnostics.Debug.WriteLine($"  ConnectionMethod: {_settings.PaxTerminal.ConnectionMethod}");
                System.Diagnostics.Debug.WriteLine($"  IpAddress: {_settings.PaxTerminal.IpAddress}");
                System.Diagnostics.Debug.WriteLine($"  Port: {_settings.PaxTerminal.Port}");
                System.Diagnostics.Debug.WriteLine($"  Timeout: {_settings.PaxTerminal.Timeout}");
                System.Diagnostics.Debug.WriteLine($"  SerialPort: '{_settings.PaxTerminal.SerialPort}'");

                connectionMethodComboBox.Text = _settings.PaxTerminal.ConnectionMethod;
                terminalIp.Text = _settings.PaxTerminal.IpAddress;
                portNumber.Text = _settings.PaxTerminal.Port.ToString();
                timeoutTextBox.Text = _settings.PaxTerminal.Timeout.ToString();

                // Populate serial ports with saved selection
                PopulateSerialPorts(_settings.PaxTerminal.SerialPort);

                // Show appropriate tab based on connection method
                UpdateTabVisibility();

                // Update the PaxService with loaded settings
                UpdatePaxService();
            }
            finally
            {
                _isLoadingPaxSettings = false;
            }
        }

        /// <summary>
        /// Populate the serial port combo box with available ports.
        /// </summary>
        private void PopulateSerialPorts(string? savedSelection = null)
        {
            try
            {
                // Use saved selection if provided, otherwise use current selection
                string currentSelection = savedSelection ?? serialPortComboBox.Text;

                System.Diagnostics.Debug.WriteLine($"PopulateSerialPorts: savedSelection={savedSelection}, currentSelection={currentSelection}");

                serialPortComboBox.Items.Clear();

                string[] ports = SerialPort.GetPortNames();
                if (ports.Length > 0)
                {
                    foreach (string port in ports)
                    {
                        serialPortComboBox.Items.Add(port);
                    }

                    // Restore previous selection if it still exists
                    if (!string.IsNullOrEmpty(currentSelection) && serialPortComboBox.Items.Contains(currentSelection))
                    {
                        serialPortComboBox.Text = currentSelection;
                        System.Diagnostics.Debug.WriteLine($"PopulateSerialPorts: Restored selection to {currentSelection}");
                    }
                    else if (serialPortComboBox.Items.Count > 0)
                    {
                        serialPortComboBox.SelectedIndex = 0;
                        System.Diagnostics.Debug.WriteLine($"PopulateSerialPorts: Set to default index 0: {serialPortComboBox.Text}");
                    }
                }
                else
                {
                    serialPortComboBox.Items.Add("No ports found");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error populating serial ports: {ex.Message}");
                serialPortComboBox.Items.Clear();
                serialPortComboBox.Items.Add("Error loading ports");
            }
        }

        /// <summary>
        /// Called when connection method changes (TCP vs USB).
        /// </summary>
        private void ConnectionMethod_Changed(object? sender, EventArgs e)
        {
            if (_isLoadingPaxSettings) return;

            UpdateTabVisibility();
            SavePaxSettings();

            // Refresh serial ports when switching to USB
            if (connectionMethodComboBox.Text == "USB")
            {
                PopulateSerialPorts();
            }
        }

        /// <summary>
        /// Update tab visibility based on selected connection method.
        /// </summary>
        private void UpdateTabVisibility()
        {
            if (connectionMethodComboBox.Text == "TCP")
            {
                if (!tabControl1.TabPages.Contains(tabPage1))
                    tabControl1.TabPages.Add(tabPage1);
                if (tabControl1.TabPages.Contains(tabPage2))
                    tabControl1.TabPages.Remove(tabPage2);
                tabControl1.SelectedTab = tabPage1;
            }
            else if (connectionMethodComboBox.Text == "USB")
            {
                if (tabControl1.TabPages.Contains(tabPage1))
                    tabControl1.TabPages.Remove(tabPage1);
                if (!tabControl1.TabPages.Contains(tabPage2))
                    tabControl1.TabPages.Add(tabPage2);
                tabControl1.SelectedTab = tabPage2;
            }
        }

        /// <summary>
        /// Called when any PAX setting textbox changes.
        /// </summary>
        private void PaxSettings_Changed(object? sender, EventArgs e)
        {
            if (_isLoadingPaxSettings) return;

            SavePaxSettings();
        }

        /// <summary>
        /// Save PAX settings to file and update the service.
        /// </summary>
        private void SavePaxSettings()
        {
            try
            {
                _settings.PaxTerminal.ConnectionMethod = connectionMethodComboBox.Text;
                _settings.PaxTerminal.IpAddress = terminalIp.Text;
                _settings.PaxTerminal.SerialPort = serialPortComboBox.Text;

                // Mirror the PAX terminal IP into the Aries config so the Aries 8
                // (customer display + callback) talks to the same device by default.
                // Only when TCP is selected and the IP is non-blank — otherwise we'd
                // clobber a previously-configured Aries IP with an empty/USB value
                // and break PhoneCollectService.
                if (connectionMethodComboBox.Text == "TCP" && !string.IsNullOrWhiteSpace(terminalIp.Text))
                {
                    _settings.Aries.TerminalIp = terminalIp.Text;
                }

                if (int.TryParse(portNumber.Text, out int port))
                {
                    _settings.PaxTerminal.Port = port;
                }

                if (int.TryParse(timeoutTextBox.Text, out int timeout))
                {
                    _settings.PaxTerminal.Timeout = timeout;
                }

                System.Diagnostics.Debug.WriteLine($"SavePaxSettings: Saving SerialPort='{serialPortComboBox.Text}'");
                _settings.Save();
                UpdatePaxService();
            }
            catch (Exception ex)
            {
                // Log but don't show error for auto-save
                System.Diagnostics.Debug.WriteLine($"Error saving PAX settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Update the PaxService with current settings.
        /// </summary>
        private void UpdatePaxService()
        {
            _paxService.UpdateSettings(
                _settings.PaxTerminal.ConnectionMethod,
                _settings.PaxTerminal.IpAddress,
                _settings.PaxTerminal.Port,
                _settings.PaxTerminal.Timeout,
                _settings.PaxTerminal.SerialPort
            );
        }

        /// <summary>
        /// Test connection to the PAX terminal.
        /// </summary>
        private async void BtnTestConnection_Click(object? sender, EventArgs e)
        {
            string connectionMethod = connectionMethodComboBox.Text;

            // Validate based on connection method
            if (connectionMethod == "TCP")
            {
                if (string.IsNullOrWhiteSpace(terminalIp.Text))
                {
                    MessageBox.Show("Please enter a terminal IP address.",
                        "Baker Scale Connect", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!int.TryParse(portNumber.Text, out int port) || port <= 0 || port > 65535)
                {
                    MessageBox.Show("Please enter a valid port number (1-65535).",
                        "Baker Scale Connect", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            else if (connectionMethod == "USB")
            {
                MessageBox.Show("Connection test not supported on USB.",
                    "Baker Scale Connect", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!int.TryParse(timeoutTextBox.Text, out int timeout) || timeout < 1000)
            {
                MessageBox.Show("Please enter a valid timeout (minimum 1000ms).",
                    "Baker Scale Connect", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Save settings before testing
            SavePaxSettings();

            // Disable button and show testing state
            button4.Enabled = false;
            button4.Text = "Testing...";
            this.Cursor = Cursors.WaitCursor;

            bool success = false;
            string message = "";
            string connectionInfo = "";

            try
            {
                // TCP connection test
                string ipAddress = terminalIp.Text;
                int port = int.Parse(portNumber.Text);
                connectionInfo = $"{ipAddress}:{port}";

                await Task.Run(async () =>
                {
                    try
                    {
                        using (var client = new System.Net.Sockets.TcpClient())
                        {
                            using (var cts = new System.Threading.CancellationTokenSource(5000))
                            {
                                try
                                {
                                    await client.ConnectAsync(ipAddress, port, cts.Token);
                                    success = true;
                                    message = "Successfully connected to PAX terminal!";
                                }
                                catch (OperationCanceledException)
                                {
                                    success = false;
                                    message = "Connection timeout. Terminal did not respond within 5 seconds.";
                                }
                                catch (System.Net.Sockets.SocketException)
                                {
                                    success = false;
                                    message = "Could not establish connection to terminal.";
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        success = false;
                        message = ex.Message;
                    }
                });

                // Show result on UI thread
                if (success)
                {
                    MessageBox.Show(
                        $"✅ Connection successful!\n\n" +
                        $"Method: {connectionMethod}\n" +
                        $"Connection: {connectionInfo}\n" +
                        $"Status: Connected",
                        "PAX Terminal Connection Test",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"❌ Connection failed.\n\n" +
                        $"Method: {connectionMethod}\n" +
                        $"Connection: {connectionInfo}\n" +
                        $"Error: {message}\n\n" +
                        (connectionMethod == "TCP"
                            ? "Please verify:\n• Terminal IP address is correct\n• Terminal is powered on and connected to network\n• Port is accessible\n• No firewall is blocking the connection"
                            : "Please verify:\n• Terminal is powered on and connected via USB\n• PAX USB driver is installed\n• Terminal is configured for USB communication\n• PAX service is listening on localhost:10009"),
                        "PAX Terminal Connection Test",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"An error occurred while testing the connection:\n\n{ex.Message}",
                    "Baker Scale Connect",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                // Re-enable button
                button4.Enabled = true;
                button4.Text = "Test Connection";
                this.Cursor = Cursors.Default;
            }
        }

        /// <summary>
        /// Run a test transaction with the specified amount.
        /// </summary>
        private async void BtnTestTransaction_Click(object? sender, EventArgs e)
        {
            string connectionMethod = connectionMethodComboBox.Text;

            // Validate based on connection method
            if (connectionMethod == "TCP")
            {
                if (string.IsNullOrWhiteSpace(terminalIp.Text))
                {
                    MessageBox.Show("Please enter a terminal IP address.",
                        "Baker Scale Connect", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!int.TryParse(portNumber.Text, out int port) || port <= 0 || port > 65535)
                {
                    MessageBox.Show("Please enter a valid port number (1-65535).",
                        "Baker Scale Connect", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            else if (connectionMethod == "USB")
            {
                if (string.IsNullOrWhiteSpace(serialPortComboBox.Text) ||
                    serialPortComboBox.Text == "No ports found" ||
                    serialPortComboBox.Text == "Error loading ports")
                {
                    MessageBox.Show("Please select a valid serial port.",
                        "Baker Scale Connect", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            if (!int.TryParse(timeoutTextBox.Text, out int timeout) || timeout < 1000)
            {
                MessageBox.Show("Please enter a valid timeout (minimum 1000ms).",
                    "Baker Scale Connect", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Validate amount
            if (string.IsNullOrWhiteSpace(testAmountTextbox.Text))
            {
                MessageBox.Show("Please enter a transaction amount.",
                    "Baker Scale Connect", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!decimal.TryParse(testAmountTextbox.Text, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("Please enter a valid amount greater than 0.",
                    "Baker Scale Connect", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Build connection info string
            string connectionInfo = connectionMethod == "TCP"
                ? $"{terminalIp.Text}:{portNumber.Text}"
                : serialPortComboBox.Text;

            // Confirm the transaction
            var confirmResult = MessageBox.Show(
                $"Are you sure you want to run a test transaction for ${amount:F2}?\n\n" +
                $"Method: {connectionMethod}\n" +
                $"Connection: {connectionInfo}\n\n" +
                $"This will process a REAL transaction on the connected terminal.",
                "Confirm Test Transaction",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirmResult != DialogResult.Yes)
            {
                return;
            }

            // Save settings before processing
            SavePaxSettings();

            // Disable controls and show processing state
            btnTestTransaction.Enabled = false;
            btnTestTransaction.Text = "Processing...";
            button4.Enabled = false;
            connectionMethodComboBox.Enabled = false;
            terminalIp.Enabled = false;
            portNumber.Enabled = false;
            timeoutTextBox.Enabled = false;
            serialPortComboBox.Enabled = false;
            testAmountTextbox.Enabled = false;
            this.Cursor = Cursors.WaitCursor;

            try
            {
                // Create the payment request
                var request = new Controllers.Models.PaxCreditRequest
                {
                    Amount = amount.ToString("F0"),
                    EcrReferenceNumber = $"TEST-{DateTime.Now:yyyyMMddHHmmss}",
                    TransactionType = "Sale"
                };

                // Run the transaction with async/await
                var response = await _paxService.ProcessCreditPaymentAsync(request);

                // Show result on UI thread
                if (response.Success)
                {
                    MessageBox.Show(
                        $"✅ Transaction successful!\n\n" +
                        $"Amount: ${amount:F2}\n" +
                        $"Response Code: {response.ResponseCode}\n" +
                        $"Response Message: {response.ResponseMessage}\n" +
                        $"ECR Reference: {response.EcrReferenceNumber}\n" +
                        $"Timestamp: {response.Timestamp:yyyy-MM-dd HH:mm:ss}",
                        "PAX Transaction Result",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"❌ Transaction failed!\n\n" +
                        $"Amount: ${amount:F2}\n" +
                        $"Error: {response.ErrorMessage}\n" +
                        $"ECR Reference: {response.EcrReferenceNumber}\n\n" +
                        $"Please verify:\n" +
                        $"• Terminal is connected and ready\n" +
                        $"• Card is inserted/swiped properly\n" +
                        $"• Terminal is not busy with another transaction",
                        "PAX Transaction Result",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"An error occurred during the transaction:\n\n{ex.Message}",
                    "Baker Scale Connect",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                // Re-enable controls
                btnTestTransaction.Enabled = true;
                btnTestTransaction.Text = "Run Transaction";
                button4.Enabled = true;
                connectionMethodComboBox.Enabled = true;
                terminalIp.Enabled = true;
                portNumber.Enabled = true;
                timeoutTextBox.Enabled = true;
                serialPortComboBox.Enabled = true;
                testAmountTextbox.Enabled = true;
                this.Cursor = Cursors.Default;
            }
        }

        /// <summary>
        /// Empty event handler for label10 click (auto-generated by designer).
        /// </summary>
        private void label10_Click(object? sender, EventArgs e)
        {
            // No action needed
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopRetryTimer();
                _updateService.StatusChanged -= OnUpdateStatusChanged;
                _updateStatusTimer?.Dispose();
                _scannerManager?.Dispose();
                _notifyIcon?.Dispose();
                _trayIcon?.Dispose();
                if (_trayIconHandle != IntPtr.Zero) DestroyIcon(_trayIconHandle);
                _contextMenu?.Dispose();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void button3_Click(object sender, EventArgs e)
        {

        }

        private void button3_Click_1(object sender, EventArgs e)
        {
            PopulateSerialPorts();
        }

        private void button5_Click(object sender, EventArgs e)
        {
            BtnTestConnection_Click(sender, e);
        }

        #region Cash Drawer Methods

        private void LoadCashDrawerSettings()
        {
            PopulateCashDrawerPorts(_settings.CashDrawer.SerialPort);
        }

        private void PopulateCashDrawerPorts(string? savedSelection = null)
        {
            string currentSelection = savedSelection ?? cashDrawerPortComboBox.Text;
            cashDrawerPortComboBox.Items.Clear();

            try
            {
                string[] ports = System.IO.Ports.SerialPort.GetPortNames();
                if (ports.Length > 0)
                {
                    foreach (string port in ports)
                        cashDrawerPortComboBox.Items.Add(port);

                    if (!string.IsNullOrEmpty(currentSelection) && cashDrawerPortComboBox.Items.Contains(currentSelection))
                        cashDrawerPortComboBox.Text = currentSelection;
                    else if (cashDrawerPortComboBox.Items.Count > 0)
                        cashDrawerPortComboBox.SelectedIndex = 0;
                }
                else
                {
                    cashDrawerPortComboBox.Items.Add("No ports found");
                }
            }
            catch
            {
                cashDrawerPortComboBox.Items.Add("Error loading ports");
            }
        }

        private void CashDrawerSettings_Changed(object? sender, EventArgs e)
        {
            SaveCashDrawerSettings();
        }

        private void SaveCashDrawerSettings()
        {
            try
            {
                string selected = cashDrawerPortComboBox.Text;
                if (selected == "No ports found" || selected == "Error loading ports")
                    return;

                _settings.CashDrawer.SerialPort = selected;
                _settings.Save();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving cash drawer settings: {ex.Message}");
            }
        }

        #endregion

        private async void kickDrawerButton_Click(object sender, EventArgs e)
        {
            string portName = _settings.CashDrawer.SerialPort;

            if (string.IsNullOrWhiteSpace(portName))
            {
                MessageBox.Show("No cash drawer serial port configured.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            kickDrawerButton.Enabled = false;

            try
            {
                await Task.Run(() => SendDrawerKick(portName)).WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to kick drawer: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                kickDrawerButton.Enabled = true;
            }
        }

        private static void SendDrawerKick(string portName)
        {
            byte[] kickCommand = [0x1B, 0x70, 0x00, 0x19, 0xFA];

            using SerialPort port = new(portName, 9600)
            {
                WriteTimeout = 1000,
                ReadTimeout = 1000
            };
            port.Open();
            port.Write(kickCommand, 0, kickCommand.Length);
        }
    }
}
