using BakerScaleConnect.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Velopack;

namespace BakerScaleConnect
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static async Task Main()
        {
            // Velopack: Handle installation/uninstallation events
            VelopackApp.Build().Run();

            // Exactly one copy may run per machine — the Run key, the watchdog service
            // and Velopack's restart each launch us independently. See SingleInstance.
            if (!SingleInstance.TryAcquire())
                return;

            AddToStartup();

            ApplicationConfiguration.Initialize();

            // Create and configure the host
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Register the background service
                    services.AddHostedService<BakerScaleBackgroundService>();
                    
                    // Register the web server hosted service
                    services.AddHostedService<WebServerHostedService>();

                    // Monitor the watchdog service and restart it if it stops
                    services.AddHostedService<WatchdogMonitorService>();

                    // Self-updater: checks GitHub Releases and applies updates automatically.
                    // Registered as a singleton as well so the form can show its status —
                    // AddHostedService<T> alone only registers it as an IHostedService.
                    services.AddSingleton<UpdateService>();
                    services.AddHostedService(sp => sp.GetRequiredService<UpdateService>());

                    // Register scanner manager as singleton
                    services.AddSingleton<ScannerManager>();

                    // Register scale weight cache as singleton (3-second TTL)
                    services.AddSingleton<ScaleWeightCache>();
                    
                    // Register connectivity service
                    services.AddScoped<ConnectivityService>();

                    // Register PAX service
                    services.AddSingleton<PaxService>();

                    // Register phone collect service (Aries 8 integration)
                    services.AddSingleton(sp => new PhoneCollectService(
                        sp.GetRequiredService<ILogger<PhoneCollectService>>(),
                        AppSettings.Load()));

                    // Register AppSettings as singleton so controllers can access it
                    services.AddSingleton(AppSettings.Load());

                    // Add logging
                    services.AddLogging(builder =>
                    {
                        builder.AddConsole();
                        builder.AddDebug();
                    });
                })
                .Build();

            // Start the background services
            _ = Task.Run(async () => await host.RunAsync());

            // Create and run the Windows Forms application
            using (var serviceScope = host.Services.CreateScope())
            {
                var form = new Form1(host);

                // A hosted service that fails fatally — the web server not being able
                // to bind port 5000, say — stops the host. Take the UI down with it: a
                // copy left running without its web server is a zombie, hidden in the
                // tray, holding the scanner and serial handles, and serving nothing.
                host.Services.GetRequiredService<IHostApplicationLifetime>()
                    .ApplicationStopped.Register(form.ShutdownFromHost);

                // A duplicate launch signals us on its way out. Surface the window so
                // the user sees the running copy instead of nothing happening at all.
                SingleInstance.ListenForShowRequests(form.ShowFromSecondInstance);

                Application.Run(form);
            }

            // Ensure host is disposed
            await host.StopAsync();
            host.Dispose();
        }

        public static void AddToStartup()
        {
            string appName = "BakerScaleConnect";
            string appPath = Environment.ProcessPath ?? Application.ExecutablePath;

            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key is null) return;

            string? existing = key.GetValue(appName) as string;
            if (existing != $"\"{appPath}\"")
            {
                key.SetValue(appName, $"\"{appPath}\"");
            }
        }
    }
}
