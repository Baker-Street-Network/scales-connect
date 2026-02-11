using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BakerScaleConnect
{
    public partial class Form1 : Form
    {
        private NotifyIcon? _notifyIcon;
        private ContextMenuStrip? _contextMenu;
        private readonly IHost _host;
        private readonly ScannerManager _scannerManager;
        private System.Windows.Forms.Timer? _retryTimer;
        private int _retryCount;
        private const int RETRY_INTERVAL_MS = 5000; // 5 seconds between retries

        public Form1(IHost host)
        {
            InitializeComponent();
            _host = host;
            _scannerManager = host.Services.GetRequiredService<ScannerManager>();
            SetupSystemTray();
            SetupForm();
            WireButtonEvents();

            // Discover scanners immediately on startup
            this.Load += Form1_Load;
        }

        private void WireButtonEvents()
        {
            button1.Click += BtnSetSnapi_Click;
            button2.Click += BtnSetEmulation_Click;
            comboVolume.SelectedIndexChanged += ComboVolume_SelectedIndexChanged;
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
    }
}
