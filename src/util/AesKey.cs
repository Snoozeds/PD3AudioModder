using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace PD3AudioModder.util
{
    internal class AesKey
    {
        // Save provided aes key so user doesn't have to re-enter it each time in ID Search tab.
        public static void SaveAesKey(string aesKey)
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData
                );
                string folderPath = Path.Combine(appDataPath, "PD3AudioModder");
                string filePath = Path.Combine(folderPath, "aes.json");

                // Create folder if it doesn't exist
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                var keyData = new { AesKey = aesKey };

                string json = JsonConvert.SerializeObject(keyData, Formatting.Indented);
                File.WriteAllText(filePath, json);

                Console.WriteLine($"AES key saved to: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving AES key: {ex.Message}");
            }
        }

        public static string? GetAesKey()
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData
                );
                string filePath = Path.Combine(appDataPath, "UnrealLocresEditor", "aes.json");

                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var keyData = JsonConvert.DeserializeObject<AesKeyData>(json);
                    return keyData?.AesKey;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading AES key: {ex.Message}");
            }

            return null;
        }

        private class AesKeyData
        {
            public string? AesKey { get; set; }
        }
    }
}
