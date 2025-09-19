using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Newtonsoft.Json;

namespace PD3AudioModder.util
{
    /// <summary>
    /// Class to represent the result of checking for existing files.
    /// </summary>
    public class FileCheckResult
    {
        public bool ShouldProceed { get; set; }
        public bool YesToAll { get; set; }
    }

    /// <summary>
    /// Class to handle file processing for single file conversion.
    /// </summary>
    internal class FileProcessor
    {
        private readonly MainWindow? _mainWindow;
        private AppConfig? _appConfig;
        private WindowNotificationManager _notificationManager;

        public FileProcessor(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            _notificationManager = mainWindow._notificationManager!;
        }

        public void UpdateStatus(string message)
        {
            if (_mainWindow != null)
            {
                _mainWindow.UpdateGlobalStatus(message, "Single File");
            }
        }

        /// <summary>
        /// Task to check for existing files in the target directory and prompt the user if they exist.
        /// </summary>
        /// <param name="saveDirectory">The directory to check.</param>
        /// <param name="baseFileName">The base filename to check for (without extension).</param>
        /// <returns></returns>
        public async Task<FileCheckResult> CheckExistingFiles(
            string saveDirectory,
            string baseFileName
        )
        {
            // Check if any of the target files already exist
            bool filesExist =
                File.Exists(Path.Combine(saveDirectory, baseFileName + ".ubulk"))
                || File.Exists(Path.Combine(saveDirectory, baseFileName + ".uexp"))
                || File.Exists(Path.Combine(saveDirectory, baseFileName + ".uasset"))
                || File.Exists(Path.Combine(saveDirectory, baseFileName + ".json"));

            if (filesExist)
            {
                UpdateStatus("Warning: Some files already exist in the target directory...");
                var warningDialog = new WarningConfirmDialog(
                    "Some files already exist in the target directory.",
                    "Do you want to overwrite them?"
                );
                await warningDialog.ShowDialog(_mainWindow!);

                return new FileCheckResult
                {
                    ShouldProceed = warningDialog.UserResponse,
                    YesToAll = warningDialog.YesToAllResponse,
                };
            }

            return new FileCheckResult { ShouldProceed = true, YesToAll = false };
        }

