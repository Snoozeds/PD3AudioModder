using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace PD3AudioModder
{
    public static class DefaultConfig
    {
        public static readonly bool AutoUpdateEnabled = true;
        public static readonly bool AskToUpdate = true;
        public static readonly string? RepakPath = null;
        public static readonly string? FfmpegOptions = "-acodec pcm_s16le -ar 48000 -ac 2";
        public static readonly string? FfmpegPath = null;
        public static readonly string? DefaultExportFolder = null;
        public static readonly bool UseDefaultExportFolder = false;
        public static readonly bool MuteNotificationSound = false;
        public static readonly bool RPCEnabled = false;
        public static readonly bool RPCDisplayTab = false;
        public static readonly bool RPCDisplayModName = false;
    }

    public class AppConfig
    {
        private static AppConfig? _instance;
        private static readonly object _lock = new object();
        public bool AutoUpdateEnabled { get; set; } = DefaultConfig.AutoUpdateEnabled;
        public bool? AskToUpdate { get; set; } = DefaultConfig.AskToUpdate;
        public string? RepakPath { get; set; } = DefaultConfig.RepakPath;
        public string? FfmpegOptions { get; set; } = DefaultConfig.FfmpegOptions;
        public string? FfmpegPath { get; set; } = DefaultConfig.FfmpegPath;
        public string? DefaultExportFolder { get; set; }
        public bool UseDefaultExportFolder { get; set; }
        public bool MuteNotificationSound { get; set; }
        public bool RPCEnabled { get; set; }
        public bool RPCDisplayTab { get; set; }
        public bool RPCDisplayModName { get; set; }

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
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "PD3AudioModder"
                );
            }
            else if (OperatingSystem.IsLinux())
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config",
                    "PD3AudioModder"
                );
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
                {
                    "AutoUpdateEnabled",
                    config => config.AutoUpdateEnabled == true || config.AutoUpdateEnabled == false
                },
                {
                    "AskToUpdate",
                    config => config.AskToUpdate == true || config.AskToUpdate == false
                },
                {
                    "RepakPath",
                    config =>
                        string.IsNullOrEmpty(config.RepakPath) || File.Exists(config.RepakPath)
                },
                { "FfmpegOptions", config => !string.IsNullOrWhiteSpace(config.FfmpegOptions) },
                {
                    "FfmpegPath",
                    config =>
                        string.IsNullOrEmpty(config.FfmpegPath) || File.Exists(config.FfmpegPath)
                },
                {
                    "DefaultExportFolder",
                    config =>
                        string.IsNullOrEmpty(config.DefaultExportFolder)
                        || Directory.Exists(config.DefaultExportFolder)
                },
                {
                    "UseDefaultExportFolder",
                    config =>
                        config.UseDefaultExportFolder == true
                        || config.UseDefaultExportFolder == false
                },
                {
                    "MuteNotificationSound",
                    config =>
                        config.MuteNotificationSound == true
                        || config.MuteNotificationSound == false
                },
                { "RPCEnabled", config => config.RPCEnabled == true || config.RPCEnabled == false },
                {
                    "RPCDisplayTab",
                    config => config.RPCDisplayTab == true || config.RPCDisplayTab == false
                },
                {
                    "RPCDisplayModName",
                    config => config.RPCDisplayModName == true || config.RPCDisplayModName == false
                },
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
                                    property.SetValue(
                                        config,
                                        typeof(DefaultConfig).GetProperty(rule.Key)?.GetValue(null)
                                    );
                                }
                            }
                        }

                        return config;
                    }
                }
            }
            catch (Exception) { }

            return new AppConfig();
        }

        public void Save()
        {
            try
            {
                Console.WriteLine(
                    $"Saving config: {JsonConvert.SerializeObject(this, Formatting.Indented)}"
                );
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
