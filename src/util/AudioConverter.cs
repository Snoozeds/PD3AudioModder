using PD3AudioModder;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

public class AudioConverter
{
    private static AppConfig _config = AppConfig.Load();
    private static string? FfmpegOptions = _config.FfmpegOptions;

    private static string GetFfmpegPath()
    {
        // check if ffmpeg exists in the application directory
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string ffmpegExecutable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";
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
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string[] commonPaths = { "/usr/bin/ffmpeg", "/usr/local/bin/ffmpeg", "/opt/homebrew/bin/ffmpeg" };
            foreach (string path in commonPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        throw new FileNotFoundException("FFmpeg not found. Please ensure FFmpeg is installed and available in your system PATH or application directory.");
    }

    private static string? GetCommandPath(string command)
    {
        string[] paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? Array.Empty<string>();
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
            throw new Exception("FFmpeg not found. Please install FFmpeg and ensure it's available in your system PATH.", ex);
        }

        string outputDir = Path.GetDirectoryName(outputPath) ?? throw new ArgumentException("Invalid output path");
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
            Arguments = $"-i \"{inputPath}\" {FfmpegOptions} \"{outputPath}\" -y",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        using var process = Process.Start(processStartInfo)
            ?? throw new Exception("Failed to start FFmpeg process");

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var warningDialog = new WarningDialog($"FFmpeg conversion failed with exit code {process.ExitCode}.");
            warningDialog.Show();
            throw new Exception($"FFmpeg conversion failed with exit code {process.ExitCode}.");
        }

        if (!File.Exists(outputPath))
        {
            throw new Exception("FFmpeg completed but no output file was created.");
        }
    }
}