        /// <summary>
        /// Process the uploaded files, convert audio, modify sizes, and save outputs.
        /// </summary>
        /// <param name="uploadedAudioPath">The path to the uploaded audio file.</param>
        /// <param name="uploadedUbulkPath">The path to the uploaded ubulk file.</param>
        /// <param name="uploadedUexpPath">The path to the uploaded uexp file.</param>
        /// <param name="uploadedUassetPath">The path to the uploaded uasset file (optional).</param>
        /// <param name="uploadedJsonPath">The path to the uploaded JSON file (optional).</param>
        /// <param name="tempDirectory">The path to the temp directory.</param>
        /// <param name="useDefaultExportPath">True to use default export path, false to prompt user.</param>
        /// <param name="statusTextBlock">The TextBlock to update status messages.</param>
        /// <param name="convertButton">Reference to the convert button to enable/disable.</param>
        /// <param name="ParentWindow">Reference to the parent window for dialogs.</param>
        /// <returns></returns>
        public async Task ProcessFiles(
            string uploadedAudioPath,
            string uploadedUbulkPath,
            string uploadedUexpPath,
            string uploadedUassetPath,
            string uploadedJsonPath,
            string tempDirectory,
            bool useDefaultExportPath,
            TextBlock statusTextBlock,
            Button convertButton,
            WindowBase ParentWindow
        )
        {
            if (
                uploadedAudioPath == null
                || uploadedUbulkPath == null
                || uploadedUexpPath == null
                || (uploadedJsonPath == null && uploadedUassetPath == null)
            )
            {
                UpdateStatus("Error: Please upload all required files first");
                return;
            }

            try
            {
                // Copy the original ubulk file to temp directory
                string tempUbulkPath = Path.Combine(
                    tempDirectory,
                    Path.GetFileName(uploadedUbulkPath)
                );
                File.Copy(uploadedUbulkPath, tempUbulkPath, true);
                UpdateStatus("Created backup of original ubulk file...");

                // Copy the original uexp file to temp directory
                string tempUexpPath = Path.Combine(
                    tempDirectory,
                    Path.GetFileName(uploadedUexpPath)
                );
                File.Copy(uploadedUexpPath, tempUexpPath, true);
                UpdateStatus("Created backup of original uexp file...");

                // Convert audio to WAV using MediaFoundation
                UpdateStatus("Converting audio to WAV format...");
                string wavPath = Path.Combine(
                    tempDirectory,
                    Path.GetFileNameWithoutExtension(uploadedAudioPath) + ".wav"
                );

                try
                {
                    await AudioConverter.ConvertToWAV(uploadedAudioPath, wavPath);
                }
                catch (Exception ex)
                {
                    throw new Exception(
                        $"Failed to convert audio: {ex.Message}. Try converting your audio to WAV format manually before uploading."
                    );
                }

                // Convert WAV to WEM
                UpdateStatus("Converting WAV to WEM format...");
                string wemPath = Path.Combine(
                    tempDirectory,
                    Path.GetFileNameWithoutExtension(wavPath) + ".wem"
                );
                await Task.Run(() => WwisePD3.EncodeToWem(wavPath, wemPath));

                // Get the original size from the JSON file if it exists
                long oldSize = -1;
                if (uploadedJsonPath != null)
                {
                    UpdateStatus("Reading original size from JSON...");
                    oldSize = GetOldSizeFromJson(uploadedJsonPath);
                    if (oldSize == -1)
                    {
                        UpdateStatus("Error: Size not found in JSON file");
                        return;
                    }
                }

                // Get the new size from the wem file
                UpdateStatus("Processing file sizes...");
                long newSize = new FileInfo(wemPath).Length;

                // Modify the temporary uexp file if we have the old size
                if (oldSize != -1)
                {
                    UpdateStatus("Modifying temporary uexp file size...");
                    ModifyUexpSize(tempUexpPath, oldSize, newSize);
                }

                // Replace the temporary ubulk file with the new wem file
                UpdateStatus("Replacing temporary ubulk file with new WEM...");
                File.Copy(wemPath, tempUbulkPath, true);

                // Ask user where to save the files, or use the default export path
                _appConfig = AppConfig.Load();
                IStorageFolder? folderResult = null;

                if (!String.IsNullOrEmpty(_appConfig.DefaultExportFolder) && useDefaultExportPath)
                {
                    folderResult = await ParentWindow.StorageProvider.TryGetFolderFromPathAsync(
                        _appConfig.DefaultExportFolder
                    );
                }

                if (folderResult == null || !useDefaultExportPath)
                {
                    UpdateStatus("Select where to save converted files...");
                    var fileResult = await ParentWindow.StorageProvider.SaveFilePickerAsync(
                        new FilePickerSaveOptions
                        {
                            Title = "Choose where to save converted files",
                            DefaultExtension = "",
                            ShowOverwritePrompt = true,
                            SuggestedFileName = Path.GetFileNameWithoutExtension(uploadedUbulkPath),
                        }
                    );

                    if (fileResult == null)
                    {
                        UpdateStatus("Save operation cancelled");
                        return;
                    }
                    folderResult = await fileResult.GetParentAsync();
                }

                if (folderResult != null)
                {
                    string saveDirectory = Path.GetDirectoryName(folderResult.Path.LocalPath)!;
                    string baseFileName = Path.GetFileNameWithoutExtension(uploadedUbulkPath);

                    // Check for existing files in the export directory if user has the setting enabled.
                    if (_appConfig.DisplayFilesInExportWarning == true)
                    {
                        var result = await CheckExistingFiles(saveDirectory, baseFileName);
                        if (!result.ShouldProceed)
                        {
                            UpdateStatus("Operation cancelled by user");
                            return;
                        }
                    }

                    // Save all files with the chosen base filename
                    UpdateStatus("Saving converted files...");

                    // Save ubulk
                    string ubulkSavePath = Path.Combine(saveDirectory, baseFileName + ".ubulk");
                    File.Copy(tempUbulkPath, ubulkSavePath, true);

                    // Save uexp
                    string uexpSavePath = Path.Combine(saveDirectory, baseFileName + ".uexp");
                    File.Copy(tempUexpPath, uexpSavePath, true);

                    // Save uasset or json if they exist
                    if (uploadedUassetPath != null)
                    {
                        string uassetSavePath = Path.Combine(
                            saveDirectory,
                            baseFileName + ".uasset"
                        );
                        File.Copy(uploadedUassetPath, uassetSavePath, true);
                    }
                    else if (uploadedJsonPath != null)
                    {
                        string jsonSavePath = Path.Combine(saveDirectory, baseFileName + ".json");
                        File.Copy(uploadedJsonPath, jsonSavePath, true);
                    }

                    UpdateStatus(
                        $"Conversion completed successfully! Files saved to {saveDirectory}"
                    );

                    _notificationManager?.Show(
                        new Notification(
                            "Export Complete",
                            $"Conversion completed successfully!\nFiles saved to {saveDirectory}",
                            NotificationType.Success,
                            TimeSpan.FromSeconds(5)
                        )
                    );
                }
                else
                {
                    UpdateStatus("Save operation cancelled");
                }

                // Clean up temp files
                try
                {
                    if (File.Exists(wavPath))
                        File.Delete(wavPath);
                    if (File.Exists(wemPath))
                        File.Delete(wemPath);
                    if (File.Exists(tempUbulkPath))
                        File.Delete(tempUbulkPath);
                    if (File.Exists(tempUexpPath))
                        File.Delete(tempUexpPath);

                    uploadedAudioPath = string.Empty;
                    uploadedUbulkPath = string.Empty;
                    uploadedUexpPath = string.Empty;
                    uploadedUassetPath = string.Empty;

                    UpdateConvertButtonState(
                        uploadedAudioPath,
                        uploadedUbulkPath,
                        uploadedUexpPath,
                        uploadedUassetPath,
                        uploadedJsonPath!,
                        convertButton
                    );
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            catch (InvalidOperationException ex)
                when (ex.Message.Contains("PAYDAY 3 only supports PCM"))
            {
                Dispatcher.UIThread?.InvokeAsync(() =>
                {
                    var warningDialog = new WarningDialog(
                        $"wwise_pd3 error:\nPAYDAY 3 only supports PCM, not the type of this audio file.\nThis will cause these files to NOT play.\n\nThis may be caused by incorrect ffmpeg arguments."
                    );
                    warningDialog.Show();
                });

                UpdateStatus($"Error: {ex.Message}");
                return;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Update the state of the convert button based on whether all required files are uploaded.
        /// </summary>
        /// <param name="uploadedAudioPath"></param>
        /// <param name="uploadedUbulkPath"></param>
        /// <param name="uploadedUexpPath"></param>
        /// <param name="uploadedUassetPath"></param>
        /// <param name="uploadedJsonPath"></param>
        /// <param name="convertButton"></param>
        public void UpdateConvertButtonState(
            string uploadedAudioPath,
            string uploadedUbulkPath,
            string uploadedUexpPath,
            string uploadedUassetPath,
            string uploadedJsonPath,
            Button convertButton
        )
        {
            // Check that the required file paths are not null.
            bool allFilesUploaded =
                !string.IsNullOrEmpty(uploadedAudioPath)
                && !string.IsNullOrEmpty(uploadedUbulkPath)
                && !string.IsNullOrEmpty(uploadedUexpPath)
                && (
                    !string.IsNullOrEmpty(uploadedJsonPath)
                    || !string.IsNullOrEmpty(uploadedUassetPath)
                );

            if (convertButton != null)
            {
                convertButton.IsEnabled = allFilesUploaded;
            }
        }

        /// <summary>
        /// Gets the original file's size from the JSON file.
        /// </summary>
        /// <param name="jsonFile"></param>
        /// <returns></returns>
        public long GetOldSizeFromJson(string jsonFile)
        {
            try
            {
                var jsonData = File.ReadAllText(jsonFile);
                dynamic jsonObject = JsonConvert.DeserializeObject(jsonData)!;
                foreach (var obj in jsonObject)
                {
                    if (obj.Type == "AkMediaAssetData")
                    {
                        return obj.DataChunks[0].BulkData.SizeOnDisk;
                    }
                }

                UpdateStatus("Error: Could not find AkMediaAssetData in JSON file");
                return -1;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error reading JSON: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Modifies the size value in the uexp file from oldSize to newSize to properly mod the audio.
        /// </summary>
        /// <param name="uexpFilePath"></param>
        /// <param name="oldSize"></param>
        /// <param name="newSize"></param>
        public void ModifyUexpSize(string uexpFilePath, long oldSize, long newSize)
        {
            byte[] uexpData = File.ReadAllBytes(uexpFilePath);

            // Convert sizes to little-endian byte arrays
            byte[] oldSizeBytes = BitConverter.GetBytes((uint)oldSize);
            byte[] newSizeBytes = BitConverter.GetBytes((uint)newSize);

            // Search for the old size pattern in the file
            for (int i = 0; i < uexpData.Length - 4; i++)
            {
                if (
                    uexpData[i] == oldSizeBytes[0]
                    && uexpData[i + 1] == oldSizeBytes[1]
                    && uexpData[i + 2] == oldSizeBytes[2]
                    && uexpData[i + 3] == oldSizeBytes[3]
                )
                {
                    // Replace with new size bytes
                    Array.Copy(newSizeBytes, 0, uexpData, i, 4);
                    break;
                }
            }

            File.WriteAllBytes(uexpFilePath, uexpData);
        }
    }
}
