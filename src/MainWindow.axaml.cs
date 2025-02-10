using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PD3AudioModder
{
    public partial class MainWindow : Window
    {
        private string? uploadedAudioPath;
        private string? uploadedUbulkPath;
        private string? uploadedUexpPath;
        private string? uploadedUassetPath;
        private string? uploadedJsonPath;
        private TextBlock? statusTextBlock;
        private readonly string tempDirectory;
        private Button? convertButton;

        private WindowNotificationManager? _notificationManager;
        private AppConfig _appConfig;

        public MainWindow()
        {
            _appConfig = AppConfig.Load();
            _notificationManager = new WindowNotificationManager(this);
            InitializeComponent();
            DataContext = this;

            // Create temp directory
            tempDirectory = Environment.OSVersion.Platform == PlatformID.Win32NT
                ? Path.Combine(Path.GetTempPath(), "PD3AudioModder")
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Temp");

            if (!Directory.Exists(tempDirectory))
            {
                Directory.CreateDirectory(tempDirectory);
            }

            // Check for updates if enabled
            if (_appConfig.AutoUpdateEnabled)
            {
                CheckForUpdatesAsync();
            }
        }

        private async void CheckForUpdatesAsync()
        {
            try
            {
                AutoUpdater updater = new AutoUpdater(_notificationManager!, this);
                var (updateAvailable, newVersion) = await updater.CheckForUpdates();

                if (updateAvailable)
                {
                    bool userWantsUpdate = await UpdateDialog.ShowDialogAsync(this, newVersion);
                    if (userWantsUpdate)
                    {
                        updater.DownloadUpdate();
                    }
                }
            }
            catch (Exception ex)
            {
                _notificationManager?.Show(new Notification(
                    "Update Error",
                    $"Failed to check for updates: {ex.Message}",
                    NotificationType.Error
                ));
            }
        }


        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            var uploadButton = this.FindControl<Button>("UploadButton")!;

            convertButton = this.FindControl<Button>("ConvertButton")!;
            // Start with the convert button disabled
            convertButton.IsEnabled = false;

            statusTextBlock = this.FindControl<TextBlock>("StatusTextBlock")!;

            uploadButton.Click += async (_, _) => await UploadFile();
            convertButton.Click += async (_, _) => await ProcessFiles();
        }

        private void UpdateStatus(string message)
        {
            if (statusTextBlock != null)
            {
                statusTextBlock.Text = $"Status: {message}";
            }
        }

        private void UpdateConvertButtonState()
        {
            // Check that the required file paths are not null.
            bool allFilesUploaded =
                   uploadedAudioPath != null
                && uploadedUbulkPath != null
                && uploadedUexpPath != null
                && uploadedJsonPath != null
                && uploadedUassetPath != null;

            if (convertButton != null)
            {
                convertButton.IsEnabled = allFilesUploaded;
            }
        }

        private async Task UploadFile()
        {
            UpdateStatus("Selecting files...");
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = true
            });

            if (files.Count > 0)
            {
                // Lists to hold the different file types
                List<string> ubulkFiles = new List<string>();
                List<string> uassetFiles = new List<string>();
                List<string> uexpFiles = new List<string>();
                List<string> jsonFiles = new List<string>();
                List<string> audioFiles = new List<string>();
                string? baseFileName = null;

                // Loop through the selected files and classify them by extension
                foreach (var file in files)
                {
                    string filePath = file.Path.LocalPath;
                    string extension = Path.GetExtension(filePath).ToLower();
                    string currentBaseFileName = Path.GetFileNameWithoutExtension(filePath).ToLower();

                    switch (extension)
                    {
                        case ".wav":
                        case ".mp3":
                        case ".ogg":
                        case ".flac":
                        case ".aiff":
                        case ".wma":
                        case ".m4a":
                        case ".aac":
                        case ".opus":
                            audioFiles.Add(filePath);
                            UpdateStatus($"Audio file uploaded: {Path.GetFileName(filePath)}");
                            break;
                        case ".ubulk":
                            ubulkFiles.Add(filePath);
                            baseFileName = currentBaseFileName;
                            break;
                        case ".uasset":
                            uassetFiles.Add(filePath);
                            break;
                        case ".uexp":
                            uexpFiles.Add(filePath);
                            break;
                        case ".json":
                            jsonFiles.Add(filePath);
                            break;
                        default:
                            UpdateStatus($"Unsupported file type: {extension}");
                            break;
                    }
                }

                // Check if files have same base filename
                bool allFilesMatchBaseName = true;
                foreach (var fileList in new List<List<string>> { ubulkFiles, uassetFiles, uexpFiles, jsonFiles })
                {
                    foreach (var filePath in fileList)
                    {
                        string currentBaseFileName = Path.GetFileNameWithoutExtension(filePath).ToLower();
                        if (currentBaseFileName != baseFileName)
                        {
                            allFilesMatchBaseName = false;
                            break;
                        }
                    }
                    if (!allFilesMatchBaseName)
                    {
                        break;
                    }
                }

                // If files have same base name, assign the paths
                if (allFilesMatchBaseName)
                {
                    if (audioFiles.Count > 0)
                    {
                        uploadedAudioPath = audioFiles[0];
                        UpdateStatus($"Audio file uploaded: {Path.GetFileName(audioFiles[0])}");
                    }

                    // Assign paths for .ubulk, .uasset, .uexp, and .json files
                    foreach (var file in files)
                    {
                        string filePath = file.Path.LocalPath;
                        string extension = Path.GetExtension(filePath).ToLower();

                        switch (extension)
                        {
                            case ".ubulk":
                                uploadedUbulkPath = filePath;
                                UpdateStatus($"Ubulk file uploaded: {Path.GetFileName(filePath)}");
                                break;
                            case ".uexp":
                                uploadedUexpPath = filePath;
                                UpdateStatus($"Uexp file uploaded: {Path.GetFileName(filePath)}");
                                break;
                            case ".uasset":
                                uploadedUassetPath = filePath;
                                UpdateStatus($"Uasset file uploaded: {Path.GetFileName(filePath)}");
                                break;
                            case ".json":
                                uploadedJsonPath = filePath;
                                UpdateStatus($"Json file uploaded: {Path.GetFileName(filePath)}");
                                break;
                        }
                    }
                }
                else
                {
                    UpdateStatus("Error: .ubulk, .uasset, .uexp, and .json files must share the same base filename (excluding extensions).");
                }

                UpdateConvertButtonState();
            }
            else
            {
                UpdateStatus("File selection cancelled");
            }
        }

        private async Task ProcessFiles()
        {
            if (uploadedAudioPath == null || uploadedUbulkPath == null || uploadedUexpPath == null ||
                (uploadedJsonPath == null && uploadedUassetPath == null))
            {
                UpdateStatus("Error: Please upload all required files first");
                return;
            }

            try
            {
                // Copy the original ubulk file to temp directory
                string tempUbulkPath = Path.Combine(tempDirectory, Path.GetFileName(uploadedUbulkPath));
                File.Copy(uploadedUbulkPath, tempUbulkPath, true);
                UpdateStatus("Created backup of original ubulk file...");

                // Copy the original uexp file to temp directory
                string tempUexpPath = Path.Combine(tempDirectory, Path.GetFileName(uploadedUexpPath));
                File.Copy(uploadedUexpPath, tempUexpPath, true);
                UpdateStatus("Created backup of original uexp file...");

                // Convert audio to WAV using MediaFoundation
                UpdateStatus("Converting audio to WAV format...");
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
                UpdateStatus("Converting WAV to WEM format...");
                string wemPath = Path.Combine(tempDirectory, Path.GetFileNameWithoutExtension(wavPath) + ".wem");
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

                // Ask user where to save the files
                var folderResult = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Choose where to save converted files",
                    DefaultExtension = "",
                    ShowOverwritePrompt = true,
                    SuggestedFileName = Path.GetFileNameWithoutExtension(uploadedUbulkPath)
                });

                if (folderResult != null)
                {
                    string saveDirectory = Path.GetDirectoryName(folderResult.Path.LocalPath)!;
                    string baseFileName = Path.GetFileNameWithoutExtension(folderResult.Path.LocalPath);

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
                        string uassetSavePath = Path.Combine(saveDirectory, baseFileName + ".uasset");
                        File.Copy(uploadedUassetPath, uassetSavePath, true);
                    }
                    else if (uploadedJsonPath != null)
                    {
                        string jsonSavePath = Path.Combine(saveDirectory, baseFileName + ".json");
                        File.Copy(uploadedJsonPath, jsonSavePath, true);
                    }

                    UpdateStatus($"Conversion completed successfully! Files saved to {saveDirectory}");
                }
                else
                {
                    UpdateStatus("Save operation cancelled");
                }

                // Clean up temp files
                try
                {
                    if (File.Exists(wavPath)) File.Delete(wavPath);
                    if (File.Exists(wemPath)) File.Delete(wemPath);
                    if (File.Exists(tempUbulkPath)) File.Delete(tempUbulkPath);
                    if (File.Exists(tempUexpPath)) File.Delete(tempUexpPath);

                    uploadedAudioPath = null;
                    uploadedUbulkPath = null;
                    uploadedUexpPath = null;
                    uploadedUassetPath = null;
                    uploadedJsonPath = null;

                    UpdateConvertButtonState();
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}");
            }
        }

        private void CopyToExport(string sourcePath, string exportFolder)
        {
            if (File.Exists(sourcePath))
            {
                string fileName = Path.GetFileName(sourcePath);
                string destPath = Path.Combine(exportFolder, fileName);
                File.Copy(sourcePath, destPath, true);
            }
        }

        private long GetOldSizeFromJson(string jsonFile)
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

        private void ModifyUexpSize(string uexpFilePath, long oldSize, long newSize)
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

        private void OnSettingsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(this);
            settingsWindow.ShowDialog(this);
        }
    }
}
