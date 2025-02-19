using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace PD3AudioModder.util
{
    internal class FileProcessor
    {
        private readonly MainWindow? _mainWindow;
        private AppConfig? _appConfig;

        public FileProcessor(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        public void UpdateStatus(string message, TextBlock statusTextBlock)
        {
            if (_mainWindow != null)
            {
                _mainWindow.UpdateGlobalStatus(message, "Single File");
            }
        }

        public async Task ProcessFiles(string uploadedAudioPath, string uploadedUbulkPath, string uploadedUexpPath,
            string uploadedUassetPath, string uploadedJsonPath, string tempDirectory, bool useDefaultExportPath, TextBlock statusTextBlock, Button convertButton, WindowBase ParentWindow)
        {
            if (uploadedAudioPath == null || uploadedUbulkPath == null || uploadedUexpPath == null ||
                (uploadedJsonPath == null && uploadedUassetPath == null))
            {
                UpdateStatus("Error: Please upload all required files first", statusTextBlock);
                return;
            }

            try
            {
                // Copy the original ubulk file to temp directory
                string tempUbulkPath = Path.Combine(tempDirectory, Path.GetFileName(uploadedUbulkPath));
                File.Copy(uploadedUbulkPath, tempUbulkPath, true);
                UpdateStatus("Created backup of original ubulk file...", statusTextBlock);

                // Copy the original uexp file to temp directory
                string tempUexpPath = Path.Combine(tempDirectory, Path.GetFileName(uploadedUexpPath));
                File.Copy(uploadedUexpPath, tempUexpPath, true);
                UpdateStatus("Created backup of original uexp file...", statusTextBlock);

                // Convert audio to WAV using MediaFoundation
                UpdateStatus("Converting audio to WAV format...", statusTextBlock);
                string wavPath = Path.Combine(tempDirectory, Path.GetFileNameWithoutExtension(uploadedAudioPath) + ".wav");

                try
                {
                    await AudioConverter.ConvertToWAV(uploadedAudioPath, wavPath);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to convert audio: {ex.Message}. Try converting your audio to WAV format manually before uploading.");
                }

                // Convert WAV to WEM
                UpdateStatus("Converting WAV to WEM format...", statusTextBlock);
                string wemPath = Path.Combine(tempDirectory, Path.GetFileNameWithoutExtension(wavPath) + ".wem");
                await Task.Run(() => WwisePD3.EncodeToWem(wavPath, wemPath));

                // Get the original size from the JSON file if it exists
                long oldSize = -1;
                if (uploadedJsonPath != null)
                {
                    UpdateStatus("Reading original size from JSON...", statusTextBlock);
                    oldSize = GetOldSizeFromJson(uploadedJsonPath, statusTextBlock);
                    if (oldSize == -1)
                    {
                        UpdateStatus("Error: Size not found in JSON file", statusTextBlock);
                        return;
                    }
                }

                // Get the new size from the wem file
                UpdateStatus("Processing file sizes...", statusTextBlock);
                long newSize = new FileInfo(wemPath).Length;

                // Modify the temporary uexp file if we have the old size
                if (oldSize != -1)
                {
                    UpdateStatus("Modifying temporary uexp file size...", statusTextBlock);
                    ModifyUexpSize(tempUexpPath, oldSize, newSize);
                }

                // Replace the temporary ubulk file with the new wem file
                UpdateStatus("Replacing temporary ubulk file with new WEM...", statusTextBlock);
                File.Copy(wemPath, tempUbulkPath, true);

                // Ask user where to save the files, or use the default export path
                _appConfig = AppConfig.Load();
                IStorageFolder? folderResult = null;

                if(!String.IsNullOrEmpty(_appConfig.DefaultExportFolder) && useDefaultExportPath) {
                    folderResult = await ParentWindow.StorageProvider.TryGetFolderFromPathAsync(_appConfig.DefaultExportFolder);
                }

                if (folderResult == null || !useDefaultExportPath)
                {
                    UpdateStatus("Select where to save converted files...", statusTextBlock);
                    var fileResult = await ParentWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = "Choose where to save converted files",
                        DefaultExtension = "",
                        ShowOverwritePrompt = true,
                        SuggestedFileName = Path.GetFileNameWithoutExtension(uploadedUbulkPath)
                    });

                    if (fileResult == null)
                    {
                        UpdateStatus("Save operation cancelled", statusTextBlock);
                        return;
                    }
                    folderResult = await fileResult.GetParentAsync();
                }

                if (folderResult != null)
                {
                    string saveDirectory = Path.GetDirectoryName(folderResult.Path.LocalPath)!;
                    string baseFileName = Path.GetFileNameWithoutExtension(uploadedUbulkPath);

                    // Save all files with the chosen base filename
                    UpdateStatus("Saving converted files...", statusTextBlock);

                    // Save ubulk
                    string ubulkSavePath = Path.Combine(saveDirectory, baseFileName + ".ubulk");
                    File.Copy(tempUbulkPath, ubulkSavePath, true);

                    // Save uexp
                    string uexpSavePath = Path.Combine(saveDirectory, baseFileName + ".uexp");
                    File.Copy(tempUexpPath, uexpSavePath, true);

                    // Save uasset or json if they exist
                    if (uploadedUassetPath != null)
                    {
                        string uassetSavePath = Path.Combine(saveDirectory, baseFileName + ".uasset");
                        File.Copy(uploadedUassetPath, uassetSavePath, true);
                    }
                    else if (uploadedJsonPath != null)
                    {
                        string jsonSavePath = Path.Combine(saveDirectory, baseFileName + ".json");
                        File.Copy(uploadedJsonPath, jsonSavePath, true);
                    }

                    UpdateStatus($"Conversion completed successfully! Files saved to {saveDirectory}", statusTextBlock);
                }
                else
                {
                    UpdateStatus("Save operation cancelled", statusTextBlock);
                }

                // Clean up temp files
                try
                {
                    if (File.Exists(wavPath)) File.Delete(wavPath);
                    if (File.Exists(wemPath)) File.Delete(wemPath);
                    if (File.Exists(tempUbulkPath)) File.Delete(tempUbulkPath);
                    if (File.Exists(tempUexpPath)) File.Delete(tempUexpPath);

                    uploadedAudioPath = string.Empty;
                    uploadedUbulkPath = string.Empty;
                    uploadedUexpPath = string.Empty;
                    uploadedUassetPath = string.Empty;

                    UpdateConvertButtonState(uploadedAudioPath, uploadedUbulkPath, uploadedUexpPath, uploadedUassetPath, uploadedJsonPath!, convertButton);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}", statusTextBlock);
            }
        }

        public void UpdateConvertButtonState(string uploadedAudioPath, string uploadedUbulkPath, string uploadedUexpPath,
            string uploadedUassetPath, string uploadedJsonPath, Button convertButton)
        {
            // Check that the required file paths are not null.
            bool allFilesUploaded =
                   !string.IsNullOrEmpty(uploadedAudioPath)
                && !string.IsNullOrEmpty(uploadedUbulkPath)
                && !string.IsNullOrEmpty(uploadedUexpPath)
                && (!string.IsNullOrEmpty(uploadedJsonPath) || !string.IsNullOrEmpty(uploadedUassetPath));

            if (convertButton != null)
            {
                convertButton.IsEnabled = allFilesUploaded;
            }
        }

        public long GetOldSizeFromJson(string jsonFile, TextBlock statusTextBlock)
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

                UpdateStatus("Error: Could not find AkMediaAssetData in JSON file", statusTextBlock);
                return -1;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error reading JSON: {ex.Message}", statusTextBlock);
                return -1;
            }
        }

        public void ModifyUexpSize(string uexpFilePath, long oldSize, long newSize)
        {
            byte[] uexpData = File.ReadAllBytes(uexpFilePath);

            // Convert sizes to little-endian byte arrays
            byte[] oldSizeBytes = BitConverter.GetBytes((uint)oldSize);
            byte[] newSizeBytes = BitConverter.GetBytes((uint)newSize);

            // Search for the old size pattern in the file
            for (int i = 0; i < uexpData.Length - 4; i++)
            {
                if (uexpData[i] == oldSizeBytes[0] &&
                    uexpData[i + 1] == oldSizeBytes[1] &&
                    uexpData[i + 2] == oldSizeBytes[2] &&
                    uexpData[i + 3] == oldSizeBytes[3])
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
