using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BakerScaleConnect.Services
{
    /// <summary>
    /// Background service that hosts the web API server.
    /// </summary>
    public class WebServerHostedService(
        ILogger<WebServerHostedService> logger,
        IServiceProvider serviceProvider,
        IHostApplicationLifetime lifetime) : BackgroundService
    {
        private const string ListenUrl = "http://localhost:5000";

        // Velopack restarts the app by launching the new copy while the old one is
        // still releasing the port, so the first bind failure is usually just handoff.
        private const int MaxBindAttempts = 3;
        private static readonly TimeSpan BindRetryDelay = TimeSpan.FromSeconds(3);

        private IWebHost? _webHost;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                if (!await StartWebHostAsync(stoppingToken))
                {
                    // The port is held by something else, so this process can never do
                    // its job. Stop the host rather than lingering as a zombie — the
                    // form shuts the UI down when the host stops (see Program.Main).
                    logger.LogCritical(
                        "Web server could not bind {Url} after {Attempts} attempts. " +
                        "Another process is holding the port. Shutting down.",
                        ListenUrl, MaxBindAttempts);

                    lifetime.StopApplication();
                    return;
                }

                // Keep the service running
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Web server is stopping");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in web server");
            }
            finally
            {
                await DisposeWebHostAsync();
            }
        }

        /// <summary>
        /// Builds and starts the web host, retrying a transient bind failure a few
        /// times. Returns false once the port is confirmed unavailable.
        /// </summary>
        private async Task<bool> StartWebHostAsync(CancellationToken stoppingToken)
        {
            for (int attempt = 1; attempt <= MaxBindAttempts; attempt++)
            {
                try
                {
                    logger.LogInformation("Starting web server on {Url}", ListenUrl);

                    _webHost = BuildWebHost();
                    await _webHost.StartAsync(stoppingToken);

                    logger.LogInformation("Web server started successfully");
                    return true;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (IOException ex) when (attempt < MaxBindAttempts)
                {
                    // Kestrel reports "Failed to bind to address" as an IOException.
                    logger.LogWarning(ex,
                        "Web server bind attempt {Attempt}/{Max} failed. Retrying in {Delay}s.",
                        attempt, MaxBindAttempts, BindRetryDelay.TotalSeconds);

                    await DisposeWebHostAsync();
                    await Task.Delay(BindRetryDelay, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Web server failed to start on {Url}", ListenUrl);
                    await DisposeWebHostAsync();
                    return false;
                }
            }

            return false;
        }

        private IWebHost BuildWebHost() =>
            new WebHostBuilder()
                .UseKestrel()
                .UseUrls(ListenUrl)
                .ConfigureServices(services =>
                {
                    services.AddCors(options =>
                    {
                        options.AddPolicy("AllowAnyOrigin",
                            builder => builder.AllowAnyOrigin()
                                              .AllowAnyHeader()
                                              .AllowAnyMethod());
                    });
                    services.AddControllers();
                    services.AddSingleton(serviceProvider.GetRequiredService<ScannerManager>());
                    services.AddSingleton(serviceProvider.GetRequiredService<ScaleWeightCache>());
                    services.AddSingleton(serviceProvider.GetRequiredService<PaxService>());
                    services.AddSingleton(serviceProvider.GetRequiredService<PhoneCollectService>());
                    services.AddSingleton(serviceProvider.GetRequiredService<AppSettings>());
                    services.AddScoped<ConnectivityService>();
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseCors("AllowAnyOrigin");
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapControllers();
                    });
                })
                .Build();

        private async Task DisposeWebHostAsync()
        {
            if (_webHost == null) return;

            try
            {
                await _webHost.StopAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error stopping web server");
            }
            finally
            {
                _webHost.Dispose();
                _webHost = null;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopping web server");
            if (_webHost != null)
                await _webHost.StopAsync(cancellationToken);
            await base.StopAsync(cancellationToken);
        }
    }
}
