using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace PD3AudioModder
{
    public partial class SettingsWindow : Window
    {
        private MainWindow? _mainWindow;
        private WindowNotificationManager? _notificationManager;
        private ToggleSwitch? _updateToggle;
        private ToggleSwitch? _muteNotificationSoundToggle;
        private TextBox? _exportFolderTextBox;
        private ToggleSwitch? _useExportFolderToggle;
        private TextBox? _repakPathTextBox;
        private TextBox? _ffmpegOptionsTextBox;

        public string? Version { get; set; }

        public SettingsWindow()
        {
            InitializeComponent();
        }

        public SettingsWindow(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            _notificationManager = new WindowNotificationManager(this);
            AutoUpdater updater = new AutoUpdater(_notificationManager!, _mainWindow);

            if (updater.GetCurrentVersion() != "0.0.0")
            {
                Version = "Version: " + updater.GetCurrentVersion();
            }
            else
            {
                Version = "Version: Unknown";
            }

            InitializeComponent();

            InitializeControls();
            LoadSettings();

            DataContext = this;
        }

    private void InitializeControls()
        {
            _updateToggle = this.FindControl<ToggleSwitch>("UpdateToggle");
            _muteNotificationSoundToggle = this.FindControl<ToggleSwitch>("MuteNotificationSoundToggle");
            _exportFolderTextBox = this.FindControl<TextBox>("ExportFolderTextBox")!;
            _useExportFolderToggle = this.FindControl<ToggleSwitch>("UseExportFolderToggle");

            if (String.IsNullOrEmpty(AppConfig.Instance.DefaultExportFolder) || AppConfig.Instance.DefaultExportFolder == "null")
            {
                _useExportFolderToggle!.IsChecked = false;
                _useExportFolderToggle!.IsEnabled = false;
            } else if(AppConfig.Instance.UseDefaultExportFolder == true)
            {
                _useExportFolderToggle!.IsChecked = true;
            }

            _repakPathTextBox = this.FindControl<TextBox>("RepakPathTextBox");
            _ffmpegOptionsTextBox = this.FindControl<TextBox>("FFmpegOptionsTextBox");

            if (_updateToggle != null)
            {
                _updateToggle.IsCheckedChanged += OnUpdateToggleChanged;
            }

            if (_muteNotificationSoundToggle != null)
            {
                _muteNotificationSoundToggle.IsCheckedChanged += OnMuteNotificationSoundToggleChanged;
            }
        }

        private void LoadSettings()
        {
            var config = AppConfig.Instance;

            if (_updateToggle != null)
                _updateToggle.IsChecked = config.AutoUpdateEnabled;

            if (_muteNotificationSoundToggle != null)
                _muteNotificationSoundToggle.IsChecked = config.MuteNotificationSound;

            if (_exportFolderTextBox != null)
                _exportFolderTextBox.Text = config.DefaultExportFolder;

            if (_repakPathTextBox != null)
                _repakPathTextBox.Text = config.RepakPath;

            if (_ffmpegOptionsTextBox != null)
                _ffmpegOptionsTextBox.Text = config.FfmpegOptions;
        }

        private void OnUpdateToggleChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_updateToggle != null)
            {
                var config = AppConfig.Instance;
                config.AutoUpdateEnabled = _updateToggle.IsChecked ?? false;
                config.Save();
            }
        }

        private async void OnExportFolderBrowseButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Export Folder"
            };

            var result = await dialog.ShowAsync(this);
            if (!string.IsNullOrEmpty(result) && _exportFolderTextBox != null)
            {
                _exportFolderTextBox.Text = result;
                AppConfig.Instance.DefaultExportFolder = result;
                AppConfig.Instance.Save();
                _mainWindow!.defaultExportFolder = result;
                _mainWindow!.UpdateExportFolderCheckboxes();
            }
        }

        private void OnUseExportFolderToggleChecked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            AppConfig.Instance.UseDefaultExportFolder = true;
            AppConfig.Instance.Save();
            _mainWindow!.UpdateExportFolderCheckboxes();
        }
        private void OnUseExportFolderToggleUnchecked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            AppConfig.Instance.UseDefaultExportFolder = false;
            AppConfig.Instance.Save();
            _mainWindow!.UpdateExportFolderCheckboxes();
        }

        private async void OnRepakPathBrowseButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Repak Executable",
                AllowMultiple = false,
                Filters = new List<FileDialogFilter> { new FileDialogFilter { Name = "Executable", Extensions = new List<string> { "exe" } } }
            };

            var result = await dialog.ShowAsync(this);
            if (result != null && result.Length > 0 && _repakPathTextBox != null)
            {
                _repakPathTextBox.Text = result[0];
                AppConfig.Instance.RepakPath = result[0];
                AppConfig.Instance.Save();
            }
        }

        private void OnResetFFmpegOptionsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            const string defaultOptions = "-acodec pcm_s16le -ar 48000 -ac 2";
            if (_ffmpegOptionsTextBox != null)
            {
                _ffmpegOptionsTextBox.Text = defaultOptions;
                AppConfig.Instance.FfmpegOptions = defaultOptions;
                AppConfig.Instance.Save();
            }
        }

        private void OnMuteNotificationSoundToggleChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_muteNotificationSoundToggle != null)
            {
                AppConfig.Instance.MuteNotificationSound = _muteNotificationSoundToggle.IsChecked ?? false;
                AppConfig.Instance.Save();
            }
        }

        private void OnLicensesClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var licensesWindow = new LicensesWindow();
            licensesWindow.ShowDialog(this);
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
                    bool userWantsUpdate = await UpdateDialog.ShowDialogAsync(this, currentVersion, newVersion);
                    if (userWantsUpdate)
                    {
                        await updater.DownloadUpdate();
                    }
                }
                else
                {
                    _notificationManager.Show(new Notification(
                        "No updates available",
                        "You are already on the latest version.",
                        NotificationType.Information
                    ));
                }
            }
            catch (Exception ex)
            {
                _notificationManager.Show(new Notification(
                    "Update Error",
                    $"Failed to check for updates: {ex.Message}",
                    NotificationType.Error
                ));
            }
        }

        private void OnReportIssueClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            OpenUrl("https://github.com/Snoozeds/PD3AudioModder/issues/new");
        }

        private void OnDonateClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            OpenUrl("https://ko-fi.com/snoozeds");
        }

        private void OpenUrl(string url)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                if (_notificationManager != null)
                {
                    _notificationManager.Show(new Notification(
                        "Error",
                        $"Failed to open URL: {ex.Message}",
                        NotificationType.Error
                    ));
                }
            }
        }

        private void OnBackClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            this.Close();
        }

        private void OnCloseClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            this.Close();
        }
    }
}