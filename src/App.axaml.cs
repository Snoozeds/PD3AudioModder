using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace PD3AudioModder
{
    public partial class App : Application
    {
        public override void Initialize()
        {
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
