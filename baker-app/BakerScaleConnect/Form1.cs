using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BakerScaleConnect.Services;
using System.IO.Ports;

namespace BakerScaleConnect
{
    public partial class Form1 : Form
    {
        private NotifyIcon? _notifyIcon;
        private ContextMenuStrip? _contextMenu;
        private readonly IHost _host;
        private readonly ScannerManager _scannerManager;
        private readonly PaxService _paxService;
        private System.Windows.Forms.Timer? _retryTimer;
        private int _retryCount;
        private const int RETRY_INTERVAL_MS = 5000; // 5 seconds between retries
        private AppSettings _settings;

        public Form1(IHost host)
        {
            InitializeComponent();
            _host = host;
            _scannerManager = host.Services.GetRequiredService<ScannerManager>();
            _paxService = host.Services.GetRequiredService<PaxService>();

            // Load settings
            _settings = AppSettings.Load();

            SetupSystemTray();
            SetupForm();
            WireButtonEvents();
            LoadPaxSettings();

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
        }

        private void SetupSystemTray()
        {
            // Create context menu for the tray icon
            _contextMenu = new ContextMenuStrip();
            _contextMenu.Items.Add("Show", null, ShowApplication);
            _contextMenu.Items.Add("Hide", null, HideApplication);
            _contextMenu.Items.Add("-"); // Separator
            _contextMenu.Items.Add("Exit", null, ExitApplication);

            // Create the NotifyIcon
            _notifyIcon = new NotifyIcon()
            {
                Icon = SystemIcons.Application, // You can replace this with a custom icon
                ContextMenuStrip = _contextMenu,
                Text = "Baker Scale Connect",
                Visible = true
            };

            // Handle double-click to show/hide the application
            _notifyIcon.DoubleClick += (s, e) => ToggleApplicationVisibility();
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
            // Stop the background service gracefully
            await _host.StopAsync();

            // Clean up the notify icon
            _notifyIcon?.Dispose();

            // Exit the application
            Application.Exit();
        }

        #region PAX Terminal Methods

        /// <summary>
        /// Load PAX terminal settings from file and populate the UI.
        /// </summary>
        private void LoadPaxSettings()
        {
            connectionMethodComboBox.Text = _settings.PaxTerminal.ConnectionMethod;
            terminalIp.Text = _settings.PaxTerminal.IpAddress;
            portNumber.Text = _settings.PaxTerminal.Port.ToString();
            timeoutTextBox.Text = _settings.PaxTerminal.Timeout.ToString();

            // Populate serial ports
            PopulateSerialPorts();

            if (!string.IsNullOrEmpty(_settings.PaxTerminal.SerialPort))
            {
                serialPortComboBox.Text = _settings.PaxTerminal.SerialPort;
            }

            // Show appropriate tab based on connection method
            UpdateTabVisibility();

            // Update the PaxService with loaded settings
            UpdatePaxService();
        }

        /// <summary>
        /// Populate the serial port combo box with available ports.
        /// </summary>
        private void PopulateSerialPorts()
        {
            try
            {
                string currentSelection = serialPortComboBox.Text;
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
                    }
                    else if (serialPortComboBox.Items.Count > 0)
                    {
                        serialPortComboBox.SelectedIndex = 0;
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

                if (int.TryParse(portNumber.Text, out int port))
                {
                    _settings.PaxTerminal.Port = port;
                }

                if (int.TryParse(timeoutTextBox.Text, out int timeout))
                {
                    _settings.PaxTerminal.Timeout = timeout;
                }

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
                // Run the transaction in a background task
                await Task.Run(() =>
                {
                    // Create the payment request
                    var request = new Controllers.Models.PaxCreditRequest
                    {
                        Amount = amount.ToString("F0"),
                        EcrReferenceNumber = $"TEST-{DateTime.Now:yyyyMMddHHmmss}",
                        TransactionType = "Sale"
                    };

                    var response = _paxService.ProcessCreditPayment(request);

                    // Show result on UI thread
                    this.Invoke((MethodInvoker)delegate
                    {
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
                    });
                });
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
                _scannerManager?.Dispose();
                _notifyIcon?.Dispose();
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
    }
}
