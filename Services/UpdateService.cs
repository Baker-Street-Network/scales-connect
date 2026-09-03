using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace BakerScaleConnect.Services
{
    /// <summary>
    /// What the updater is currently doing. Surfaced on the main form so the machine
    /// can be left unattended and still be seen to be keeping itself current.
    /// </summary>
    public enum UpdatePhase
    {
        /// <summary>Counting down to the next check. <see cref="UpdateService.NextCheckUtc"/> is set.</summary>
        Waiting,
        Checking,
        Downloading,
        Disabled
    }

    public class UpdateService : BackgroundService
    {
        private readonly ILogger<UpdateService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(4);
        private readonly TimeSpan _startupDelay = TimeSpan.FromSeconds(30);
        private UpdateManager? _updateManager;
        private bool _updatesUnavailable;

        public UpdateService(ILogger<UpdateService> logger)
        {
            _logger = logger;
        }

        /// <summary>Raised whenever the phase or the next-check time changes. Fired on
        /// a background thread — handlers must marshal to the UI thread themselves.</summary>
        public event EventHandler? StatusChanged;

        public UpdatePhase Phase { get; private set; } = UpdatePhase.Waiting;

        /// <summary>When the next check is due, or null when no check is scheduled.</summary>
        public DateTimeOffset? NextCheckUtc { get; private set; }

        /// <summary>Outcome of the last completed check, e.g. "Up to date" — null before
        /// the first one finishes.</summary>
        public string? LastResult { get; private set; }

        private void SetStatus(UpdatePhase phase, DateTimeOffset? nextCheckUtc, string? lastResult = null)
        {
            Phase = phase;
            NextCheckUtc = nextCheckUtc;
            if (lastResult != null) LastResult = lastResult;

            StatusChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Only the instance holding the single-instance mutex may update. A
            // duplicate copy exits in Program.Main long before it reaches here, but
            // N copies each calling ApplyUpdatesAndRestart on their own 4-hour timer
            // is exactly how a restart storm starts, so refuse to run without it.
            if (!SingleInstance.IsOwner)
            {
                _logger.LogWarning(
                    "Not the primary instance — auto-updates are disabled in this process.");
                SetStatus(UpdatePhase.Disabled, null, "Auto-updates disabled (another copy is running)");
                return;
            }

            // Wait 30 seconds after startup before first check
            SetStatus(UpdatePhase.Waiting, DateTimeOffset.UtcNow + _startupDelay);
            await Task.Delay(_startupDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckForUpdatesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking for updates");
                    SetStatus(UpdatePhase.Waiting, DateTimeOffset.UtcNow + _checkInterval, "Check failed");
                }

                // Nothing about a non-installed build changes while it runs, so stop
                // rather than failing the same way every four hours forever.
                if (_updatesUnavailable)
                    return;

                // Wait before next check
                SetStatus(UpdatePhase.Waiting, DateTimeOffset.UtcNow + _checkInterval);
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                _logger.LogInformation("Checking for updates...");
                SetStatus(UpdatePhase.Checking, null);

                // Initialize update manager if not already done
                if (_updateManager == null)
                {
                    var source = new GithubSource(
                        "https://github.com/Baker-Street-Network/scales-connect",
                        null,
                        false
                    );
                    _updateManager = new UpdateManager(source);
                }

                // Check for updates
                var newVersion = await _updateManager.CheckForUpdatesAsync();

                if (newVersion != null)
                {
                    var version = newVersion.TargetFullRelease.Version;
                    _logger.LogInformation($"New version available: {version}");

                    // Download the update
                    _logger.LogInformation("Downloading update...");
                    SetStatus(UpdatePhase.Downloading, null, $"Downloading {version}");
                    await _updateManager.DownloadUpdatesAsync(newVersion);

                    _logger.LogInformation("Update downloaded successfully.");

                    // Re-check ownership immediately before restarting: the download
                    // can take minutes, and this is the one call that kills the process.
                    if (!SingleInstance.IsOwner)
                    {
                        _logger.LogWarning(
                            "No longer the primary instance — skipping restart.");
                        SetStatus(UpdatePhase.Disabled, null, "Restart skipped (another copy is running)");
                        return;
                    }

                    // Apply updates and restart. Note: you might want to prompt the
                    // user first in a production app.
                    SetStatus(UpdatePhase.Downloading, null, $"Restarting into {version}");
                    _updateManager.ApplyUpdatesAndRestart(newVersion);
                }
                else
                {
                    _logger.LogInformation("No updates available. Already on latest version.");
                    SetStatus(UpdatePhase.Waiting, null, "Up to date");
                }
            }
            catch (Velopack.Exceptions.NotInstalledException)
            {
                // Running from plain build output rather than a Velopack install — a
                // dev build, or a hand-copied folder. This can never succeed, and it
                // is not a fault worth showing anyone a recurring error over.
                _logger.LogInformation(
                    "Not a Velopack install — auto-updates are unavailable in this copy.");
                _updatesUnavailable = true;
                SetStatus(UpdatePhase.Disabled, null, "Auto-updates unavailable (not an installed build)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check for updates");
                SetStatus(UpdatePhase.Waiting, null, "Check failed");
            }
        }

        public override void Dispose()
        {
            // UpdateManager doesn't implement IDisposable, nothing to dispose
            base.Dispose();
        }
    }
}
