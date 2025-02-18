using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using PD3AudioModder.util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PD3AudioModder
{
    public partial class MainWindow : Window
    {
        // Default folder to save converted files to
        public string? defaultExportFolder { get; set; }

        // Single file operation
        private string? uploadedAudioPath;
        private string? uploadedUbulkPath;
        private string? uploadedUexpPath;
        private string? uploadedUassetPath;
        private string? uploadedJsonPath;
        private CheckBox? useExportFolderCheckBox;
        private TextBlock? statusTextBlock;
        private Button? convertButton;

        private readonly string tempDirectory;
        private readonly WindowNotificationManager? _notificationManager;
        private readonly AppConfig _appConfig;
        private readonly FileProcessor _fileProcessor;

        // Batch file operation
        private readonly BatchProcessor _batchProcessor;
        private Button? selectAudioFolderButton;
        private Button? selectGameFilesFolderButton;
        private Button? batchConvertButton;
        private CheckBox? batchUseExportFolderCheckBox;
        private TextBlock? batchStatusTextBlock;
        private ProgressBar? batchProgressBar;

        // Pack Files tab
        private string? ModName;
        private bool? CompressionEnabled;
        private string? PackFolderPath;

        public MainWindow()
        {
            _appConfig = AppConfig.Load();
            _notificationManager = new WindowNotificationManager(this);
            _fileProcessor = new FileProcessor();

            _batchProcessor = new BatchProcessor();

            if (String.IsNullOrEmpty(_appConfig.DefaultExportFolder) || _appConfig.DefaultExportFolder == "null")
            {
                defaultExportFolder = "Not set, change in settings.";
            }
            else
            {
                defaultExportFolder = _appConfig.DefaultExportFolder;
            }

            DataContext = this;
            InitializeComponent();

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
                    string currentVersion = updater.GetCurrentVersion();
                    bool userWantsUpdate = await UpdateDialog.ShowDialogAsync(this, currentVersion, newVersion);
                    if (userWantsUpdate)
                    {
                        await updater.DownloadUpdate();
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

            // Single file operation
            var uploadButton = this.FindControl<Button>("UploadButton")!;
            convertButton = this.FindControl<Button>("ConvertButton")!;
            convertButton.IsEnabled = false;// Start with the convert button disabled
            useExportFolderCheckBox = this.FindControl<CheckBox>("UseExportFolder")!;
            useExportFolderCheckBox.IsEnabled = defaultExportFolder != "Not set, change in settings.";
            useExportFolderCheckBox.IsChecked = useExportFolderCheckBox.IsEnabled;
            statusTextBlock = this.FindControl<TextBlock>("StatusTextBlock")!;

            uploadButton.Click += async (_, _) => await UploadFile();
            convertButton.Click += async (_, _) => await _fileProcessor.ProcessFiles(uploadedAudioPath!, uploadedUbulkPath!, uploadedUexpPath!, uploadedUassetPath!, uploadedJsonPath!, tempDirectory, useExportFolderCheckBox.IsChecked ?? false, statusTextBlock!, convertButton!, this);

            // Batch file operation
            selectAudioFolderButton = this.FindControl<Button>("SelectAudioFolderButton")!;
            selectGameFilesFolderButton = this.FindControl<Button>("SelectGameFilesFolderButton")!;
            batchConvertButton = this.FindControl<Button>("BatchConvertButton")!;
            batchConvertButton.IsEnabled = false;// Start with the batch convert button disabled
            batchUseExportFolderCheckBox = this.FindControl<CheckBox>("BatchUseExportFolder")!;
            batchUseExportFolderCheckBox.IsEnabled = defaultExportFolder != "Not set, change in settings.";
            batchUseExportFolderCheckBox.IsChecked = batchUseExportFolderCheckBox.IsEnabled;
            batchStatusTextBlock = this.FindControl<TextBlock>("BatchStatusTextBlock")!;
            batchProgressBar = this.FindControl<ProgressBar>("BatchProgressBar")!;

            selectAudioFolderButton.Click += async (_, _) => await SelectAudioFolder();
            selectGameFilesFolderButton.Click += async (_, _) => await SelectGameFilesFolder();
            batchConvertButton.Click += async (_, _) => await _batchProcessor.ProcessBatch(
                tempDirectory,
                batchUseExportFolderCheckBox.IsChecked ?? false,
                batchStatusTextBlock!,
                batchProgressBar!,
                batchConvertButton!,
                this
            );

            // Pack Files tab
            var repakPathTextBlock = this.FindControl<TextBlock>("RepakPathTextBlock")!;
            var selectRepakButton = this.FindControl<Button>("SelectRepakButton")!;
            var ModNameTextBox = this.FindControl<TextBox>("ModNameTextBox")!;
            var selectFolderButton = this.FindControl<Button>("SelectFolderButton")!;
            var compressCheckBox = this.FindControl<CheckBox>("CompressCheckBox")!;
            var packButton = this.FindControl<Button>("PackButton")!;

            if (_appConfig.RepakPath != null)
            {
                repakPathTextBlock.Text = "Repak path: " + _appConfig.RepakPath;
            }

            ModNameTextBox.TextChanged += (_, _) =>
            {
                if (!string.IsNullOrEmpty(ModNameTextBox.Text))
                {
                    ModName = ModNameTextBox.Text;
                }
                else
                {
                    ModName = "MyPD3Mod";
                }
            };
            selectRepakButton.Click += (_, _) => SelectRepakButton_Click(repakPathTextBlock);
            compressCheckBox.IsCheckedChanged += (_, _) => CompressionEnabled = compressCheckBox.IsChecked;
            selectFolderButton.Click += (_, _) => SelectFolderButton_Click(packButton);
            packButton.Click += (_, _) => PackButton_Click(PackFolderPath!);
        }

        public void UpdateExportFolderCheckboxes()
        {
            useExportFolderCheckBox!.IsEnabled = defaultExportFolder != "Not set, change in settings.";
            useExportFolderCheckBox.IsChecked = useExportFolderCheckBox.IsEnabled;

            batchUseExportFolderCheckBox!.IsEnabled = defaultExportFolder != "Not set, change in settings.";
            batchUseExportFolderCheckBox.IsChecked = batchUseExportFolderCheckBox.IsEnabled;
        }

        private async Task UploadFile()
        {
            _fileProcessor.UpdateStatus("Selecting files...", StatusTextBlock);
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
                            _fileProcessor.UpdateStatus($"Audio file uploaded: {Path.GetFileName(filePath)}", StatusTextBlock);
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
                            _fileProcessor.UpdateStatus($"Unsupported file type: {extension}", StatusTextBlock);
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
                        _fileProcessor.UpdateStatus($"Audio file uploaded: {Path.GetFileName(audioFiles[0])}", StatusTextBlock);
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
                                _fileProcessor.UpdateStatus($"Ubulk file uploaded: {Path.GetFileName(filePath)}", StatusTextBlock);
                                break;
                            case ".uexp":
                                uploadedUexpPath = filePath;
                                _fileProcessor.UpdateStatus($"Uexp file uploaded: {Path.GetFileName(filePath)}", StatusTextBlock);
                                break;
                            case ".uasset":
                                uploadedUassetPath = filePath;
                                _fileProcessor.UpdateStatus($"Uasset file uploaded: {Path.GetFileName(filePath)}", StatusTextBlock);
                                break;
                            case ".json":
                                uploadedJsonPath = filePath;
                                _fileProcessor.UpdateStatus($"Json file uploaded: {Path.GetFileName(filePath)}", StatusTextBlock);
                                break;
                        }
                    }
                }
                else
                {
                    _fileProcessor.UpdateStatus("Error: .ubulk, .uasset, .uexp, and .json files must share the same base filename (excluding extensions).", StatusTextBlock);
                }

                _fileProcessor.UpdateConvertButtonState(uploadedAudioPath!, uploadedUbulkPath!, uploadedUexpPath!, uploadedUassetPath!, uploadedJsonPath!, convertButton!);
            }
            else
            {
                _fileProcessor.UpdateStatus("File selection cancelled", StatusTextBlock);
            }
        }

        private async Task SelectAudioFolder()
        {
            var folder = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Audio Files Folder",
                AllowMultiple = false
            });

            if (folder.Count > 0)
            {
                _batchProcessor.SetAudioFolderPath(folder[0].Path.LocalPath);
                _batchProcessor.UpdateStatus($"Audio folder selected: {folder[0].Name}", batchStatusTextBlock!);
                _batchProcessor.UpdateButtonStates(folder[0].Path.LocalPath, "", batchConvertButton!);
            }
        }

        private async Task SelectGameFilesFolder()
        {
            var folder = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Game Files Folder",
                AllowMultiple = false
            });

            if (folder.Count > 0)
            {
                _batchProcessor.SetGameFilesFolderPath(folder[0].Path.LocalPath);
                _batchProcessor.UpdateStatus($"Game files folder selected: {folder[0].Name}", batchStatusTextBlock!);
                _batchProcessor.UpdateButtonStates("", folder[0].Path.LocalPath, batchConvertButton!);
            }
        }

        // Pack files tab
        private async void SelectRepakButton_Click(TextBlock repakPathTextBlock)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select repak.exe",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
        {
            new FilePickerFileType("Executable")
            {
                Patterns = new List<string> { "*.exe" }
            }
        }
            });

            if (files.Count > 0)
            {
                repakPathTextBlock.Text = "Repak path: " + files[0].Path.LocalPath;
                _appConfig.RepakPath = files[0].Path.LocalPath;
                _appConfig.Save();
            }
        }

        private async void SelectFolderButton_Click(Button packButton)
        {
            var folder = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select folder to pack"
            });

            if (folder.Count > 0)
            {
                PackFolderPath = folder[0].Path.LocalPath;
                packButton.IsEnabled = true;
            }
        }

        private async void PackButton_Click(string packFolderPath)
        {
            await PackFiles.DownloadMappings();

            if (_appConfig.RepakPath == null)
            {
                _notificationManager?.Show(new Notification(
                    "Error",
                    "Please select the repak.exe path.",
                    NotificationType.Error
                ));
                return;
            }

            PackFiles.Pack(_appConfig.RepakPath, CompressionEnabled ?? false, packFolderPath, Path.GetDirectoryName(packFolderPath)!, ModName ?? "MyPD3Mod");
            await PackFiles.Repak(_appConfig.RepakPath, CompressionEnabled ?? false, packFolderPath, ModName ?? "MyPD3Mod");

            _notificationManager?.Show(new Notification(
                "Success",
                "Files packed successfully.",
                NotificationType.Success
            ));

            System.Diagnostics.Process.Start("explorer.exe", Path.GetDirectoryName(packFolderPath)!);

            // Reset fields
            PackFolderPath = null;
            var packButton = this.FindControl<Button>("PackButton")!;
            packButton.IsEnabled = false;
        }

        private void OnHelpClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var tabControl = this.FindControl<TabControl>("MainTabControl");

            // Determine which tab is selected
            var selectedTab = ((TabItem)tabControl!.SelectedItem!).Header!.ToString();
            string activeTab = selectedTab switch
            {
                "Single File" => "SingleFile",
                "Batch Files" => "BatchConversion",
                "Pack Files" => "PackFiles",
                _ => "single"
            };

            // Show help window with the active tab
            var helpWindow = new HelpWindow(activeTab);
            helpWindow.ShowDialog(this);
        }

        private void OnSettingsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(this);
            settingsWindow.ShowDialog(this);
        }

    }
}
