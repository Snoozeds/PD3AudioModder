﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PD3AudioModder.util
{
    public class IDSearcher
    {
        private static DefaultFileProvider _provider;
        private static string _pakDirectory;
        private WaveOutEvent _outputDevice;
        private AudioFileReader _audioFile;

        public static async Task ProcessPakFiles(
            System.Collections.Generic.IReadOnlyList<Avalonia.Platform.Storage.IStorageFile> files,
            MainWindow _mainWindow
        )
        {
            List<SoundItem> soundItems = new List<SoundItem>();
            IDSearcher sharedSearcher = new IDSearcher();
            var aesKey = "0x27DFBADBB537388ACDE27A7C5F3EBC3721AF0AE0A7602D2D7F8A16548F37D394";

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
                    Console.WriteLine(
                        $"Provider initialized. Available files: {_provider.Files.Count}"
                    );
                }

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
                        new SoundItem(_mainWindow._notificationManager, sharedSearcher)
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
                _mainWindow.UpdateGlobalStatus(
                    $"Loaded {soundItems.Count} sound files",
                    "ID Search"
                );
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

            string vgmstreamPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "vgmstream-cli.exe"
            );

            try
            {
                if (!File.Exists(vgmstreamPath))
                {
                    ShowWarning(
                        "vgmstream command line not found!\nPlease download it from\nhttps://vgmstream.org/downloads\nand place it (and all the DLLs) in the same folder as this app."
                    );
                    return;
                }

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
