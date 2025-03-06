using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using PD3AudioModder;

public class AudioConverter
{
    private static AppConfig _config = AppConfig.Load();
    private static string? FfmpegOptions = _config.FfmpegOptions;
    private static string? FfmpegPath = _config.FfmpegPath;

    // Used for launching console window when batch converting so that multiple ffmpeg windows are not spawned.
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();
    private static string GetFfmpegPath()
    {
        // check config path
        if (!String.IsNullOrEmpty(FfmpegPath))
        {
            return FfmpegPath;
        }

        // check if ffmpeg exists in the application directory
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string ffmpegExecutable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "ffmpeg.exe"
            : "ffmpeg";
        string localFfmpeg = Path.Combine(baseDir, ffmpegExecutable);

        if (File.Exists(localFfmpeg))
        {
            return localFfmpeg;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string? ffmpegFromPath = GetCommandPath(ffmpegExecutable);
            if (!string.IsNullOrEmpty(ffmpegFromPath))
            {
                return ffmpegFromPath;
            }
        }

        // check unix-like system locations
        if (
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
        )
        {
            string[] commonPaths =
            {
                "/usr/bin/ffmpeg",
                "/usr/local/bin/ffmpeg",
                "/opt/homebrew/bin/ffmpeg",
            };
            foreach (string path in commonPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        throw new FileNotFoundException(
            "FFmpeg not found. Please ensure FFmpeg is installed and available in your system PATH or the same directory PD3AudioModder is in. You may also choose a path in settings."
        );
    }

    private static string? GetCommandPath(string command)
    {
        string[] paths =
            Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator)
            ?? Array.Empty<string>();
        foreach (string path in paths)
        {
            string fullPath = Path.Combine(path, command);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }
        return null;
    }

    public static async Task ConvertToWAV(string inputPath, string outputPath)
    {
        if (string.IsNullOrEmpty(inputPath))
        {
            throw new ArgumentException("Input path cannot be null or empty.", nameof(inputPath));
        }

        if (string.IsNullOrEmpty(outputPath))
        {
            throw new ArgumentException("Output path cannot be null or empty.", nameof(outputPath));
        }

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Input file not found: {inputPath}");
        }

        string ffmpegPath;
        try
        {
            ffmpegPath = GetFfmpegPath();
        }
        catch (Exception ex)
        {
            throw new Exception(
                "FFmpeg not found. Please install FFmpeg and ensure it's available in your system PATH.",
                ex
            );
        }

        string outputDir =
            Path.GetDirectoryName(outputPath) ?? throw new ArgumentException("Invalid output path");
        Directory.CreateDirectory(outputDir);

        if (!outputPath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
        {
            outputPath = Path.ChangeExtension(outputPath, ".wav");
        }

        if (string.IsNullOrEmpty(FfmpegOptions))
        {
            FfmpegOptions = DefaultConfig.FfmpegOptions!;
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = $"-i \"{inputPath}\" {FfmpegOptions.Replace("\"", "")} \"{outputPath}\" -y",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Minimized,
        };

        using var process =
            Process.Start(processStartInfo)
            ?? throw new Exception("Failed to start FFmpeg process");

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var warningDialog = new WarningDialog(
                $"FFmpeg conversion failed with exit code {process.ExitCode}."
            );
            warningDialog.Show();
            throw new Exception($"FFmpeg conversion failed with exit code {process.ExitCode}.");
        }

        if (!File.Exists(outputPath))
        {
            throw new Exception("FFmpeg completed but no output file was created.");
        }
    }

    public static async Task BatchConvertToWAV(string[] inputPaths, string outputDirectory)
    {
        if (inputPaths == null || inputPaths.Length == 0)
        {
            throw new ArgumentException("Input paths array cannot be null or empty.", nameof(inputPaths));
        }

        if (string.IsNullOrEmpty(outputDirectory))
        {
            throw new ArgumentException("Output directory cannot be null or empty.", nameof(outputDirectory));
        }

        Directory.CreateDirectory(outputDirectory);

        string ffmpegPath;
        try
        {
            ffmpegPath = GetFfmpegPath();
        }
        catch (Exception ex)
        {
            throw new Exception("FFmpeg not found. Please install FFmpeg and ensure it's available in your system PATH.", ex);
        }

        if (string.IsNullOrEmpty(FfmpegOptions))
        {
            FfmpegOptions = DefaultConfig.FfmpegOptions!;
        }

        // Allocate console only if it is not already allocated
        bool consoleAllocated = false;
        if (Environment.GetCommandLineArgs().Contains("-console"))
        {
            if (AllocConsole())
            {
                consoleAllocated = true;
                Console.WriteLine("Console initialized due to -console argument.");
            }
        }
        else if (!consoleAllocated && AllocConsole())
        {
            consoleAllocated = true;
            Console.WriteLine("Console dynamically allocated.");
        }

        try
        {
            // Process files in batches
            const int batchSize = 20;
            for (int i = 0; i < inputPaths.Length; i += batchSize)
            {
                var batchPaths = inputPaths.Skip(i).Take(batchSize).ToArray();

                foreach (string inputPath in batchPaths)
                {
                    if (!File.Exists(inputPath))
                    {
                        throw new FileNotFoundException($"Input file not found: {inputPath}");
                    }

                    string outputPath = Path.Combine(outputDirectory, Path.ChangeExtension(Path.GetFileName(inputPath), ".wav"));

                    // Ensure the paths are properly quoted and escaped
                    string inputQuoted = $"\"{inputPath}\"";
                    string outputQuoted = $"\"{outputPath}\"";
                    string arguments = $"-i {inputQuoted} {FfmpegOptions} {outputQuoted} -y";

                    // Setup ProcessStartInfo
                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = false,
                        RedirectStandardError = false,
                        CreateNoWindow = false  // Keep window visible, otherwise Windows Defender thinks its a trojan. We love MS!!! :DD
                    };

                    using var process = new Process { StartInfo = processStartInfo };

                    try
                    {
                        process.Start();
                        await process.WaitForExitAsync();

                        if (process.ExitCode != 0)
                        {
                            throw new Exception($"FFmpeg conversion failed for: {inputPath}");
                        }

                        // Verify output file
                        if (!File.Exists(outputPath))
                        {
                            throw new Exception($"Conversion completed but output file was not created: {outputPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");
                        throw;
                    }
                }
            }
        }
        finally
        {
            // Free console after all files are processed
            if (consoleAllocated)
            {
                FreeConsole();
                consoleAllocated = false;
                Console.WriteLine("Console freed.");
            }
        }
    }
}