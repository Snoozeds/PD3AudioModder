using System;
using System.Collections.Generic;
using System.IO;

namespace PD3AudioModder
{
    /// <summary>
    /// Parses INI files for theme management.
    /// </summary>
    public class iniParser
    {
        private readonly Dictionary<string, Dictionary<string, string>> data = new();

        public iniParser(string filePath)
        {
            if (File.Exists(filePath))
            {
                Load(filePath);
            }
        }

        /// <summary>
        /// Loads and parses the INI file.
        /// </summary>
        /// <param name="filePath">The path to the INI file.</param>
        public void Load(string filePath)
        {
            data.Clear();
            Dictionary<string, string> currentSection = new();
            string currentSectionName = "Theme";

            foreach (var line in File.ReadAllLines(filePath))
            {
                string trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";"))
                    continue;

                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    currentSectionName = trimmedLine.Trim('[', ']');
                    currentSection = new();
                    data[currentSectionName] = currentSection;
                }
                else if (trimmedLine.Contains("="))
                {
                    var parts = trimmedLine.Split('=', 2);
                    currentSection[parts[0].Trim()] = parts[1].Trim();
                }
            }
        }

        /// <summary>
        /// Gets a value from the INI file.
        /// </summary>
        /// <param name="section">The section name.</param>
        /// <param name="key">The key name.</param>
        /// <param name="defaultValue">The default value if the key is not found.</param>
        /// <returns></returns>
        public string GetValue(string section, string key, string defaultValue = "")
        {
            return data.ContainsKey(section) && data[section].ContainsKey(key)
                ? data[section][key]
                : defaultValue;
        }

        /// <summary>
        /// Sets a value in the INI file.
        /// </summary>
        /// <param name="section">The section name.</param>
        /// <param name="key">The key name.</param>
        /// <param name="value">The value to set.</param>
        public void SetValue(string section, string key, string value)
        {
            if (!data.ContainsKey(section))
                data[section] = new Dictionary<string, string>();

            data[section][key] = value;
        }

        /// <summary>
        /// Saves the INI file to the specified path.
        /// </summary>
        /// <param name="filePath">The path to save the INI file to.</param>
        public void Save(string filePath)
        {
            using var writer = new StreamWriter(filePath);
            foreach (var section in data)
            {
                writer.WriteLine($"[{section.Key}]");
                foreach (var kvp in section.Value)
                {
                    writer.WriteLine($"{kvp.Key}={kvp.Value}");
                }
                writer.WriteLine();
            }
        }
    }
}
