using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Threading;
using Newtonsoft.Json.Linq;
using PD3AudioModder;

public class PackFiles
{
    private static readonly string LocalizedPath =
        "PAYDAY3/Content/WwiseAudio/Localized/English_US_/Media";
    private static readonly string MediaPath = "PAYDAY3/Content/WwiseAudio/Media";

    private static MainWindow? _mainWindow;

    public static void Initialize(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
    }

    public static void UpdateStatus(string message)
    {
        if (_mainWindow?.globalStatusTextBlock != null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _mainWindow.globalStatusTextBlock.Text = message;
            });
        }
    }

    private static string AppDataPath()
    {
        string appDataPath;

        if (OperatingSystem.IsWindows())
        {
            appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PD3AudioModder",
                "Mappings"
            );
        }
        else if (OperatingSystem.IsLinux())
        {
            appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config",
                "PD3AudioModder",
                "Mappings"
            );
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
        string LocalizedMappings = AppConfig.Instance.WwiseLocalizedMappingsURL;
        string MediaMappings = AppConfig.Instance.WwiseMediaMappingsURL;

        // Revert to default if empty
        if (string.IsNullOrWhiteSpace(LocalizedMappings))
        {
            LocalizedMappings = DefaultConfig.WwiseLocalizedMappingsURl;
        }

        if (string.IsNullOrWhiteSpace(MediaMappings))
        {
            MediaMappings = DefaultConfig.WwiseMediaMappingsURL;
        }

        UpdateStatus("Downloading ID mappings...");
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
            UpdateStatus("Mapping files downloaded successfully.");
        }
        catch (Exception ex)
        {
            throw new Exception("Unexpected error: " + ex.Message);
        }
    }

    public static bool Pack(
        string repakPath,
        bool compression,
        string packFolderPath,
        string folderPath,
        string modName,
        bool autoSort
    )
    {
        try
        {
            UpdateStatus("Starting to pack files...");

            // Create the mod folder if it doesn't exist
            string modFolderPath = Path.Combine(folderPath, modName);
            Directory.CreateDirectory(modFolderPath);

            if (!autoSort)
            {
                // Just copy all files from packFolderPath to modFolderPath as-is
                UpdateStatus("Packing without sorting...");
                CopyAllFiles(packFolderPath, modFolderPath);
                UpdateStatus("Files packed without sorting.");
                return true;
            }

            // Create both Media and Localized paths
            UpdateStatus("Creating mod folder structure...");
            string localizedFullPath = Path.Combine(modFolderPath, LocalizedPath);
            string mediaFullPath = Path.Combine(modFolderPath, MediaPath);
            Directory.CreateDirectory(localizedFullPath);
            Directory.CreateDirectory(mediaFullPath);

            // Read and parse JSON files
            string localizedJson = File.ReadAllText(Path.Combine(AppDataPath(), "localized.json"));
            string mediaJson = File.ReadAllText(Path.Combine(AppDataPath(), "media.json"));

            JToken localizedMappings = JToken.Parse(localizedJson);
            JToken mediaMappings = JToken.Parse(mediaJson);

            // Used to display a warning when files do not exist in mappings
            List<string> unmatchedFiles = new List<string>();

            // Iterate through all files in packFolderPath
            var files = Directory.GetFiles(packFolderPath, "*", SearchOption.AllDirectories);
            foreach (var filePath in files)
            {
                string fileName = Path.GetFileName(filePath);
                UpdateStatus($"Processing file: {fileName}");
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);

                // Skip non uassset, json, ubulk, and uexp files
                if (
                    !fileName.EndsWith(".uasset")
                    && !fileName.EndsWith(".json")
                    && !fileName.EndsWith(".ubulk")
                    && !fileName.EndsWith(".uexp")
                )
                {
                    continue;
                }

                bool fileProcessed = false;

                // Check if file (without extension) matches localized mapping
                if (
                    localizedMappings is JArray localizedArray
                    && localizedArray.Any(token =>
                        token
                            .ToString()
                            .Equals(fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase)
                    )
                )
                {
                    string destinationPath = Path.Combine(localizedFullPath, fileName);
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(destinationPath)
                            ?? throw new Exception("Invalid destination path.")
                    );
                    File.Copy(filePath, destinationPath, true);
                    fileProcessed = true;
                    Console.WriteLine($"Copied {fileName} to Localized folder.");
                }

                // Check if file (without extension) matches media mapping
                if (!fileProcessed)
                {
                    if (
                        mediaMappings is JArray mediaArray
                        && mediaArray.Any(token =>
                            token
                                .ToString()
                                .Equals(
                                    fileNameWithoutExtension,
                                    StringComparison.OrdinalIgnoreCase
                                )
                        )
                    )
                    {
                        string destinationPath = Path.Combine(mediaFullPath, fileName);
                        Directory.CreateDirectory(
                            Path.GetDirectoryName(destinationPath)
                                ?? throw new Exception("Invalid destination path.")
                        );
                        File.Copy(filePath, destinationPath, true);
                        fileProcessed = true;
                        Console.WriteLine($"Copied {fileName} to Media folder.");
                    }
                }

                if (!fileProcessed)
                {
                    unmatchedFiles.Add(fileName);
                    Console.WriteLine($"File {fileName} does not match any known mappings.");
                }
            }

            if (unmatchedFiles.Count > 0)
            {
                string warningMessage =
                    $"{unmatchedFiles.Count} file(s) do not match any known mappings:\n"
                    + string.Join("\n", unmatchedFiles.Take(10))
                    + (unmatchedFiles.Count > 10 ? "\n(and more...)" : "");

                warningMessage +=
                    "\n\nThere may have been an update to the game recently,\nand the mappings have not been updated yet.\nPlease make sure to only use this tab for audio files, too.";

                var warningDialog = new WarningDialog(warningMessage);
                warningDialog.Show();
                return false;
            }

            UpdateStatus("Files packed successfully.");
            return true;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error processing files: {ex.Message}");
        }
    }

    private static void CopyAllFiles(string sourceDir, string targetDir)
    {
        foreach (var filePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDir, filePath);
            string targetFilePath = Path.Combine(targetDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);
            File.Copy(filePath, targetFilePath, true);
        }
    }

    public static async Task Repak(
        string repakPath,
        bool compression,
        string folderPath,
        string modName
    )
    {
        try
        {
            UpdateStatus("Starting repak...");
            string modFolderPath = Path.Combine(folderPath, modName);
            string repakArguments = $"pack \"{modFolderPath}\"";

            if (compression)
            {
                repakArguments += " -compression Zlib";
                UpdateStatus("Using Zlib compression...");
            }

            await Task.Run(() =>
            {
                UpdateStatus("Running repak command...");
                using var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = repakPath,
                        Arguments = repakArguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = false,
                    },
                };

                process.Start();
                process.WaitForExit();
            });
            UpdateStatus("Repak process completed successfully.");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error during repak process: {ex.Message}");
            throw;
        }
        finally
        {
            UpdateStatus("Cleaning up temporary files...");
            Directory.Delete(Path.Combine(folderPath, modName), true);
            UpdateStatus($"{modName} packing process complete.");
        }
    }
}
