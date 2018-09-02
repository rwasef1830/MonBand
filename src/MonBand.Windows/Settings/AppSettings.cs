using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace MonBand.Windows.Settings
{
    public class AppSettings
    {
        public static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            nameof(MonBand),
            "settings.json");

        public static readonly string LogFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            nameof(MonBand),
            "Logs",
            "Application.log");

        public IList<SnmpPollerConfig> SnmpPollers { get; set; }

        public AppSettings()
        {
            this.SnmpPollers = new List<SnmpPollerConfig>();
        }

        public void Save()
        {
            var settingsDirectoryPath = Path.GetDirectoryName(SettingsFilePath);
            if (settingsDirectoryPath != null && !Directory.Exists(settingsDirectoryPath))
            {
                Directory.CreateDirectory(settingsDirectoryPath);
            }

            using (var textWriter = File.CreateText(SettingsFilePath))
            using (var jsonWriter = new JsonTextWriter(textWriter))
            {
                var serializer = new JsonSerializer { Formatting = Formatting.Indented };
                serializer.Serialize(jsonWriter, this);
            }
        }

        public static AppSettings Load()
        {
            if (!File.Exists(SettingsFilePath))
            {
                var settings = new AppSettings();
                settings.Save();
                return settings;
            }

            using (var textReader = File.OpenText(SettingsFilePath))
            using (var jsonReader = new JsonTextReader(textReader))
            {
                var serializer = new JsonSerializer();
                return serializer.Deserialize<AppSettings>(jsonReader);
            }
        }
    }
}
