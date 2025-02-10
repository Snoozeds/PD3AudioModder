using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using System;

namespace PD3AudioModder
{
    public partial class SettingsWindow : Window
    {
        private MainWindow? _mainWindow;
        private WindowNotificationManager? _notificationManager;
        private ToggleSwitch? _updateToggle;

        public SettingsWindow()
        {
            InitializeComponent();
        }

        public SettingsWindow(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            _notificationManager = new WindowNotificationManager(this);
            InitializeComponent();

            _updateToggle = this.FindControl<ToggleSwitch>("UpdateToggle");
            if (_updateToggle != null)
            {
                _updateToggle.IsChecked = AppConfig.Instance.AutoUpdateEnabled;
                _updateToggle.IsCheckedChanged += OnUpdateToggleChanged;
            }
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
                    bool userWantsUpdate = await UpdateDialog.ShowDialogAsync(this, newVersion);
                    if (userWantsUpdate)
                    {
                         updater.DownloadUpdate();
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
