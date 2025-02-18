using Avalonia.Controls.Notifications;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace PD3AudioModder
{
    public class AutoUpdater
    {
        private const string VersionUrl = "https://raw.githubusercontent.com/Snoozeds/PD3AudioModder/main/version.txt";
        private const string LocalVersionFile = "version.txt";
        private const string TempUpdatePath = "update.zip";
        private readonly MainWindow _mainWindow;

        public AutoUpdater(WindowNotificationManager notificationManager, MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

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
                    ? File.ReadAllText(LocalVersionFile).Replace("\r", "").Replace("\n", "").TrimEnd()
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

        public string GetCurrentVersion()
        {
            try
            {
                string currentVersion = File.Exists(LocalVersionFile)
                    ? File.ReadAllText(LocalVersionFile).Replace("\r", "").Replace("\n", "").TrimEnd()
                    : "0.0.0";
                return currentVersion;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving current version: {ex.Message}");
                return "Unknown";
            }
        }

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
                scriptContent = @$"
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
                scriptContent = @$"
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
                Process.Start(new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x {updateScriptPath}.sh",
                    UseShellExecute = true
                });
            }
            else
            {
                throw new NotSupportedException("Unsupported OS platform.");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "bash",
                Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"/c start /min \"\" \"{updateScriptPath}.bat\"" : updateScriptPath + ".sh",
                UseShellExecute = true,
                CreateNoWindow = false
            });

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
                        throw new Exception($"Failed to fetch version file. HTTP Status Code: {response.StatusCode}");
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

        public async Task DownloadUpdate()
        {
            string url = "https://github.com/Snoozeds/PD3AudioModder/releases/latest/download/PD3AudioModder.zip";

            using (HttpClient client = new HttpClient())
            {
                byte[] updateData = await client.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(TempUpdatePath, updateData);
            }

            LaunchUpdateProcess();
        }
    }
}