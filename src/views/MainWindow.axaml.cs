using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using PD3AudioModder.util;

namespace PD3AudioModder
{
    // Used in ID Search tab
    public class SoundItem
    {
        public string SoundId { get; set; }
        public string SoundDescription { get; set; }
        public string SoundFolder { get; set; }
        public string UbulkPath { get; set; }
        public ICommand PlayCommand { get; set; }
        public ICommand StopCommand { get; set; }

        private readonly WindowNotificationManager _notificationManager;
        private readonly IDSearcher _idSearcher;

        public SoundItem(WindowNotificationManager notificationManager, IDSearcher idSearcher)
        {
            _notificationManager = notificationManager;
            _idSearcher = idSearcher;

            PlayCommand = new RelayCommand<SoundItem>(PlaySound);
            StopCommand = new RelayCommand<object>(_ => StopSound());
        }

        private void PlaySound(SoundItem soundItem)
        {
            _idSearcher.PlaySound(soundItem);
        }

        private void StopSound()
        {
            _idSearcher.StopAudio();
        }
    }

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
        private TextBlock? statusTextBlock = null;
        private Button? convertButton;

        private readonly string tempDirectory;
        public readonly WindowNotificationManager? _notificationManager;
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
        public string? ModName;
        private bool? CompressionEnabled;
        private string? PackFolderPath;

        // Status text
        public TextBlock? globalStatusTextBlock;
        public string currentTab = "Single File";

        // Discord RPC
        private readonly DiscordRPC _discordRPC;

        // ID Search tab
        public ObservableCollection<SoundItem> _soundItems = new ObservableCollection<SoundItem>();
        public ObservableCollection<SoundItem> SoundItems => _soundItems;
        public string pd3InstallFolder;
        public DataGrid? soundsDataGrid;

        public MainWindow()
        {
            _appConfig = AppConfig.Load();
            _notificationManager = new WindowNotificationManager(this);
            _fileProcessor = new FileProcessor(this);
            _batchProcessor = new BatchProcessor(this);

            if (
                String.IsNullOrEmpty(_appConfig.DefaultExportFolder)
                || _appConfig.DefaultExportFolder == "null"
            )
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
            tempDirectory =
                Environment.OSVersion.Platform == PlatformID.Win32NT
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

            // Discord RPC
            _discordRPC = new DiscordRPC();
            _discordRPC.Initialize();

            PackFiles.Initialize(this);
        }

