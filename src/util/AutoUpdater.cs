using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;

namespace PD3AudioModder
{
    /// <summary>
    /// Class to handle automatic updates for the application.
    /// </summary>
    public class AutoUpdater
    {
        private const string VersionUrl =
            "https://raw.githubusercontent.com/Snoozeds/PD3AudioModder/main/version.txt";
        private const string LocalVersionFile = "version.txt";
        private const string TempUpdatePath = "update.zip";
        private readonly MainWindow _mainWindow;
        private WindowNotificationManager _notificationManager;

        public AutoUpdater(WindowNotificationManager notificationManager, MainWindow mainWindow)
        {
            _notificationManager = notificationManager;
            _mainWindow = mainWindow;
        }

        // Check if running with admin privileges
        private bool IsRunningAsAdmin()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Check if running as root or with sudo
                return Environment.UserName == "root"
                    || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SUDO_USER"));
            }
            return false;
        }

        // Check if we can write to the application directory
        private bool CanWriteToAppDirectory()
        {
            try
            {
                string testFile = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "write_test.tmp"
                );
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Restart application with admin privileges
        private void RestartWithAdminPrivileges()
        {
            try
            {
                string? currentExePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(currentExePath))
                {
                    throw new Exception("Failed to get current executable path.");
                }

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = currentExePath,
                    UseShellExecute = true,
                };

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    startInfo.Verb = "runas"; // Request admin privileges on Windows
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // Use pkexec or gksudo (unmaintained but Linux users are Linux users) for privilege escalation on Linux
                    if (File.Exists("/usr/bin/pkexec"))
                    {
                        startInfo.FileName = "pkexec";
                        startInfo.Arguments = $"{currentExePath}";
                    }
                    else if (File.Exists("/usr/bin/gksudo"))
                    {
                        startInfo.FileName = "gksudo";
                        startInfo.Arguments = $"{currentExePath}";
                    }
                    else
                    {
                        // Fallback to sudo in terminal
                        startInfo.FileName = "sudo";
                        startInfo.Arguments = $"{currentExePath}";
                    }
                }

                Process.Start(startInfo);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                _notificationManager?.Show(
                    new Notification(
                        "Error",
                        $"Failed to restart with admin privileges: {ex.Message}",
                        NotificationType.Error,
                        TimeSpan.FromSeconds(10)
                    )
                );
            }
        }

        /// <summary>
        /// Checks for updates by comparing the local version with the latest version from GitHub.
        /// </summary>
        /// <returns></returns>
        public async Task<(bool available, string newVersion)> CheckForUpdates()
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                Console.WriteLine("Skipping update check - debug mode.");
                return (false, string.Empty);
            }

            try
            {
                string latestVersion = await GetLatestVersion();
                string currentVersion = File.Exists(LocalVersionFile)
                    ? File.ReadAllText(LocalVersionFile)
                        .Replace("\r", "")
                        .Replace("\n", "")
                        .TrimEnd()
                    : "0.0.0";

                if (latestVersion != currentVersion)
                {
                    return (true, latestVersion);
                }

                Console.WriteLine("You are running the latest version.");
                return (false, currentVersion);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking for updates: {ex.Message}");
                return (false, string.Empty);
            }
        }

        /// <summary>
        /// Gets the current version of the application from the version file.
        /// </summary>
        /// <returns></returns>
        public string GetCurrentVersion()
        {
            try
            {
                string currentVersion = File.Exists(LocalVersionFile)
                    ? File.ReadAllText(LocalVersionFile)
                        .Replace("\r", "")
                        .Replace("\n", "")
                        .TrimEnd()
                    : "0.0.0";
                return currentVersion;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving current version: {ex.Message}");
                return "Unknown";
            }
        }

        /// <summary>
        /// Starts the update process.
        /// </summary>
        /// <exception cref="Exception"></exception>
        /// <exception cref="NotSupportedException"></exception>
        public void LaunchUpdateProcess()
        {
            string currentProcessId = Process.GetCurrentProcess().Id.ToString();
            string? currentExePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(currentExePath))
            {
                throw new Exception("Failed to get current executable path.");
            }
            string updateScriptPath = Path.Combine(Path.GetTempPath(), "update_script");

            string scriptContent;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows script
                scriptContent =
                    @$"
