using System;
using System.IO;
using Microsoft.Win32;

namespace PD3AudioModder.util
{
    /// <summary>
    /// Util used to save/get locations of registry keys.
    /// Example: Saving default folder locations.
    /// User loads .pak files from a folder, it is a good idea to save this folder in the registry and then default to that folder whenever the user loads .pak files.
    /// </summary>
    public class RegistryLocations
    {
        private const string RegistryKey = @"SOFTWARE\PD3AudioModder"; // Default registry key location
        private const string PakDirectoryValue = "PakDirectory"; // Used for when user loads pak files
        private const string AudioSaveDirectoryValue = "AudioSaveDirectory"; // Used for when user saves audio files (.wav)
        private const string ExportDirectoryValue = "ExportDirectory"; // Used for when user exports audio files (.uasset, .ubulk, .uexp)

        // Get the stored pak directory or null if not set
        public static string GetPakDirectory()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKey))
                {
                    if (key != null)
                    {
                        var value = key.GetValue(PakDirectoryValue) as string;
                        if (!string.IsNullOrEmpty(value) && Directory.Exists(value))
                            return value;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading pak directory from registry: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Saves the pak directory to registry
        /// </summary>
        /// <param name="directory">The directory to save</param>
        public static void SavePakDirectory(string directory)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RegistryKey))
                {
                    key.SetValue(PakDirectoryValue, directory);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving pak directory to registry: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the stored audio save directory or null if not set
        /// </summary>
        /// <returns></returns>
        public static string GetAudioSaveDirectory()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKey))
                {
                    if (key != null)
                    {
                        var value = key.GetValue(AudioSaveDirectoryValue) as string;
                        if (!string.IsNullOrEmpty(value) && Directory.Exists(value))
                            return value;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Error reading audio save directory from registry: {ex.Message}"
                );
            }
            return null;
        }

        /// <summary>
        /// Saves the audio save directory to registry
        /// </summary>
        /// <param name="directory">The directory to save</param>
        public static void SaveAudioSaveDirectory(string directory)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RegistryKey))
                {
                    key.SetValue(AudioSaveDirectoryValue, directory);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving audio save directory to registry: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the stored export directory or null if not set
        /// </summary>
        /// <returns></returns>
        public static string GetExportDirectory()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKey))
                {
                    if (key != null)
                    {
                        var value = key.GetValue(ExportDirectoryValue) as string;
                        if (!string.IsNullOrEmpty(value) && Directory.Exists(value))
                            return value;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading export directory from registry: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Saves the export directory to registry
        /// </summary>
        /// <param name="directory">The directory to save</param>
        public static void SaveExportDirectory(string directory)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RegistryKey))
                {
                    key.SetValue(ExportDirectoryValue, directory);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving export directory to registry: {ex.Message}");
            }
        }
    }
}
