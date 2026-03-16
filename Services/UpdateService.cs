using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace BakerScaleConnect.Services
{
    public class UpdateService : BackgroundService
    {
        private readonly ILogger<UpdateService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(4);
        private UpdateManager? _updateManager;

        public UpdateService(ILogger<UpdateService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait 30 seconds after startup before first check
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckForUpdatesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking for updates");
                }

                // Wait before next check
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                _logger.LogInformation("Checking for updates...");

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
                    _logger.LogInformation($"New version available: {newVersion.TargetFullRelease.Version}");

                    // Download the update
                    _logger.LogInformation("Downloading update...");
                    await _updateManager.DownloadUpdatesAsync(newVersion);

                    _logger.LogInformation("Update downloaded successfully. Will apply on next restart.");
                    
                    // Apply updates and restart
                    // Note: You might want to prompt the user first in a production app
                    // For now, we'll just apply it silently on next manual restart
                    // Uncomment the line below to auto-restart:
                    // _updateManager.ApplyUpdatesAndRestart(newVersion);
                }
                else
                {
                    _logger.LogInformation("No updates available. Already on latest version.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check for updates");
            }
        }

        public override void Dispose()
        {
            // UpdateManager doesn't implement IDisposable, nothing to dispose
            base.Dispose();
        }
    }
}
