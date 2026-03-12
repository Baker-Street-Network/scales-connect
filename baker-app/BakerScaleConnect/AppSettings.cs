using System.Text.Json;

namespace BakerScaleConnect
{
    /// <summary>
    /// Simple settings manager for application configuration.
    /// Stores settings in a JSON file in the user's AppData folder.
    /// </summary>
    public class AppSettings
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BakerScaleConnect",
            "settings.json"
        );

        public PaxTerminalConfig PaxTerminal { get; set; } = new();

        public class PaxTerminalConfig
        {
            public string ConnectionMethod { get; set; } = "TCP";
            public string IpAddress { get; set; } = "127.0.0.1";
            public int Port { get; set; } = 10009;
            public int Timeout { get; set; } = 60000;
            public string SerialPort { get; set; } = "";
        }

        /// <summary>
        /// Load settings from file, or return defaults if file doesn't exist.
        /// </summary>
        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    return settings ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                // Log error but continue with defaults
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            }

            return new AppSettings();
        }

        /// <summary>
        /// Save current settings to file.
        /// </summary>
        public void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(SettingsFilePath)!;
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save settings: {ex.Message}", ex);
            }
        }
    }
}
