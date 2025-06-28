using System;
using System.IO;
using System.Linq;

namespace PD3AudioModder.util
{
    public static class VgmstreamPathFinder
    {
        /// <summary>
        /// Searches for vgmstream-cli.exe in common system locations
        /// </summary>
        /// <returns>Full path to vgmstream-cli.exe if found, null otherwise</returns>
        public static string FindVgmstreamPath()
        {
            string[] searchDirectories =
            {
                AppDomain.CurrentDomain.BaseDirectory,
                // System directories
                Environment.GetFolderPath(Environment.SpecialFolder.System), // System32
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "SysWOW64"
                ),
                // Program Files directories
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                // Common installation subdirectories in Program Files
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "vgmstream"
                ),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "vgmstream"
                ),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "VGMStream"
                ),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "VGMStream"
                ),
            };

            // Also check PATH environment variable directories
            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            var pathDirectories = pathEnv
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Where(dir => !string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                .ToArray();

            // Combine search directories with PATH directories
            var allDirectories = searchDirectories.Concat(pathDirectories).Distinct();

            foreach (string directory in allDirectories)
            {
                try
                {
                    if (!Directory.Exists(directory))
                        continue;

                    string vgmstreamPath = Path.Combine(directory, "vgmstream-cli.exe");
                    if (File.Exists(vgmstreamPath))
                    {
                        Console.WriteLine($"Found vgmstream-cli.exe at: {vgmstreamPath}");
                        return vgmstreamPath;
                    }
                }
                catch (Exception ex)
                {
                    // Skip directories we can't access
                    Console.WriteLine($"Could not access directory {directory}: {ex.Message}");
                    continue;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the vgmstream path with fallback error message
        /// </summary>
        public static (string path, string errorMessage) GetVgmstreamPathWithFallback()
        {
            string vgmstreamPath = FindVgmstreamPath();

            if (!string.IsNullOrEmpty(vgmstreamPath))
            {
                return (vgmstreamPath, null);
            }

            string errorMessage =
                "vgmstream-cli.exe not found!\n"
                + "Please download it from https://vgmstream.org/downloads\n"
                + "and place it in one of these locations:\n"
                + $"• Application directory: {AppDomain.CurrentDomain.BaseDirectory}\n"
                + $"• System32: {Environment.GetFolderPath(Environment.SpecialFolder.System)}\n"
                + $"• Program Files: {Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}\\vgmstream\\\n"
                + "• Or add it to your system PATH";

            return (null, errorMessage);
        }
    }
}
