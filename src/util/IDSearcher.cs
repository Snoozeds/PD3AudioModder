﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Objects.Core.Misc;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PD3AudioModder.util
{
    public class IDSearcher
    {
        private MainWindow _mainWindow;
        private static AppConfig _appConfig = AppConfig.Load();

        // Command process
        private Process _sharedProcess;
        private bool _isProcessInitialized = false;

        public IDSearcher(MainWindow mainWindow = null)
        {
            _mainWindow = mainWindow;
        }

        // Used for finding vgmstream path
        private static string _cachedVgmstreamPath = null;

        private string GetVgmstreamPath()
        {
            if (_cachedVgmstreamPath != null)
                return _cachedVgmstreamPath;

            var (path, errorMessage) = VgmstreamPathFinder.GetVgmstreamPathWithFallback();

            if (path != null)
            {
                _cachedVgmstreamPath = path;
                return path;
            }

            // If not found, show error and return null
            ShowWarning(errorMessage);
            return null;
        }

        // Reusable command window so Windows Defender doesn't think PAM is a trojan :D
        private void InitializeSharedProcess()
        {
            if (_isProcessInitialized && _sharedProcess != null && !_sharedProcess.HasExited)
                return;

            string vgmstreamPath = GetVgmstreamPath();
            if (string.IsNullOrEmpty(vgmstreamPath))
                return;

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = false,
            };

            _sharedProcess = new Process { StartInfo = psi };

            _sharedProcess.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                    Console.WriteLine(args.Data);
            };
            _sharedProcess.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                    Console.WriteLine("ERROR: " + args.Data);
            };

            _sharedProcess.Start();
            _sharedProcess.BeginOutputReadLine();
            _sharedProcess.BeginErrorReadLine();

            _sharedProcess.StandardInput.WriteLine(
                $"cd /d \"{Path.GetDirectoryName(vgmstreamPath)}\""
            );
            _sharedProcess.StandardInput.Flush();

            _isProcessInitialized = true;
        }

        public void CleanupSharedProcess()
        {
            if (_sharedProcess != null && !_sharedProcess.HasExited)
            {
                _sharedProcess.StandardInput.WriteLine("exit");
                _sharedProcess.WaitForExit(1000);
                _sharedProcess.Dispose();
                _sharedProcess = null;
            }
            _isProcessInitialized = false;
        }

        private static DefaultFileProvider _provider;
        private static string _pakDirectory;
        private WaveOutEvent _outputDevice;
        private AudioFileReader _audioFile;

        public static async Task ProcessPakFiles(
            System.Collections.Generic.IReadOnlyList<Avalonia.Platform.Storage.IStorageFile> files,
            MainWindow _mainWindow,
            string aesKey
        )
        {
            List<SoundItem> soundItems = new List<SoundItem>();
            IDSearcher sharedSearcher = new IDSearcher(_mainWindow);

            try
            {
                _mainWindow.UpdateGlobalStatus("Loading .pak files...", "ID Search");

                // Get directory of first PAK file
                var firstFilePath = files.First().Path.LocalPath;
                _pakDirectory = Path.GetDirectoryName(firstFilePath);

                // Initialize provider if not already done
                if (_provider == null)
                {
                    Console.WriteLine($"Initializing provider for directory: {_pakDirectory}");
                    _provider = new DefaultFileProvider(
                        _pakDirectory,
                        SearchOption.AllDirectories,
                        false
                    );
                    _provider.Initialize();
                    _provider.SubmitKey(new FGuid(), new FAesKey(aesKey));

                    if (_appConfig.SaveAesKey == true)
                    {
                        AesKey.SaveAesKey(aesKey);
                    }

                    Console.WriteLine(
                        $"Provider initialized. Available files: {_provider.Files.Count}"
                    );
                }

                RegistryLocations.SavePakDirectory(_pakDirectory);

                Dictionary<string, string> uassetPathsByID = new Dictionary<string, string>();
                Dictionary<string, string> ubulkPathsByID = new Dictionary<string, string>();

                // Collect all relevant .uasset and .ubulk files
                foreach (var filePath in _provider.Files.Keys)
                {
                    string filename = Path.GetFileNameWithoutExtension(filePath);

                    if (
                        filePath.Contains("/WwiseAudio/", StringComparison.OrdinalIgnoreCase)
                        || filePath.Contains("\\WwiseAudio\\", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        if (filePath.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
                        {
                            uassetPathsByID[filename] = filePath;
                        }
                        else if (filePath.EndsWith(".ubulk", StringComparison.OrdinalIgnoreCase))
                        {
                            ubulkPathsByID[filename] = filePath;
                        }
                    }
                }

                Console.WriteLine(
                    $"Found {uassetPathsByID.Count} .uasset files and {ubulkPathsByID.Count} .ubulk files"
                );

                // Process files and extract MediaName
                foreach (var entry in ubulkPathsByID)
                {
                    string soundID = entry.Key;
                    string ubulkPath = entry.Value;
                    string folder = Path.GetDirectoryName(ubulkPath);
                    string description = "Unknown"; // Default description

                    // Check if there's a matching .uasset file
                    if (uassetPathsByID.TryGetValue(soundID, out string uassetPath))
                    {
                        try
                        {
                            // Load the package
                            var package = _provider.LoadPackage(uassetPath);
                            if (package != null)
                            {
                                // Convert exports to JSON
                                string jsonData = JsonConvert.SerializeObject(
                                    package.GetExports(),
                                    Formatting.Indented
                                );

                                try
                                {
                                    var jsonArray = JsonConvert.DeserializeObject<JArray>(jsonData);
                                    if (jsonArray != null)
                                    {
                                        // Extract MediaName
                                        foreach (var item in jsonArray)
                                        {
                                            string assetType = item["Type"]?.ToString();

                                            if (
                                                (
                                                    assetType == "AkMediaAsset"
                                                    || assetType == "AkLocalizedMediaAsset"
                                                )
                                                && item["Properties"] != null
                                                && item["Properties"]["MediaName"] != null
                                            )
                                            {
                                                description = item["Properties"]
                                                    ["MediaName"]
                                                    .ToString();
                                                break;
                                            }
                                        }
                                    }
                                }
                                catch (Exception jsonEx)
                                {
                                    Console.WriteLine(
                                        $"Error parsing JSON for {uassetPath}: {jsonEx.Message}"
                                    );
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(
                                $"Error extracting MediaName from {uassetPath}: {ex.Message}"
                            );
                        }
                    }

                    string soundFolder = ubulkPath.Contains(
                        "Localized",
                        StringComparison.OrdinalIgnoreCase
                    )
                        ? "Localized"
                        : "Media";

                    // Add the extracted sound item
                    soundItems.Add(
                        new SoundItem(
                            _mainWindow._notificationManager!,
                            sharedSearcher,
                            _mainWindow
                        )
                        {
                            SoundId = soundID,
                            SoundDescription = description,
                            SoundFolder = soundFolder,
                            UbulkPath = ubulkPath,
                        }
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing PAK files: {ex.Message}");
            }

            Console.WriteLine($"Loaded {soundItems.Count} sound files");

            // Update UI
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _mainWindow._soundItems.Clear();
                foreach (var item in soundItems)
                {
                    _mainWindow._soundItems.Add(item);
                }

                if (soundItems.Count > 0)
                {
                    _mainWindow.UpdateGlobalStatus(
                        $"Loaded {soundItems.Count} sound files",
                        "ID Search"
                    );
                }
                else
                {
                    _mainWindow.UpdateGlobalStatus(
                        $"Loaded {soundItems.Count} sound files. AES key may be invalid.",
                        "ID Search"
                    );
                }
            });
        }

        public async void PlaySound(SoundItem soundItem)
        {
            StopAudio();

            if (_provider == null)
            {
                ShowWarning("Error: Provider is not initialized!");
                return;
            }

            string tempWemPath = Path.Combine(Path.GetTempPath(), $"{soundItem.SoundId}.wem");
            string tempWavPath = Path.Combine(Path.GetTempPath(), $"{soundItem.SoundId}.wav");

            string uexpPath = Path.ChangeExtension(soundItem.UbulkPath, ".uexp");
            string uassetPath = Path.ChangeExtension(soundItem.UbulkPath, ".uasset");

            string vgmstreamPath = GetVgmstreamPath();
            if (string.IsNullOrEmpty(vgmstreamPath))
                return; // Error already shown in GetVgmstreamPath

            try
            {
                byte[] wemData = Array.Empty<byte>();

                // Try getting ubulk data first
                if (_provider.Files.TryGetValue(soundItem.UbulkPath, out var ubulkFile))
                {
                    wemData = ubulkFile.Read();
                }

                // Then uexp
                if (wemData.Length == 0 && _provider.Files.TryGetValue(uexpPath, out var uexpFile))
                {
                    wemData = uexpFile.Read();
                }

                // Then uasset
                if (
                    wemData.Length == 0
                    && _provider.Files.TryGetValue(uassetPath, out var uassetFile)
                )
                {
                    wemData = uassetFile.Read();
                }

                if (wemData.Length == 0)
                {
                    ShowWarning("Could not extract WEM data.");
                    return;
                }

                await File.WriteAllBytesAsync(tempWemPath, wemData);

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = vgmstreamPath,
                    Arguments = $"-o \"{tempWavPath}\" \"{tempWemPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = false,
                };

                using (Process process = new Process { StartInfo = psi })
                {
                    process.Start();
                    await process.WaitForExitAsync();
                }

                if (!File.Exists(tempWavPath))
                {
                    ShowWarning("Failed to decode WEM to WAV.");
                    return;
                }

                StopAudio();

                _audioFile = new AudioFileReader(tempWavPath);
                _outputDevice = new WaveOutEvent();

                float volume = AppConfig.Instance.Volume / 100f;
                _audioFile.Volume = volume;

                _outputDevice.Init(_audioFile);
                _outputDevice.Play();

                _outputDevice.PlaybackStopped += (s, e) => StopAudio(); // Cleanup after playback
            }
            catch (Exception ex)
            {
                ShowWarning($"Error playing sound: {ex.Message}");
            }
        }

        public void StopAudio()
        {
            if (_outputDevice != null)
            {
                _outputDevice.Stop();
                _outputDevice.Dispose();
                _outputDevice = null;
            }

            if (_audioFile != null)
            {
                _audioFile.Dispose();
                _audioFile = null;
            }
        }

        public async Task ExportSound(SoundItem soundItem, string selectedPath)
        {
            if (_provider == null)
            {
                ShowWarning("Error: Provider is not initialized!");
                return;
            }

            try
            {
                string destFolder = Path.Combine(selectedPath);
                Directory.CreateDirectory(destFolder);

                // Prepare file paths
                string uassetPath = Path.ChangeExtension(soundItem.UbulkPath, ".uasset");
                string uexpPath = Path.ChangeExtension(soundItem.UbulkPath, ".uexp");
                string ubulkPath = soundItem.UbulkPath;

                // Create destination file paths
                string destUassetPath = Path.Combine(destFolder, $"{soundItem.SoundId}.uasset");
                string destUexpPath = Path.Combine(destFolder, $"{soundItem.SoundId}.uexp");
                string destUbulkPath = Path.Combine(destFolder, $"{soundItem.SoundId}.ubulk");
                string destJsonPath = Path.Combine(destFolder, $"{soundItem.SoundId}.json");

                int exportedCount = 0;

                // Export .uasset file and extract JSON properties
                if (_provider.Files.ContainsKey(uassetPath))
                {
                    await File.WriteAllBytesAsync(
                        destUassetPath,
                        _provider.Files[uassetPath].Read()
                    );
                    exportedCount++;

                    // Load the package to extract JSON properties
                    try
                    {
                        var package = _provider.LoadPackage(uassetPath);
                        if (package != null)
                        {
                            // Convert exports to JSON
                            string jsonData = JsonConvert.SerializeObject(
                                package.GetExports(),
                                Formatting.Indented
                            );

                            await File.WriteAllTextAsync(destJsonPath, jsonData);
                            exportedCount++;
                        }
                    }
                    catch (Exception jsonEx)
                    {
                        Console.WriteLine(
                            $"Error extracting JSON for {uassetPath}: {jsonEx.Message}"
                        );
                        ShowWarning(
                            $"$\"Error extracting JSON for {{uassetPath}}: {{jsonEx.Message}}\""
                        );
                    }
                }

                // Export .uexp file
                if (_provider.Files.ContainsKey(uexpPath))
                {
                    await File.WriteAllBytesAsync(destUexpPath, _provider.Files[uexpPath].Read());
                    exportedCount++;
                }

                // Export .ubulk file
                if (_provider.Files.ContainsKey(ubulkPath))
                {
                    await File.WriteAllBytesAsync(destUbulkPath, _provider.Files[ubulkPath].Read());
                    exportedCount++;
                }
            }
            catch (Exception ex)
            {
                ShowWarning($"Error exporting files: {ex.Message}");
            }
        }

        public async Task SaveSound(SoundItem soundItem, string saveFolder)
        {
            if (_provider == null)
            {
                ShowWarning("Error: Provider is not initialized!");
                return;
            }

            string vgmstreamPath = GetVgmstreamPath();
            if (string.IsNullOrEmpty(vgmstreamPath))
                return; // Error already shown in GetVgmstreamPath

            // Initialize shared command prompt window so that PAM doesn't get falsely flagged as a trojan/spam command windows
            InitializeSharedProcess();

            try
            {
                string savePath = Path.Combine(saveFolder, $"{soundItem.SoundId}.wav");

                string ubulkPath = soundItem.UbulkPath;
                string uexpPath = Path.ChangeExtension(ubulkPath, ".uexp");
                string uassetPath = Path.ChangeExtension(ubulkPath, ".uasset");

                // Check if uasset exists
                if (!_provider.Files.ContainsKey(uassetPath))
                {
                    ShowWarning("Could not find corresponding .uasset file.");
                    return;
                }

                // Load uasset
                var package = _provider.LoadPackage(uassetPath);
                if (package == null)
                {
                    ShowWarning("Failed to load .uasset.");
                    return;
                }

                // Extract JSON data
                string jsonData = JsonConvert.SerializeObject(
                    package.GetExports(),
                    Formatting.Indented
                );
                var jsonArray = JsonConvert.DeserializeObject<JArray>(jsonData);

                if (jsonArray == null)
                {
                    ShowWarning("Invalid JSON structure in .uasset file.");
                    return;
                }

                // Find relevant media asset data
                var mediaAssetData = jsonArray.FirstOrDefault(x =>
                    x["Type"]?.ToString() == "AkMediaAssetData"
                );
                if (mediaAssetData == null)
                {
                    ShowWarning("Could not find media asset data.");
                    return;
                }

                // Extract all BulkData chunks
                var dataChunks = mediaAssetData["DataChunks"]?.ToArray();
                if (dataChunks == null || dataChunks.Length == 0)
                {
                    ShowWarning("No BulkData found in .uasset file.");
                    return;
                }

                // Sort chunks by OffsetInFile
                var sortedChunks = dataChunks.OrderBy(chunk =>
                    Convert.ToInt32(chunk["BulkData"]["OffsetInFile"].ToString(), 16)
                );

                List<byte> wemData = new List<byte>();

                // Ensure ubulk file exists
                if (!_provider.Files.TryGetValue(ubulkPath, out var ubulkFile))
                {
                    ShowWarning("Could not find corresponding .ubulk file.");
                    return;
                }

                byte[] ubulkBytes = ubulkFile.Read();
                using (MemoryStream stream = new MemoryStream(ubulkBytes))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    foreach (var chunk in sortedChunks)
                    {
                        bool isPrefetch = chunk["IsPrefetch"]?.ToObject<bool>() ?? false;
                        if (isPrefetch)
                            continue; // Skip prefetch data

                        int chunkSize = chunk["BulkData"]["SizeOnDisk"]?.ToObject<int>() ?? 0;
                        int offset = Convert.ToInt32(
                            chunk["BulkData"]["OffsetInFile"].ToString(),
                            16
                        );

                        if (chunkSize <= 0)
                            continue;

                        // Seek to offset
                        stream.Seek(offset, SeekOrigin.Begin);

                        // Read chunk data
                        byte[] chunkData = reader.ReadBytes(chunkSize);
                        wemData.AddRange(chunkData);
                    }
                }

                if (wemData.Count == 0)
                {
                    ShowWarning("Failed to reconstruct WEM data.");
                    return;
                }

                // Write WEM data to temp file
                string tempWemPath = Path.Combine(Path.GetTempPath(), $"{soundItem.SoundId}.wem");
                string tempWavPath = Path.Combine(Path.GetTempPath(), $"{soundItem.SoundId}.wav");

                await File.WriteAllBytesAsync(tempWemPath, wemData.ToArray());

                // Convert WEM to WAV using vgmstream
                string vgmstreamExe = Path.GetFileName(vgmstreamPath);
                _sharedProcess.StandardInput.WriteLine(
                    $"{vgmstreamExe} -o \"{tempWavPath}\" \"{tempWemPath}\""
                );
                _sharedProcess.StandardInput.Flush();

                int attempts = 0;
                while (!File.Exists(tempWavPath) && attempts < 50)
                {
                    await Task.Delay(100);
                    attempts++;
                }

                if (!File.Exists(tempWavPath))
                {
                    ShowWarning("Conversion failed: WAV file was not created.");
                    return;
                }

                // Copy to destination
                File.Copy(tempWavPath, savePath, true);

                // Clean up temp files
                try
                {
                    File.Delete(tempWemPath);
                    File.Delete(tempWavPath);
                }
                catch { }
            }
            catch (Exception ex)
            {
                ShowWarning($"Error saving sound: {ex.Message}");
            }
        }

        private void ShowWarning(string message)
        {
            Dispatcher.UIThread?.InvokeAsync(() =>
            {
                var warningDialog = new WarningDialog(message);
                warningDialog.Show();
            });
        }
    }
}
