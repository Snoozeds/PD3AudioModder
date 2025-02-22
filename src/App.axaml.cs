using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace PD3AudioModder
{
    public partial class App : Application
    {
        // Allow launching the app with "-console argument"
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();

        private bool _consoleAllocated = false;

        public override void Initialize()
        {
            // Check for command-line arguments
            if (Environment.GetCommandLineArgs().Contains("-console"))
            {
                if (AllocConsole())
                {
                    _consoleAllocated = true;
                    Console.WriteLine("Console initialized due to -console argument.");
                }
            }

            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException!;

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();

            if (_consoleAllocated)
            {
                AppDomain.CurrentDomain.ProcessExit += (sender, args) => FreeConsole();
            }
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogException(ex, "Unhandled Exception");
                Console.Error.WriteLine($"Unhandled Exception: {ex}");
            }
            else
            {
                Console.Error.WriteLine(
                    "Unhandled Exception: Unknown error (ExceptionObject was null)."
                );
            }
        }

        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            LogException(e.Exception, "Unobserved Task Exception");
            Console.Error.WriteLine($"Unobserved Task Exception: {e.Exception}");
            e.SetObserved();
        }

        private void LogException(Exception ex, string exceptionType)
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData
                );
                string logDirectory = Path.Combine(appDataPath, "PD3AudioModder", "Logs");
                Directory.CreateDirectory(logDirectory);

                string logFilePath = Path.Combine(logDirectory, "crashlog.txt");
                string logMessage =
                    $"{DateTime.Now}: {exceptionType} - {ex?.Message}\n{ex?.StackTrace}\n\n";

                File.AppendAllText(logFilePath, logMessage);

                Console.Error.WriteLine(logMessage);
            }
            catch
            {
                return;
            }
        }
    }
}
