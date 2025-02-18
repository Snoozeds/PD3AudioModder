using Newtonsoft.Json.Linq;
using PD3AudioModder;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

public class PackFiles
{
    private readonly static string LocalizedMappings = "https://raw.githubusercontent.com/Snoozeds/PD3WwiseMappings/refs/heads/main/localized.json";
    private readonly static string MediaMappings = "https://raw.githubusercontent.com/Snoozeds/PD3WwiseMappings/refs/heads/main/media.json";

    private readonly static string LocalizedPath = "PAYDAY3/Content/WwiseAudio/Localized/English_US_/Media";
    private readonly static string MediaPath = "PAYDAY3/Content/WwiseAudio/Media";

    private static string AppDataPath()
    {
        string appDataPath;

        if (OperatingSystem.IsWindows())
        {
            appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PD3AudioModder", "Mappings");
        }
        else if (OperatingSystem.IsLinux())
        {
            appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "PD3AudioModder", "Mappings");
        }
        else
        {
            throw new PlatformNotSupportedException("Unsupported operating system.");
        }

        CreateDirectories(appDataPath);
        return appDataPath;
    }

    private static void CreateDirectories(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    public static async Task DownloadMappings()
    {
        using var client = new HttpClient();
        try
        {
            // Localized mappings
            HttpResponseMessage response = await client.GetAsync(LocalizedMappings);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Failed to download localized mappings.");
            }

            string localizedJson = await response.Content.ReadAsStringAsync();
            string localizedFilePath = Path.Combine(AppDataPath(), "localized.json");

            // Check if the content is different before writing
            if (File.Exists(localizedFilePath))
            {
                string currentLocalizedJson = await File.ReadAllTextAsync(localizedFilePath);
                if (localizedJson != currentLocalizedJson)
                {
                    await File.WriteAllTextAsync(localizedFilePath, localizedJson);
                }
            }
            else
            {
                // If the file doesn't exist, write the new content
                await File.WriteAllTextAsync(localizedFilePath, localizedJson);
            }

            // Media mappings
            response = await client.GetAsync(MediaMappings);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Failed to download media mappings.");
            }

            string mediaJson = await response.Content.ReadAsStringAsync();
            string mediaFilePath = Path.Combine(AppDataPath(), "media.json");

            // Check if the content is different before writing
            if (File.Exists(mediaFilePath))
            {
                string currentMediaJson = await File.ReadAllTextAsync(mediaFilePath);
                if (mediaJson != currentMediaJson)
                {
                    await File.WriteAllTextAsync(mediaFilePath, mediaJson);
                }
            }
            else
            {
                // If the file doesn't exist, write the new content
                await File.WriteAllTextAsync(mediaFilePath, mediaJson);
            }
        }
        catch (Exception ex)
        {
            throw new Exception("Unexpected error: " + ex.Message);
        }
    }

    public static void Pack(string repakPath, bool compression, string packFolderPath, string folderPath, string modName)
    {
        try
        {
            // Create the mod folder if it doesn't exist
            string modFolderPath = Path.Combine(folderPath, modName);
            Directory.CreateDirectory(modFolderPath);

            // Create both Media and Localized paths
            string localizedFullPath = Path.Combine(modFolderPath, LocalizedPath);
            string mediaFullPath = Path.Combine(modFolderPath, MediaPath);
            Directory.CreateDirectory(localizedFullPath);
            Directory.CreateDirectory(mediaFullPath);

            // Read and parse JSON files
            string localizedJson = File.ReadAllText(Path.Combine(AppDataPath(), "localized.json"));
            string mediaJson = File.ReadAllText(Path.Combine(AppDataPath(), "media.json"));

            JToken localizedMappings = JToken.Parse(localizedJson);
            JToken mediaMappings = JToken.Parse(mediaJson);

            // Iterate through all files in packFolderPath
            var files = Directory.GetFiles(packFolderPath, "*", SearchOption.AllDirectories);
            foreach (var filePath in files)
            {
                string fileName = Path.GetFileName(filePath);
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);

                // Skip non uassset, json, ubulk, and uexp files
                if (!fileName.EndsWith(".uasset") && !fileName.EndsWith(".json") && !fileName.EndsWith(".ubulk") && !fileName.EndsWith(".uexp"))
                {
                    continue;
                }

                bool fileProcessed = false;

                // Check if file (without extension) matches localized mapping
                if (localizedMappings is JArray localizedArray &&
                    localizedArray.Any(token => token.ToString().Equals(fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase)))
                {
                    string destinationPath = Path.Combine(localizedFullPath, fileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? throw new Exception("Invalid destination path."));
                    File.Copy(filePath, destinationPath, true);
                    fileProcessed = true;
                    Console.WriteLine($"Copied {fileName} to Localized folder.");
                }

                // Check if file (without extension) matches media mapping
                if (!fileProcessed)
                {
                    if (mediaMappings is JArray mediaArray &&
                        mediaArray.Any(token => token.ToString().Equals(fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase)))
                    {
                        string destinationPath = Path.Combine(mediaFullPath, fileName);
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? throw new Exception("Invalid destination path."));
                        File.Copy(filePath, destinationPath, true);
                        fileProcessed = true;
                        Console.WriteLine($"Copied {fileName} to Media folder.");
                    }
                }

                if (!fileProcessed)
                {
                    var warningDialog = new WarningDialog($"File {fileName} does not match any known mappings.\nThere may have been an update to the game recently,\nand the mappings have not been updated yet.");
                    warningDialog.Show();
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error processing files: {ex.Message}");
        }
    }

    public static async Task Repak(string repakPath, bool compression, string folderPath, string modName)
    {
        try
        {
            string modFolderPath = Path.Combine(folderPath, modName);
            string repakArguments = $"pack \"{modFolderPath}\"";

            if (compression)
            {
                repakArguments += " -compression Zlib";
            }

            await Task.Run(() =>
            {
                using var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "repak",
                        Arguments = repakArguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit();
            });
        }

        catch (Exception ex)
        {
            throw new Exception($"Error running repak: {ex.Message}");
        }

        finally
        {
            Directory.Delete(Path.Combine(folderPath, modName), true);
        }
    }
}