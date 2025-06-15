using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;

namespace PD3AudioModder
{
    public partial class SettingsWindow : Window
    {
        private readonly MainWindow? _mainWindow;
        private readonly WindowNotificationManager? _notificationManager;
        private readonly Dictionary<string, Control> _controls = new();

        public string Version { get; set; } = "Version: Unknown";
        public string FFmpegOptions { get; set; } = "-acodec pcm_s16le -ar 48000 -ac 2";

        public SettingsWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        public SettingsWindow(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            _notificationManager = new WindowNotificationManager(this);

            InitializeVersion();
            InitializeComponent();
            InitializeControls();
            LoadSettings();
            DataContext = this;

            this.FindControl<Slider>("VolumeSlider")!.Value = AppConfig.Instance.Volume;
        }

        private void OnVolumeChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is Slider slider && VolumeValueText != null)
            {
                VolumeValueText.Text = $"{(int)slider.Value}%";
                AppConfig.Instance.Volume = (int)slider.Value;
                AppConfig.Instance.Save();
            }
        }

        private void InitializeVersion()
        {
            var updater = new AutoUpdater(_notificationManager!, _mainWindow!);
            var currentVersion = updater.GetCurrentVersion();
            Version = currentVersion != "0.0.0" ? $"Version: {currentVersion}" : "Version: Unknown";
        }

        private void InitializeControls()
        {
            string[] controlNames =
            {
                "UpdateToggle",
                "AskUpdateToggle",
                "MuteNotificationSoundToggle",
                "ExportFolderTextBox",
                "UseExportFolderToggle",
                "DisplayFileExistToggle",
                "RepakPathTextBox",
                "SaveAESKeyToggle",
                "FFmpegOptionsTextBox",
                "FFmpegPathTextBox",
                "EnableDiscordRPCToggle",
                "DisplayCurrentTabToggle",
                "ShowModNameToggle",
                "MediaMappingsTextBox",
                "LocalizedMappingsTextBox",
            };
            foreach (var name in controlNames)
            {
                _controls[name] = this.FindControl<Control>(name)!;
            }

            InitializeEventHandlers();
        }

        private void InitializeEventHandlers()
        {
            foreach (
                var toggleName in new[]
                {
                    "UpdateToggle",
                    "AskUpdateToggle",
                    "MuteNotificationSoundToggle",
                    "UseExportFolderToggle",
                    "DisplayFileExistToggle",
                    "SaveAESKeyToggle",
                    "EnableDiscordRPCToggle",
                    "DisplayCurrentTabToggle",
                    "ShowModNameToggle",
                }
            )
            {
                if (_controls[toggleName] is ToggleSwitch toggle)
                    toggle.IsCheckedChanged += HandleToggleChanged;
            }
        }

        private void LoadSettings()
        {
            var config = AppConfig.Instance;
            foreach (
                var (key, value) in new Dictionary<string, object?>
                {
                    { "UpdateToggle", config.AutoUpdateEnabled },
                    { "AskUpdateToggle", config.AskToUpdate },
                    { "MuteNotificationSoundToggle", config.MuteNotificationSound },
                    { "ExportFolderTextBox", config.DefaultExportFolder },
                    { "UseExportFolderToggle", config.UseDefaultExportFolder },
                    { "DisplayFileExistToggle", config.DisplayFilesInExportWarning },
                    { "SaveAESKeyToggle", config.SaveAesKey },
                    { "RepakPathTextBox", config.RepakPath },
                    { "FFmpegOptionsTextBox", config.FfmpegOptions },
                    { "FFmpegPathTextBox", config.FfmpegPath },
                    { "EnableDiscordRPCToggle", config.RPCEnabled },
                    { "DisplayCurrentTabToggle", config.RPCDisplayTab },
                    { "ShowModNameToggle", config.RPCDisplayModName },
                    { "MediaMappingsTextBox", config.WwiseMediaMappingsURL },
                    { "LocalizedMappingsTextBox", config.WwiseLocalizedMappingsURL },
                }
            )
            {
                if (_controls.TryGetValue(key, out var control))
                {
                    switch (control)
                    {
                        case ToggleSwitch toggle:
                            toggle.IsChecked = (bool?)value;
                            break;
                        case TextBox textBox:
                            textBox.Text = (string?)value;
                            break;
                    }
                }
            }

            // Disable UseExportFolderToggle if ExportFolderTextBox is empty
            if (
                _controls.TryGetValue("UseExportFolderToggle", out var useExportFolderControl)
                && _controls.TryGetValue("ExportFolderTextBox", out var exportFolderControl)
                && exportFolderControl is TextBox exportFolderTextBox
                && useExportFolderControl is ToggleSwitch useExportFolderToggle
            )
            {
                useExportFolderToggle.IsEnabled = !string.IsNullOrWhiteSpace(
                    exportFolderTextBox.Text
                );
                useExportFolderToggle.IsChecked = !string.IsNullOrWhiteSpace(
                    exportFolderTextBox.Text
                );
            }
        }

        private void HandleToggleChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (
                sender is ToggleSwitch toggle
                && !string.IsNullOrEmpty(toggle.Name)
                && _controls.TryGetValue(toggle.Name, out _)
            )
            {
                var config = AppConfig.Instance;
                var condition = toggle.IsChecked ?? false;

                switch (toggle.Name)
                {
                    case "UpdateToggle":
                        config.AutoUpdateEnabled = condition;
                        break;
                    case "AskUpdateToggle":
                        config.AskToUpdate = condition;
                        break;
                    case "MuteNotificationSoundToggle":
                        config.MuteNotificationSound = condition;
                        break;
                    case "UseExportFolderToggle":
                        config.UseDefaultExportFolder = condition;
                        break;
                    case "DisplayFileExistToggle":
                        config.DisplayFilesInExportWarning = condition;
                        break;
                    case "SaveAESKeyToggle":
                        config.SaveAesKey = condition;
                        break;
                    case "EnableDiscordRPCToggle":
                        config.RPCEnabled = condition;
                        break;
                    case "DisplayCurrentTabToggle":
                        config.RPCDisplayTab = condition;
                        break;
                    case "ShowModNameToggle":
                        config.RPCDisplayModName = condition;
                        break;
                }
                config.Save();
            }
        }

        private async void HandleBrowseButtonClick(
            object? sender,
            Avalonia.Interactivity.RoutedEventArgs e
        )
        {
            if (
                sender is not Button button
                || !_controls.TryGetValue(
                    button.Name!.Replace("BrowseButton", "TextBox"),
                    out var control
                )
                || control is not TextBox textBox
            )
                return;

            string? result = button.Name.Contains("ExportFolder")
                ? await new OpenFolderDialog { Title = "Select Folder" }.ShowAsync(this)
                : (
                    await new OpenFileDialog
                    {
                        Title = "Select File",
                        AllowMultiple = false,
                        Filters = new List<FileDialogFilter>
                        {
                            new()
                            {
                                Name = "Executable",
                                Extensions = new List<string> { "exe" },
                            },
                        },
                    }.ShowAsync(this)
                )?[0];

            if (!string.IsNullOrEmpty(result))
            {
                textBox.Text = result;
                switch (button.Name)
                {
                    case "ExportFolderBrowseButton":
                        AppConfig.Instance.DefaultExportFolder = result;
                        break;
                    case "RepakPathBrowseButton":
                        AppConfig.Instance.RepakPath = result;
                        break;
                    case "FFmpegPathBrowseButton":
                        AppConfig.Instance.FfmpegPath = result;
                        break;
                }
                AppConfig.Instance.Save();
            }
        }

        private void HandleClearButtonClick(
            object? sender,
            Avalonia.Interactivity.RoutedEventArgs e
        )
        {
            if (sender is Button button)
            {
                // FFmpeg options reset button
                if (button.Name == "ResetFFmpegOptionsButton")
                {
                    if (
                        _controls.TryGetValue("FFmpegOptionsTextBox", out var ffmpegControl)
                        && ffmpegControl is TextBox ffmpegTextBox
                    )
                    {
                        ffmpegTextBox.Text = DefaultConfig.FfmpegOptions;
                        AppConfig.Instance.FfmpegOptions = DefaultConfig.FfmpegOptions;
                        AppConfig.Instance.Save();
                        return;
                    }
                }

                // Mapping URL reset buttons
                if (button.Name == "ResetMediaMappingsButton")
                {
                    if (
                        _controls.TryGetValue("MediaMappingsTextBox", out var mediaControl)
                        && mediaControl is TextBox mediaMappingsTextBox
                    )
                    {
                        mediaMappingsTextBox.Text = DefaultConfig.WwiseMediaMappingsURL;
                        AppConfig.Instance.WwiseMediaMappingsURL =
                            DefaultConfig.WwiseMediaMappingsURL;
                        AppConfig.Instance.Save();
                        return;
                    }
                }

                if (button.Name == "ResetLocalizedMappingsButton")
                {
                    if (
                        _controls.TryGetValue("LocalizedMappingsTextBox", out var localizedControl)
                        && localizedControl is TextBox localizedMappingsTextBox
                    )
                    {
                        localizedMappingsTextBox.Text = DefaultConfig.WwiseLocalizedMappingsURl;
                        AppConfig.Instance.WwiseLocalizedMappingsURL =
                            DefaultConfig.WwiseLocalizedMappingsURl;
                        AppConfig.Instance.Save();
                        return;
                    }
                }

                // Other buttons
                if (
                    _controls.TryGetValue(
                        button.Name!.Replace("ClearButton", "TextBox"),
                        out var textControl
                    ) && textControl is TextBox textBox
                )
                {
                    textBox.Text = string.Empty;
                    switch (button.Name)
                    {
                        case "ExportFolderClearButton":
                            AppConfig.Instance.DefaultExportFolder = string.Empty;
                            break;
                        case "RepakPathClearButton":
                            AppConfig.Instance.RepakPath = string.Empty;
                            break;
                        case "FFmpegPathClearButton":
                            AppConfig.Instance.FfmpegPath = string.Empty;
                            break;
                    }
                    AppConfig.Instance.Save();
                }
            }
        }

        private void HandleTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (textBox.Name == "FFmpegOptionsTextBox")
                {
                    AppConfig.Instance.FfmpegOptions = textBox.Text;
                }
                else if (
                    textBox.Name == "ExportFolderTextBox"
                    && _controls.TryGetValue(
                        "UseExportFolderToggle",
                        out var useExportFolderControl
                    )
                    && useExportFolderControl is ToggleSwitch useExportFolderToggle
                )
                {
                    useExportFolderToggle.IsEnabled = !string.IsNullOrWhiteSpace(textBox.Text);
                    useExportFolderToggle.IsChecked = !string.IsNullOrWhiteSpace(textBox.Text);
                }

                AppConfig.Instance.Save();
            }
        }

        private async void OnUpdateClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_notificationManager == null || _mainWindow == null)
                return;

            try
            {
                AutoUpdater updater = new AutoUpdater(_notificationManager, _mainWindow);
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
                else
                {
                    _notificationManager.Show(
                        new Notification(
                            "No updates available",
                            "You are already on the latest version.",
                            NotificationType.Information
                        )
                    );
                }
            }
            catch (Exception ex)
            {
                _notificationManager.Show(
                    new Notification(
                        "Update Error",
                        $"Failed to check for updates: {ex.Message}",
                        NotificationType.Error
                    )
                );
            }
        }

        private void ThemeEditorClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var themeEditorWindow = new ThemeEditor();
            themeEditorWindow.ShowDialog(this);
        }

        private void ThemeFolderClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            string appDataPath = Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData
            );
            string themesDirectory = Path.Combine(appDataPath, "PD3AudioModder", "Themes");

            if (!Directory.Exists(themesDirectory))
            {
                Directory.CreateDirectory(themesDirectory);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("explorer.exe", themesDirectory);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", themesDirectory);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", themesDirectory);
            }
            else
            {
                Console.WriteLine("Unable to open theme folder. Unsupported OS?");
            }
        }

        private void OnLicensesClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var licensesWindow = new LicensesWindow();
            licensesWindow.ShowDialog(this);
        }

        private void OnReportIssueClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = "https://github.com/Snoozeds/PD3AudioModder/issues/new",
                    UseShellExecute = true,
                }
            );
        }

        private void OnDonateClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = "https://ko-fi.com/snoozeds",
                    UseShellExecute = true,
                }
            );
        }

        private void OnBackClick(object sender, Avalonia.Interactivity.RoutedEventArgs e) =>
            Close();
    }
}
