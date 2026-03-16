using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;
using System.ServiceProcess;

namespace BakerScaleConnect.Services
{
    /// <summary>
    /// Ensures the Baker Street Watchdog Windows Service stays running.
    /// Mirrors the watchdog's own role in reverse: if the watchdog is stopped,
    /// this service attempts to restart it so the two programs keep each other alive.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class WatchdogMonitorService(ILogger<WatchdogMonitorService> logger) : BackgroundService
    {
        private const string WatchdogServiceName = "BakerStreetWatchdog";
        private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Give the system time to fully settle before the first check.
            await Task.Delay(StartupDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                TryEnsureWatchdogRunning();
                await Task.Delay(PollInterval, stoppingToken);
            }
        }

        private void TryEnsureWatchdogRunning()
        {
            try
            {
                using var svc = new ServiceController(WatchdogServiceName);

                var status = svc.Status;

                if (status == ServiceControllerStatus.Running ||
                    status == ServiceControllerStatus.StartPending)
                {
                    return; // All good.
                }

                logger.LogWarning(
                    "Watchdog service '{Name}' is in state '{Status}'. Attempting to start it.",
                    WatchdogServiceName, status);

                svc.Start();
                svc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));

                logger.LogInformation("Watchdog service '{Name}' started successfully.", WatchdogServiceName);
            }
            catch (InvalidOperationException)
            {
                // Service does not exist on this machine — watchdog is not installed.
                // Log once at debug level to avoid log spam on machines without the watchdog.
                logger.LogDebug(
                    "Watchdog service '{Name}' not found. " +
                    "Install BakerStreetWatchdog to enable mutual monitoring.",
                    WatchdogServiceName);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not start watchdog service '{Name}'.", WatchdogServiceName);
            }
        }
    }
}
