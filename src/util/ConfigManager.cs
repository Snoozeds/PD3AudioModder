using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace PD3AudioModder
{
    public static class DefaultConfig
    {
        public static readonly bool AutoUpdateEnabled = true;
        public static readonly string? RepakPath = null;
    }

    public class AppConfig
    {
        private static AppConfig? _instance;
        private static readonly object _lock = new object();
        public bool AutoUpdateEnabled { get; set; } = DefaultConfig.AutoUpdateEnabled;
        public string? RepakPath { get; set; } = DefaultConfig.RepakPath;

        public static AppConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= Load();
                    }
                }
                return _instance;
            }
        }

        private static string GetConfigDirectory()
        {
            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PD3AudioModder");
            }
            else if (OperatingSystem.IsLinux())
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "PD3AudioModder");
            }

            throw new PlatformNotSupportedException("Unsupported OS.");
        }

        private static string GetConfigFilePath()
        {
            string configDirectory = GetConfigDirectory();
            Directory.CreateDirectory(configDirectory);
            return Path.Combine(configDirectory, "config.json");
        }

        private static Dictionary<string, Func<AppConfig, bool>> GetValidationRules()
        {
            return new Dictionary<string, Func<AppConfig, bool>>()
            {
                { "AutoUpdateEnabled", config => config.AutoUpdateEnabled == true || config.AutoUpdateEnabled == false },
                { "RepakPath", config => config.RepakPath == null || File.Exists(config.RepakPath) }
            };
        }

        public static AppConfig Load()
        {
            try
            {
                string filePath = GetConfigFilePath();

                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var config = JsonConvert.DeserializeObject<AppConfig>(json);

                    if (config != null)
                    {
                        var validationRules = GetValidationRules();

                        foreach (var rule in validationRules)
                        {
                            var property = typeof(AppConfig).GetProperty(rule.Key);
                            if (property != null)
                            {
                                var value = property.GetValue(config);
                                if (!rule.Value(config))
                                {
                                    // If validation fails, revert to the default config value
                                    property.SetValue(config, typeof(DefaultConfig).GetProperty(rule.Key)?.GetValue(null));
                                }
                            }
                        }

                        return config;
                    }
                }
            }
            catch (Exception)
            {
            }

            return new AppConfig();
        }

        public void Save()
        {
            try
            {
                Console.WriteLine($"Saving config: {JsonConvert.SerializeObject(this, Formatting.Indented)}");
                string filePath = GetConfigFilePath();
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to save config: {e}");
            }
        }
    }
}