        private async void CheckForUpdatesAsync()
        {
            try
            {
                AutoUpdater updater = new AutoUpdater(new WindowNotificationManager(this), this);
                var (updateAvailable, newVersion) = await updater.CheckForUpdates();

                if (updateAvailable)
                {
                    string currentVersion = updater.GetCurrentVersion();
                    bool userWantsUpdate;

                    if (
                        AppConfig.Instance.AskToUpdate == true
                        || AppConfig.Instance.AskToUpdate == null
                    )
                    {
                        userWantsUpdate = await UpdateDialog.ShowDialogAsync(
                            this,
                            currentVersion,
                            newVersion
                        );

                        if (userWantsUpdate)
                        {
                            await updater.DownloadUpdate();
                        }
                    }
                    else if (AppConfig.Instance.AskToUpdate == false)
                    {
                        await updater.DownloadUpdate();
                    }
                }
            }
            catch (Exception ex)
            {
                _notificationManager?.Show(
                    new Notification(
                        "Update Error",
                        $"Failed to check for updates: {ex.Message}",
                        NotificationType.Error
                    )
                );
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            // Single file operation
            var uploadButton = this.FindControl<Button>("UploadButton")!;
            convertButton = this.FindControl<Button>("ConvertButton")!;
            convertButton.IsEnabled = false; // Start with the convert button disabled
            useExportFolderCheckBox = this.FindControl<CheckBox>("UseExportFolder")!;
            useExportFolderCheckBox.IsEnabled =
                defaultExportFolder != "Not set, change in settings.";
            useExportFolderCheckBox.IsChecked =
                useExportFolderCheckBox.IsEnabled && _appConfig.UseDefaultExportFolder == true;

            uploadButton.Click += async (_, _) => await UploadFile();
            convertButton.Click += async (_, _) =>
                await _fileProcessor.ProcessFiles(
                    uploadedAudioPath!,
                    uploadedUbulkPath!,
                    uploadedUexpPath!,
                    uploadedUassetPath!,
                    uploadedJsonPath!,
                    tempDirectory,
                    useExportFolderCheckBox.IsChecked ?? false,
                    statusTextBlock!,
                    convertButton!,
                    this
                );

            // Batch file operation
            selectAudioFolderButton = this.FindControl<Button>("SelectAudioFolderButton")!;
            selectGameFilesFolderButton = this.FindControl<Button>("SelectGameFilesFolderButton")!;
            batchConvertButton = this.FindControl<Button>("BatchConvertButton")!;
            batchConvertButton.IsEnabled = false; // Start with the batch convert button disabled
            batchUseExportFolderCheckBox = this.FindControl<CheckBox>("BatchUseExportFolder")!;
            batchUseExportFolderCheckBox.IsEnabled =
                defaultExportFolder != "Not set, change in settings.";
            batchUseExportFolderCheckBox.IsChecked =
                useExportFolderCheckBox.IsEnabled && _appConfig.UseDefaultExportFolder == true;
            batchStatusTextBlock = this.FindControl<TextBlock>("BatchStatusTextBlock")!;
            batchProgressBar = this.FindControl<ProgressBar>("BatchProgressBar")!;

            selectAudioFolderButton.Click += async (_, _) => await SelectAudioFolder();
            selectGameFilesFolderButton.Click += async (_, _) => await SelectGameFilesFolder();
            batchConvertButton.Click += async (_, _) =>
                await _batchProcessor.ProcessBatch(
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

            var mainTabControl = this.FindControl<TabControl>("MainTabControl")!;
            ModNameTextBox.TextChanged += (_, _) =>
            {
                ModName = !string.IsNullOrEmpty(ModNameTextBox.Text)
                    ? ModNameTextBox.Text
                    : "MyPD3Mod";
                _discordRPC.UpdatePresence(mainTabControl, ModName);
            };
            selectRepakButton.Click += (_, _) => SelectRepakButton_Click(repakPathTextBlock);
            compressCheckBox.IsCheckedChanged += (_, _) =>
                CompressionEnabled = compressCheckBox.IsChecked;
            selectFolderButton.Click += (_, _) => SelectFolderButton_Click(packButton);
            packButton.Click += (_, _) => PackButton_Click(PackFolderPath!);

            // ID Search Tab
            soundsDataGrid = this.FindControl<DataGrid>("SoundsDataGrid");
            var searchButton = this.FindControl<Button>("SearchButton")!;
            var searchTextBox = this.FindControl<TextBox>("SearchTextBox")!;
            searchButton.Click += (_, _) => PerformSearch(searchTextBox.Text);

            searchTextBox.KeyDown += (s, e) =>
            {
                if (e.Key == Avalonia.Input.Key.Enter)
                    PerformSearch(searchTextBox.Text);
            };

            globalStatusTextBlock = this.FindControl<TextBlock>("StatusTextBlock")!;
        }

        public void UpdateExportFolderCheckboxes()
        {
            bool isExportFolderSet = defaultExportFolder != "Not set, change in settings.";
            bool useDefaultExport = AppConfig.Instance.UseDefaultExportFolder == true;

            if (useExportFolderCheckBox != null)
            {
                useExportFolderCheckBox.IsEnabled = isExportFolderSet;
                useExportFolderCheckBox.IsChecked = isExportFolderSet && useDefaultExport;
            }

            if (batchUseExportFolderCheckBox != null)
            {
                batchUseExportFolderCheckBox.IsEnabled = isExportFolderSet;
                batchUseExportFolderCheckBox.IsChecked = isExportFolderSet && useDefaultExport;
            }
        }

        private async Task UploadFile()
        {
            _fileProcessor.UpdateStatus("Selecting files...");
            var files = await StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions { AllowMultiple = true }
            );

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
                    string currentBaseFileName = Path.GetFileNameWithoutExtension(filePath)
                        .ToLower();

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
                            _fileProcessor.UpdateStatus(
                                $"Audio file uploaded: {Path.GetFileName(filePath)}"
                            );
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
                            _fileProcessor.UpdateStatus($"Unsupported file type: {extension}");
                            break;
                    }
                }

