using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MonBand.Windows.Settings
{
    public class AppSettings
    {
        public static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            nameof(MonBand),
            "settings.json");

        public IList<SnmpPollerConfig> SnmpPollers { get; set; }
        public IList<PerformanceCounterPollerConfig> PerformanceCounterPollers { get; set; }

        public AppSettings()
        {
            this.SnmpPollers = new List<SnmpPollerConfig>();
            this.PerformanceCounterPollers = new List<PerformanceCounterPollerConfig>();
        }

        public void Save()
        {
            var settingsDirectoryPath = Path.GetDirectoryName(SettingsFilePath);
            if (settingsDirectoryPath != null && !Directory.Exists(settingsDirectoryPath))
            {
                Directory.CreateDirectory(settingsDirectoryPath);
            }

            using var stream = File.OpenWrite(SettingsFilePath);
            using var jsonWriter = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            JsonSerializer.Serialize(jsonWriter, this, this.GetType());
        }

        public static AppSettings Load()
        {
            if (!File.Exists(SettingsFilePath))
            {
                var settings = new AppSettings();
                settings.Save();
                return settings;
            }

            var fileBytes = File.ReadAllBytes(SettingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(fileBytes);
        }

        public static string GetLogFilePath(string fileNameSuffix)
        {
            if (string.IsNullOrWhiteSpace(fileNameSuffix))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(fileNameSuffix));
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                nameof(MonBand),
                "Logs",
                $"Application-{fileNameSuffix}.log");
        }
    }
}