@echo off
timeout /t 1 /nobreak >nul
:loop
tasklist /fi ""PID eq {currentProcessId}"" 2>nul | find ""{currentProcessId}"" >nul
if errorlevel 1 (
    powershell -Command ""Expand-Archive -Path '{TempUpdatePath}' -DestinationPath '{AppDomain.CurrentDomain.BaseDirectory}' -Force""
    del ""{TempUpdatePath}""
    start """" ""{currentExePath}""
    del ""%~f0""
    exit
) else (
    timeout /t 1 /nobreak >nul
    goto loop
)";
                File.WriteAllText(updateScriptPath + ".bat", scriptContent);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Linux script
                scriptContent =
                    @$"
#!/bin/bash
while true; do
    if ! ps -p {currentProcessId} > /dev/null; then
        unzip -o {TempUpdatePath} -d {AppDomain.CurrentDomain.BaseDirectory}
        rm {TempUpdatePath}
        nohup {currentExePath} &
        rm -- ""$0""
        exit
    else
        sleep 1
    fi
done";
                File.WriteAllText(updateScriptPath + ".sh", scriptContent);
                // Make the script executable
                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"+x {updateScriptPath}.sh",
                        UseShellExecute = true,
                    }
                );
            }
            else
            {
                throw new NotSupportedException("Unsupported OS platform.");
            }

            Process.Start(
                new ProcessStartInfo
                {
                    FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? "cmd.exe"
                        : "bash",
                    Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? $"/c start /min \"\" \"{updateScriptPath}.bat\""
                        : updateScriptPath + ".sh",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                }
            );

            Environment.Exit(0);
        }

        private async Task<string> GetLatestVersion()
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await client.GetAsync(VersionUrl);
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception(
                            $"Failed to fetch version file. HTTP Status Code: {response.StatusCode}"
                        );
                    }
                    string version = await response.Content.ReadAsStringAsync();
                    return version.Replace("\r", "").Replace("\n", "").TrimEnd();
                }
                catch (Exception ex)
                {
                    throw new Exception("Unexpected error: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Downloads the update and initiates the update process.
        /// </summary>
        /// <returns></returns>
        public async Task DownloadUpdate()
        {
            // Check if we need admin privileges before starting the update
            if (!CanWriteToAppDirectory() && !IsRunningAsAdmin())
            {
                _notificationManager?.Show(
                    new Notification(
                        "Admin Rights Required",
                        "Administrator privileges are required to update the application. The application will restart with elevated permissions.",
                        NotificationType.Warning,
                        TimeSpan.FromSeconds(5)
                    )
                );

                // Wait a moment for the user to see the notification
                await Task.Delay(2000);

                RestartWithAdminPrivileges();
                return;
            }

            _notificationManager?.Show(
                new Notification(
                    "Update in Progress",
                    "An update is in progress. Please wait...",
                    NotificationType.Information,
                    TimeSpan.FromSeconds(9999) // Expiration
                )
            );

            try
            {
                string url =
                    "https://github.com/Snoozeds/PD3AudioModder/releases/latest/download/PD3AudioModder.zip";

                using (HttpClient client = new HttpClient())
                {
                    byte[] updateData = await client.GetByteArrayAsync(url);
                    await File.WriteAllBytesAsync(TempUpdatePath, updateData);
                }

                LaunchUpdateProcess();
            }
            catch (UnauthorizedAccessException)
            {
                _notificationManager?.Show(
                    new Notification(
                        "Permission Error",
                        "Unable to write update file. Please run as administrator or check file permissions.",
                        NotificationType.Error,
                        TimeSpan.FromSeconds(10)
                    )
                );
            }
            catch (Exception ex)
            {
                _notificationManager?.Show(
                    new Notification(
                        "Update Error",
                        $"Failed to download update: {ex.Message}",
                        NotificationType.Error,
                        TimeSpan.FromSeconds(10)
                    )
                );
            }
        }
    }
}
