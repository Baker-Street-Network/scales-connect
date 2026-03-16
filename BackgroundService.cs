using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BakerScaleConnect
{
    public class BakerScaleBackgroundService(ILogger<BakerScaleBackgroundService> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Baker Scale Background Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Add your background work here
                    // For example: monitoring scales, processing data, etc.
                    logger.LogInformation("Background service running at: {time}", DateTimeOffset.Now);
                    
                    // Wait for 30 seconds before next iteration
                    await Task.Delay(30000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Service is being stopped
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error occurred in background service");
                    // Wait a bit before retrying to avoid tight error loops
                    await Task.Delay(5000, stoppingToken);
                }
            }

            logger.LogInformation("Baker Scale Background Service stopped");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Baker Scale Background Service is stopping");
            await base.StopAsync(cancellationToken);
        }
    }
}
