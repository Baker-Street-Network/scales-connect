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
                    
                    // Register scanner manager as singleton
                    services.AddSingleton<ScannerManager>();

                    // Register scale weight cache as singleton (3-second TTL)
                    services.AddSingleton<ScaleWeightCache>();
                    
                    // Register connectivity service
                    services.AddScoped<ConnectivityService>();

                    // Register PAX service
                    services.AddSingleton<PaxService>();

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
