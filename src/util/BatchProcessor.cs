using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

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

        // used to track clicking the "yes to all" button in the warning dialog when files exist in export dir.
        private bool _yesToAllFiles = false;

        public BatchProcessor(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            _fileProcessor = new FileProcessor(_mainWindow);
        }

        public void UpdateStatus(
            string message,
            TextBlock statusTextBlock,
            ProgressBar? progressBar = null,
            double? progress = null
        )
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

        public void UpdateButtonStates(
            string audioFolderPath,
            string gameFilesFolderPath,
            Button batchConvertButton
        )
        {
            if (batchConvertButton != null)
            {
                // Enable button only if both folders are selected
                batchConvertButton.IsEnabled =
                    !string.IsNullOrEmpty(_audioFolderPath)
                    && !string.IsNullOrEmpty(_gameFilesFolderPath);
            }
        }

        public async Task ProcessBatch(
            string tempDirectory,
            bool useDefaultExportPath,
            TextBlock statusTextBlock,
            ProgressBar progressBar,
            Button batchConvertButton,
            WindowBase parentWindow
        )
        {
            if (
                string.IsNullOrEmpty(_audioFolderPath) || string.IsNullOrEmpty(_gameFilesFolderPath)
            )
            {
                UpdateStatus("Error: Please select both folders first", statusTextBlock);
                return;
            }

            try
            {
                // Ask for output directory, or use the default export path
                _appConfig = AppConfig.Instance;
                IStorageFolder? folderResult = null;

                if (!String.IsNullOrEmpty(_appConfig.DefaultExportFolder) && useDefaultExportPath)
                {
                    folderResult = await parentWindow.StorageProvider.TryGetFolderFromPathAsync(
                        _appConfig.DefaultExportFolder
                    );
                }

                if (folderResult == null || !useDefaultExportPath)
                {
                    var folderResults = await parentWindow.StorageProvider.OpenFolderPickerAsync(
                        new FolderPickerOpenOptions
                        {
                            Title = "Choose output folder for converted files",
                            AllowMultiple = false,
                        }
                    );

                    if (!folderResults.Any())
                    {
                        UpdateStatus("Batch conversion cancelled", statusTextBlock);
                        return;
                    }

                    folderResult = folderResults[0];
                }

                string outputDirectory = folderResult.Path.LocalPath;

                // Get all audio files
                var audioFiles = Directory
                    .GetFiles(_audioFolderPath, "*.*")
                    .Where(file =>
                        new[]
                        {
                            ".wav",
                            ".mp3",
                            ".ogg",
                            ".flac",
                            ".aiff",
                            ".wma",
                            ".m4a",
                            ".aac",
                            ".opus",
                        }.Contains(Path.GetExtension(file).ToLower())
                    )
                    .ToList();

                if (!audioFiles.Any())
                {
                    UpdateStatus(
                        "Error: No supported audio files found in the selected folder",
                        statusTextBlock
                    );
                    return;
                }

                // Get all game files
                var gameFiles = Directory
                    .GetFiles(_gameFilesFolderPath, "*.*", SearchOption.AllDirectories)
                    .Where(file =>
                        new[] { ".ubulk", ".uexp", ".uasset", ".json" }.Contains(
                            Path.GetExtension(file).ToLower()
                        )
                    )
                    .ToList();

                // Group game files by base name
                var gameFileGroups = gameFiles
                    .GroupBy(file => Path.GetFileNameWithoutExtension(file).ToLower())
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Collect all files that need processing
                var filesToProcess =
                    new List<(
                        string audioPath,
                        string ubulkPath,
                        string uexpPath,
                        string uassetPath,
                        string jsonPath,
                        string baseName
                    )>();
                foreach (var audioFile in audioFiles)
                {
                    string audioBaseName = Path.GetFileNameWithoutExtension(audioFile);
                    if (gameFileGroups.TryGetValue(audioBaseName.ToLower(), out var matchingFiles))
                    {
                        string? ubulkPath = matchingFiles.FirstOrDefault(f =>
                            f.EndsWith(".ubulk", StringComparison.OrdinalIgnoreCase)
                        );
                        string? uexpPath = matchingFiles.FirstOrDefault(f =>
                            f.EndsWith(".uexp", StringComparison.OrdinalIgnoreCase)
                        );
                        string? uassetPath = matchingFiles.FirstOrDefault(f =>
                            f.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)
                        );
                        string? jsonPath = matchingFiles.FirstOrDefault(f =>
                            f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                        );

                        if (
                            ubulkPath != null
                            && uexpPath != null
                            && (uassetPath != null || jsonPath != null)
                        )
                        {
                            filesToProcess.Add(
                                (
                                    audioFile,
                                    ubulkPath,
                                    uexpPath,
                                    uassetPath!,
                                    jsonPath!,
                                    audioBaseName
                                )
                            );
                        }
                        else
                        {
                            _skippedFiles[audioBaseName] = "Missing required game files";
                        }
                    }
                    else
                    {
                        _skippedFiles[audioBaseName] = "No matching game files found";
                    }
                }

                if (filesToProcess.Count > 0)
                {
                    UpdateStatus(
                        $"Processing {filesToProcess.Count} files...",
                        statusTextBlock,
                        progressBar,
                        0
                    );

                    await ProcessFiles(
                        filesToProcess,
                        tempDirectory,
                        outputDirectory,
                        statusTextBlock,
                        progressBar
                    );

                    UpdateStatus(
                        $"Batch conversion completed! Processed {filesToProcess.Count} files",
                        statusTextBlock,
                        progressBar,
                        100
                    );
                }
                else
                {
                    UpdateStatus("No files to process", statusTextBlock);
                }

                _audioFolderPath = string.Empty;
                _gameFilesFolderPath = string.Empty;

                if (_skippedFiles.Any())
                {
                    var warningDialog = new WarningDialog(
                        "Some files were skipped during batch conversion:\n\n"
                            + string.Join(
                                "\n",
                                _skippedFiles.Select(kvp => $"{kvp.Key}: {kvp.Value}")
                            )
                    );
                    warningDialog.Show();
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error during batch processing: {ex.Message}", statusTextBlock);
            }
        }

        private async Task ProcessFiles(
            List<(
                string audioPath,
                string ubulkPath,
                string uexpPath,
                string uassetPath,
                string jsonPath,
                string baseName
            )> filesToProcess,
            string tempDirectory,
            string outputDirectory,
            TextBlock statusTextBlock,
            ProgressBar progressBar
        )
        {
            var audioInputPaths = filesToProcess.Select(f => f.audioPath).ToArray();
            double totalFiles = filesToProcess.Count;
            double processedFiles = 0;

            // Track PCM errors
            var pcmErrorFiles = new List<string>();

            try
            {
                // Convert all audio files to WAV format
                await AudioConverter.BatchConvertToWAV(audioInputPaths, tempDirectory);

                // Process each file pair
                foreach (var fileSet in filesToProcess)
                {
                    try
                    {
                        UpdateStatus(
                            $"Processing {fileSet.baseName}...",
                            statusTextBlock,
                            progressBar,
                            (processedFiles / totalFiles) * 100
                        );

                        // Check for existing files
                        if (_appConfig?.DisplayFilesInExportWarning == true && !_yesToAllFiles)
                        {
                            var result = await _fileProcessor.CheckExistingFiles(
                                outputDirectory,
                                fileSet.baseName
                            );
                            if (!result.ShouldProceed)
                            {
                                _skippedFiles[fileSet.baseName] =
                                    "User skipped due to existing files";
                                continue;
                            }
                            _yesToAllFiles = result.YesToAll;
                        }

                        string wavPath = Path.Combine(
                            tempDirectory,
                            Path.GetFileNameWithoutExtension(fileSet.audioPath) + ".wav"
                        );

                        // Convert WAV to WEM
                        string wemPath = Path.Combine(
                            tempDirectory,
                            Path.GetFileNameWithoutExtension(wavPath) + ".wem"
                        );
                        await Task.Run(() => WwisePD3.EncodeToWem(wavPath, wemPath));

                        // Process UBULK and UEXP files
                        string tempUbulkPath = Path.Combine(
                            tempDirectory,
                            Path.GetFileName(fileSet.ubulkPath)
                        );
                        string tempUexpPath = Path.Combine(
                            tempDirectory,
                            Path.GetFileName(fileSet.uexpPath)
                        );

                        File.Copy(fileSet.ubulkPath, tempUbulkPath, true);
                        File.Copy(fileSet.uexpPath, tempUexpPath, true);

                        // Handle file sizes
                        long oldSize = -1;
                        if (!string.IsNullOrEmpty(fileSet.jsonPath))
                        {
                            oldSize = _fileProcessor.GetOldSizeFromJson(fileSet.jsonPath);
                        }

                        long newSize = new FileInfo(wemPath).Length;

                        if (oldSize != -1)
                        {
                            _fileProcessor.ModifyUexpSize(tempUexpPath, oldSize, newSize);
                        }

                        // Replace UBULK with new WEM file
                        File.Copy(wemPath, tempUbulkPath, true);

                        // Save converted files
                        string ubulkSavePath = Path.Combine(
                            outputDirectory,
                            Path.GetFileName(fileSet.ubulkPath)
                        );
                        string uexpSavePath = Path.Combine(
                            outputDirectory,
                            Path.GetFileName(fileSet.uexpPath)
                        );

                        File.Copy(tempUbulkPath, ubulkSavePath, true);
                        File.Copy(tempUexpPath, uexpSavePath, true);

                        if (!string.IsNullOrEmpty(fileSet.uassetPath))
                        {
                            string uassetSavePath = Path.Combine(
                                outputDirectory,
                                fileSet.baseName + ".uasset"
                            );
                            File.Copy(fileSet.uassetPath, uassetSavePath, true);
                        }
                        else if (!string.IsNullOrEmpty(fileSet.jsonPath))
                        {
                            string jsonSavePath = Path.Combine(
                                outputDirectory,
                                fileSet.baseName + ".json"
                            );
                            File.Copy(fileSet.jsonPath, jsonSavePath, true);
                        }

                        // Clean up temp files
                        try
                        {
                            File.Delete(wavPath);
                            File.Delete(wemPath);
                            File.Delete(tempUbulkPath);
                            File.Delete(tempUexpPath);
                        }
                        catch
                        {
                            // Ignore cleanup errors
                        }

                        processedFiles++;
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("PAYDAY 3 only supports PCM"))
                    {
                        pcmErrorFiles.Add(fileSet.baseName);
                        _skippedFiles[fileSet.baseName] = $"Error: {ex.Message}";
                        continue;
                    }
                    catch (Exception ex)
                    {
                        _skippedFiles[fileSet.baseName] = $"Error during processing: {ex.Message}";
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Batch conversion failed: {ex.Message}", ex);
            }
            finally
            {
                _yesToAllFiles = false;


                if (pcmErrorFiles.Count > 0)
                {
                    Dispatcher.UIThread?.InvokeAsync(() =>
                    {
                        var warningDialog = new WarningDialog(
                            $"wwise_pd3 error:\nPAYDAY 3 only supports PCM format.\nThis will cause these files to NOT play.\n\n" +
                            $"The following {pcmErrorFiles.Count} file(s) were skipped:\n" +
                            string.Join("\n", pcmErrorFiles) +
                            "\n\nThis may be caused by incorrect ffmpeg arguments."
                        );
                        warningDialog.Show();
                    });
                }
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
