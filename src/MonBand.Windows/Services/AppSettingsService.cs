using System;
using System.IO;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MonBand.Windows.Services
{
    public class AppSettingsService : IAppSettingsService
    {
        public static readonly string SettingsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            nameof(MonBand));

        static readonly JsonSerializerOptions s_DefaultJsonSerializerOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new JsonStringEnumConverter() }
        };

        public void Save<TSettings>(TSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (!Directory.Exists(SettingsRoot))
            {
                Directory.CreateDirectory(SettingsRoot);
            }

            var settingsFilePath = GetSettingsFilePath(typeof(TSettings));
            using var stream = File.Open(settingsFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var jsonWriter = new Utf8JsonWriter(
                stream,
                new JsonWriterOptions { Indented = true, Encoder = s_DefaultJsonSerializerOptions.Encoder });
            JsonSerializer.Serialize(
                jsonWriter,
                settings,
                typeof(TSettings),
                s_DefaultJsonSerializerOptions);
        }

        public TSettings LoadOrCreate<TSettings>() where TSettings : new()
        {
            var settingsFilePath = GetSettingsFilePath(typeof(TSettings));

            if (!File.Exists(settingsFilePath))
            {
                var settings = new TSettings();
                this.Save(settings);
                return settings;
            }

            var settingsFileInfo = new FileInfo(settingsFilePath);
            if (settingsFileInfo.Length > 1 * 1024 * 1024)
            {
                throw new InvalidOperationException("Refusing to load settings files larger than 1 MB in size.");
            }

            try
            {
                var fileBytes = File.ReadAllBytes(settingsFilePath);
                return JsonSerializer.Deserialize<TSettings>(fileBytes, s_DefaultJsonSerializerOptions);
            }
            catch
            {
                var settings = new TSettings();
                this.Save(settings);
                return settings;
            }
        }

        static string GetSettingsFilePath(MemberInfo type)
        {
            var name = type.Name;
            name = name[..1].ToLower() + name[1..];

            if (name.EndsWith("ViewModel"))
            {
                name = name[..^"ViewModel".Length];
            }

            if (name.EndsWith("Model"))
            {
                name = name[..^"Model".Length];
            }

            var fileName = name + ".json";
            return Path.Combine(SettingsRoot, fileName);
        }

        public string GetLogFilePath(string applicationName)
        {
            if (string.IsNullOrWhiteSpace(applicationName))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(applicationName));
            }

            return Path.Combine(
                SettingsRoot,
                "Logs",
                $"{applicationName}.log");
        }
    }
}