                // Check if files have same base filename
                bool allFilesMatchBaseName = true;
                foreach (
                    var fileList in new List<List<string>>
                    {
                        ubulkFiles,
                        uassetFiles,
                        uexpFiles,
                        jsonFiles,
                    }
                )
                {
                    foreach (var filePath in fileList)
                    {
                        string currentBaseFileName = Path.GetFileNameWithoutExtension(filePath)
                            .ToLower();
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
                        _fileProcessor.UpdateStatus(
                            $"Audio file uploaded: {Path.GetFileName(audioFiles[0])}"
                        );
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
                                _fileProcessor.UpdateStatus(
                                    $"Ubulk file uploaded: {Path.GetFileName(filePath)}"
                                );
                                break;
                            case ".uexp":
                                uploadedUexpPath = filePath;
                                _fileProcessor.UpdateStatus(
                                    $"Uexp file uploaded: {Path.GetFileName(filePath)}"
                                );
                                break;
                            case ".uasset":
                                uploadedUassetPath = filePath;
                                _fileProcessor.UpdateStatus(
                                    $"Uasset file uploaded: {Path.GetFileName(filePath)}"
                                );
                                break;
                            case ".json":
                                uploadedJsonPath = filePath;
                                _fileProcessor.UpdateStatus(
                                    $"Json file uploaded: {Path.GetFileName(filePath)}"
                                );
                                break;
                        }
                    }
                }
                else
                {
                    _fileProcessor.UpdateStatus(
                        "Error: .ubulk, .uasset, .uexp, and .json files must share the same base filename (excluding extensions)."
                    );
                }

                _fileProcessor.UpdateConvertButtonState(
                    uploadedAudioPath!,
                    uploadedUbulkPath!,
                    uploadedUexpPath!,
                    uploadedUassetPath!,
                    uploadedJsonPath!,
                    convertButton!
                );
            }
            else
            {
                _fileProcessor.UpdateStatus("File selection cancelled");
            }
        }

        private async Task SelectAudioFolder()
        {
            var folder = await StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions
                {
                    Title = "Select Audio Files Folder",
                    AllowMultiple = false,
                }
            );

            if (folder.Count > 0)
            {
                _batchProcessor.SetAudioFolderPath(folder[0].Path.LocalPath);
                _batchProcessor.UpdateStatus(
                    $"Audio folder selected: {folder[0].Name}",
                    batchStatusTextBlock!
                );
                _batchProcessor.UpdateButtonStates(
                    folder[0].Path.LocalPath,
                    "",
                    batchConvertButton!
                );
            }
        }

        private async Task SelectGameFilesFolder()
        {
            var folder = await StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions
                {
                    Title = "Select Game Files Folder",
                    AllowMultiple = false,
                }
            );

            if (folder.Count > 0)
            {
                _batchProcessor.SetGameFilesFolderPath(folder[0].Path.LocalPath);
                _batchProcessor.UpdateStatus(
                    $"Game files folder selected: {folder[0].Name}",
                    batchStatusTextBlock!
                );
                _batchProcessor.UpdateButtonStates(
                    "",
                    folder[0].Path.LocalPath,
                    batchConvertButton!
                );
            }
        }

        // Pack files tab
        private async void SelectRepakButton_Click(TextBlock repakPathTextBlock)
        {
            var files = await StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "Select repak.exe",
                    AllowMultiple = false,
                    FileTypeFilter = new List<FilePickerFileType>
                    {
                        new FilePickerFileType("Executable")
                        {
                            Patterns = new List<string> { "*.exe" },
                        },
                    },
                }
            );

            if (files.Count > 0)
            {
                repakPathTextBlock.Text = "Repak path: " + files[0].Path.LocalPath;
                _appConfig.RepakPath = files[0].Path.LocalPath;
                _appConfig.Save();
            }
        }

        private async void SelectFolderButton_Click(Button packButton)
        {
            var folder = await StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions { Title = "Select folder to pack" }
            );

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
                _notificationManager?.Show(
                    new Notification(
                        "Error",
                        "Please select the repak.exe path.",
                        NotificationType.Error
                    )
                );
                return;
            }

            PackFiles.Pack(
                _appConfig.RepakPath,
                CompressionEnabled ?? false,
                packFolderPath,
                Path.GetDirectoryName(packFolderPath)!,
                ModName ?? "MyPD3Mod"
            );
            await PackFiles.Repak(
                _appConfig.RepakPath,
                CompressionEnabled ?? false,
                packFolderPath,
                ModName ?? "MyPD3Mod"
            );

            _notificationManager?.Show(
                new Notification("Success", "Files packed successfully.", NotificationType.Success)
            );

            System.Diagnostics.Process.Start(
                "explorer.exe",
                Path.GetDirectoryName(packFolderPath)!
            );

            // Reset fields
            PackFolderPath = null;
            var packButton = this.FindControl<Button>("PackButton")!;
            packButton.IsEnabled = false;
        }

        // ID Search tab
        private void PerformSearch(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                // Reset to show all items
                soundsDataGrid!.ItemsSource = _soundItems;
                UpdateGlobalStatus($"Showing all {_soundItems.Count} sound files", "ID Search");
                return;
            }

            searchText = searchText.Trim().ToLowerInvariant();

            // Collection based on search text
            var filteredItems = _soundItems
                .Where(item =>
                    item.SoundId.ToLowerInvariant().Contains(searchText)
                    || item.SoundDescription.ToLowerInvariant().Contains(searchText)
                )
                .ToList();

            soundsDataGrid!.ItemsSource = filteredItems;
            UpdateGlobalStatus(
                $"Found {filteredItems.Count} results for '{searchText}'",
                "ID Search"
            );
        }

        private async void OnLoadPakFilesClick(
            object sender,
            Avalonia.Interactivity.RoutedEventArgs e
        )
        {
            try
            {
                var window = (Window)((Control)this).GetVisualRoot()!;
                var storageProvider = window.StorageProvider;
                var fileOptions = new FilePickerOpenOptions
                {
                    Title = "Select .pak Files",
                    AllowMultiple = true,
                    FileTypeFilter = new FilePickerFileType[]
                    {
                        new FilePickerFileType("Unreal Engine Pak Files")
                        {
                            Patterns = new[] { "*.pak" },
                            MimeTypes = new[] { "application/octet-stream" },
                        },
                    },
                };
                var files = await storageProvider.OpenFilePickerAsync(fileOptions);
                if (files != null && files.Count > 0)
                {
                    // Process selected .pak files
                    await IDSearcher.ProcessPakFiles(files, this);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening file dialog: {ex.Message}");
            }
        }

        // Status
        public void UpdateGlobalStatus(string message, string sourceTab)
        {
            if (currentTab == sourceTab && globalStatusTextBlock != null)
            {
                globalStatusTextBlock.Text = message;
            }
        }

        private void OnHelpClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var tabControl = this.FindControl<TabControl>("MainTabControl");

            // Determine which tab is selected
            var selectedTab = ((TabItem)tabControl!.SelectedItem!).Header!.ToString();
            string activeTab = selectedTab switch
            {
                "Single File" => "SingleFile",
                "Batch Conversion" => "BatchConversion",
                "Pack Files" => "PackFiles",
                "ID Search" => "IDSearch",
                _ => "single",
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

        // Toggle the hamburger menu
        private void OnHamburgerButtonClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var splitView = this.FindControl<Avalonia.Controls.SplitView>("MainSplitView");
            splitView!.IsPaneOpen = !splitView.IsPaneOpen;
        }

        // Handle clicks on menu items to switch tabs
        private void OnMenuItemClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is object tag)
            {
                int tabIndex = Convert.ToInt32(tag);
                var tabControl = this.FindControl<TabControl>("MainTabControl");

                if (tabControl != null && tabIndex < tabControl.Items.Count)
                {
                    tabControl.SelectedIndex = tabIndex;

                    // Update the current tab for status text
                    if (
                        tabControl.SelectedItem is TabItem tabItem
                        && tabItem.Header is string header
                    )
                    {
                        currentTab = header;

                        // Update global status based on current tab
                        switch (currentTab)
                        {
                            case "Single File":
                                globalStatusTextBlock!.Text = "Status: Waiting for input...";
                                break;
                            case "Batch Conversion":
                                globalStatusTextBlock!.Text = "Status: Waiting for input...";
                                break;
                            case "Pack Files":
                                globalStatusTextBlock!.Text = "Status: Ready to pack files...";
                                break;
                            case "ID Search":
                                globalStatusTextBlock!.Text = "Status: Ready to search IDs...";
                                break;
                        }

                        // Update Discord RPC
                        if (!String.IsNullOrEmpty(ModName))
                        {
                            _discordRPC.UpdatePresence(tabControl, ModName);
                        }
                    }

                    // Auto collapse menu when in compact mode
                    var splitView = this.FindControl<Avalonia.Controls.SplitView>("MainSplitView");
                    if (
                        splitView != null
                        && splitView.DisplayMode
                            == Avalonia.Controls.SplitViewDisplayMode.CompactOverlay
                    )
                    {
                        splitView.IsPaneOpen = false;
                    }
                }
            }
        }

        private void OnWindowClosing(object sender, WindowClosingEventArgs e)
        {
            _discordRPC.Dispose();
        }
    }
}
