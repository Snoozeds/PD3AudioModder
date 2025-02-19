using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PD3AudioModder.util
{
    internal class BatchProcessor
    {
        private readonly MainWindow? _mainWindow;
        private AppConfig? _appConfig;
        private readonly FileProcessor _fileProcessor;
        private string _audioFolderPath = string.Empty;
        private string _gameFilesFolderPath = string.Empty;

        // track skipped files and reason for skipping them
        private Dictionary<string, string> _skippedFiles = new Dictionary<string, string>();

        public BatchProcessor(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            _fileProcessor = new FileProcessor(_mainWindow);
        }

        public void UpdateStatus(string message, TextBlock statusTextBlock, ProgressBar? progressBar = null, double? progress = null)
        {
            if (_mainWindow != null)
            {
                _mainWindow.UpdateGlobalStatus(message, "Batch Conversion");
            }

            if (progressBar != null && progress.HasValue)
            {
                progressBar.Value = progress.Value;
            }
        }

        public void UpdateButtonStates(string audioFolderPath, string gameFilesFolderPath, Button batchConvertButton)
        {
            if (batchConvertButton != null)
            {
                // Enable button only if both folders are selected
                batchConvertButton.IsEnabled = !string.IsNullOrEmpty(_audioFolderPath) && !string.IsNullOrEmpty(_gameFilesFolderPath);
            }
        }

        public async Task ProcessBatch(string tempDirectory, bool useDefaultExportPath, TextBlock statusTextBlock, ProgressBar progressBar,
            Button batchConvertButton, WindowBase parentWindow)
        {
            if (string.IsNullOrEmpty(_audioFolderPath) || string.IsNullOrEmpty(_gameFilesFolderPath))
            {
                UpdateStatus("Error: Please select both folders first", statusTextBlock);
                return;
            }

            try
            {
                // Ask for output directory, or use the default export path
                _appConfig = AppConfig.Load();
                IStorageFolder? folderResult = null;

                if (!String.IsNullOrEmpty(_appConfig.DefaultExportFolder) && useDefaultExportPath)
                {
                    folderResult = await parentWindow.StorageProvider.TryGetFolderFromPathAsync(_appConfig.DefaultExportFolder);
                }

                if (folderResult == null || !useDefaultExportPath)
                {
                    var folderResults = await parentWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                    {
                        Title = "Choose output folder for converted files",
                        AllowMultiple = false
                    });

                    if (!folderResults.Any())
                    {
                        UpdateStatus("Batch conversion cancelled", statusTextBlock);
                        return;
                    }

                    folderResult = folderResults[0];
                }

                string outputDirectory = folderResult.Path.LocalPath;

                // Get all audio files
                var audioFiles = Directory.GetFiles(_audioFolderPath, "*.*")
                    .Where(file => new[] { ".wav", ".mp3", ".ogg", ".flac", ".aiff", ".wma", ".m4a", ".aac", ".opus" }
                        .Contains(Path.GetExtension(file).ToLower()))
                    .ToList();

                if (!audioFiles.Any())
                {
                    UpdateStatus("Error: No supported audio files found in the selected folder", statusTextBlock);
                    return;
                }

                // Get all game files
                var gameFiles = Directory.GetFiles(_gameFilesFolderPath, "*.*", SearchOption.AllDirectories)
                    .Where(file => new[] { ".ubulk", ".uexp", ".uasset", ".json" }
                        .Contains(Path.GetExtension(file).ToLower()))
                    .ToList();

                // Group game files by base name
                var gameFileGroups = gameFiles
                    .GroupBy(file => Path.GetFileNameWithoutExtension(file).ToLower())
                    .ToDictionary(g => g.Key, g => g.ToList());

                double totalFiles = audioFiles.Count;
                double processedFiles = 0;

                foreach (var audioFile in audioFiles)
                {
                    string audioBaseName = Path.GetFileNameWithoutExtension(audioFile);

                    // Find matching game files
                    if (gameFileGroups.TryGetValue(audioBaseName.ToLower(), out var matchingFiles))
                    {
                        string? ubulkPath = matchingFiles.FirstOrDefault(f => f.EndsWith(".ubulk", StringComparison.OrdinalIgnoreCase));
                        string? uexpPath = matchingFiles.FirstOrDefault(f => f.EndsWith(".uexp", StringComparison.OrdinalIgnoreCase));
                        string? uassetPath = matchingFiles.FirstOrDefault(f => f.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase));
                        string? jsonPath = matchingFiles.FirstOrDefault(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

                        if (ubulkPath != null && uexpPath != null && (uassetPath != null || jsonPath != null))
                        {
                            UpdateStatus($"Processing {audioBaseName}...", statusTextBlock, progressBar, (processedFiles / totalFiles) * 100);

                            try
                            {
                                // Process the file conversion
                                await ProcessSingleFile(
                                    audioFile,
                                    ubulkPath,
                                    uexpPath,
                                    uassetPath!,
                                    jsonPath!,
                                    tempDirectory,
                                    outputDirectory,
                                    audioBaseName
                                );
                            }
                            catch (Exception ex)
                            {
                                UpdateStatus($"Error processing {audioBaseName}: {ex.Message}", statusTextBlock);
                                // Continue with next file
                            }
                        }
                        else
                        {
                            UpdateStatus($"Skipping {audioBaseName}: Missing required game files", statusTextBlock);
                            _skippedFiles[audioBaseName] = "Missing required game files";
                        }
                    }
                    else
                    {
                        UpdateStatus($"Skipping {audioBaseName}: No matching game files found", statusTextBlock);
                        _skippedFiles[audioBaseName] = "No matching game files found";
                    }

                    processedFiles++;
                    UpdateStatus($"Processed {processedFiles} of {totalFiles} files", statusTextBlock, progressBar, (processedFiles / totalFiles) * 100);
                }

                UpdateStatus($"Batch conversion completed! Processed {processedFiles} files", statusTextBlock, progressBar, 100);
                _audioFolderPath = string.Empty;
                _gameFilesFolderPath = string.Empty;

                if (_skippedFiles.Any())
                {
                    var warningDialog = new WarningDialog("Some files were skipped during batch conversion:\n\n" +
                        string.Join("\n", _skippedFiles.Select(kvp => $"{kvp.Key}: {kvp.Value}")));
                    warningDialog.Show();
                }

            }
            catch (Exception ex)
            {
                UpdateStatus($"Error during batch processing: {ex.Message}", statusTextBlock);
            }
        }

        private async Task ProcessSingleFile(string audioPath, string ubulkPath, string uexpPath, string uassetPath,
            string jsonPath, string tempDirectory, string outputDirectory, string baseName)
        {
            // Copy the original ubulk file to temp directory
            string tempUbulkPath = Path.Combine(tempDirectory, Path.GetFileName(ubulkPath));
            File.Copy(ubulkPath, tempUbulkPath, true);

            // Copy the original uexp file to temp directory
            string tempUexpPath = Path.Combine(tempDirectory, Path.GetFileName(uexpPath));
            File.Copy(uexpPath, tempUexpPath, true);

            // Convert audio to WAV
            string wavPath = Path.Combine(tempDirectory, Path.GetFileNameWithoutExtension(audioPath) + ".wav");
            await AudioConverter.ConvertToWAV(audioPath, wavPath);

            // Convert WAV to WEM
            string wemPath = Path.Combine(tempDirectory, Path.GetFileNameWithoutExtension(wavPath) + ".wem");
            await Task.Run(() => WwisePD3.EncodeToWem(wavPath, wemPath));

            // Get the original size from the JSON file if it exists
            long oldSize = -1;
            if (!string.IsNullOrEmpty(jsonPath))
            {
                oldSize = _fileProcessor.GetOldSizeFromJson(jsonPath, null!);
            }

            // Get the new size from the wem file
            long newSize = new FileInfo(wemPath).Length;

            // Modify the temporary uexp file if we have the old size
            if (oldSize != -1)
            {
                _fileProcessor.ModifyUexpSize(tempUexpPath, oldSize, newSize);
            }

            // Replace the temporary ubulk file with the new wem file
            File.Copy(wemPath, tempUbulkPath, true);

            // Save the converted files
            string ubulkSavePath = Path.Combine(outputDirectory, Path.GetFileName(ubulkPath));
            string uexpSavePath = Path.Combine(outputDirectory, Path.GetFileName(uexpPath));

            File.Copy(tempUbulkPath, ubulkSavePath, true);
            File.Copy(tempUexpPath, uexpSavePath, true);

            if (!string.IsNullOrEmpty(uassetPath))
            {
                string uassetSavePath = Path.Combine(outputDirectory, baseName + ".uasset");
                File.Copy(uassetPath, uassetSavePath, true);
            }
            else if (!string.IsNullOrEmpty(jsonPath))
            {
                string jsonSavePath = Path.Combine(outputDirectory, baseName + ".json");
                File.Copy(jsonPath, jsonSavePath, true);
            }

            // Clean up temp files
            try
            {
                if (File.Exists(wavPath)) File.Delete(wavPath);
                if (File.Exists(wemPath)) File.Delete(wemPath);
                if (File.Exists(tempUbulkPath)) File.Delete(tempUbulkPath);
                if (File.Exists(tempUexpPath)) File.Delete(tempUexpPath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        public void SetAudioFolderPath(string path)
        {
            _audioFolderPath = path;
            if (!string.IsNullOrEmpty(path))
            {
                Console.WriteLine($"Audio folder path set to: {path}");
            }
        }

        public void SetGameFilesFolderPath(string path)
        {
            _gameFilesFolderPath = path;
            if (!string.IsNullOrEmpty(path))
            {
                Console.WriteLine($"Game files folder path set to: {path}");
            }
        }
    }
}