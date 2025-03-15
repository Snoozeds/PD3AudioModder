using System;
using System.Collections.Generic;
using System.IO;

namespace PD3AudioModder
{
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

        public string GetValue(string section, string key, string defaultValue = "")
        {
            return data.ContainsKey(section) && data[section].ContainsKey(key)
                ? data[section][key]
                : defaultValue;
        }

        public void SetValue(string section, string key, string value)
        {
            if (!data.ContainsKey(section))
                data[section] = new Dictionary<string, string>();

            data[section][key] = value;
        }

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
