using System;
using System.Collections.Generic;
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

        // Used so user doesn't acidentally overwrite the default xml theme.
        private FileStream? _fileLock;

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
            string appDataPath = Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData
            );
            string themesDirectory = Path.Combine(appDataPath, "PD3AudioModder", "Themes");
            string defaultThemePath = Path.Combine(themesDirectory, "Default.ini");

            // Create themes directory if it does not exist
            try
            {
                if (!Directory.Exists(themesDirectory))
                {
                    Directory.CreateDirectory(themesDirectory);
                    Console.WriteLine($"Created themes directory: {themesDirectory}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to create themes directory: {ex.Message}");
                LogException(ex, "Themes Directory Creation Error");
            }

            // Load theme from config
            string selectedTheme = AppConfig.Instance.Theme ?? "Default.ini";
            string themePath = Path.Combine(themesDirectory, selectedTheme);

            // Check if the theme file exists
            if (!File.Exists(themePath))
            {
                Console.WriteLine($"Theme file not found: {themePath}. Using Default.ini instead.");
                themePath = Path.Combine(themesDirectory, "Default.ini");
                AppConfig.Instance.Theme = "Default.ini";
                AppConfig.Instance.Save();
            }

            // Load and apply theme
            try
            {
                ApplyThemeFromFile(themePath);
                Console.WriteLine($"Applied theme: {selectedTheme}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error applying theme: {ex.Message}");
                LogException(ex, "Theme Application Error");
            }

            // Create default themes if they do not exist
            var defaultThemes = new Dictionary<string, string>
            {
                {
                    "Default.ini",
                    @"
[Theme]
BackgroundColor=#1B262C
TextColor=#BBE1FA
ButtonColor=#3282B8
ButtonTextColor=#FFFFFF
SecondaryButtonColor=#BBE1FA
SecondaryButtonTextColor=#1B262C
BorderColor=#3282B8
TertiaryColor=#0F4C75
BorderBackgroundColor=#2C3E50
WarningTextColor=#E6C400
SettingsTextColor=#8B9DA7
MenuHoverColor=#275E83
SystemAccentColor=#3CA1E6
"
                },
                {
                    "DarkBlue.ini",
                    @"
[Theme]
BackgroundColor=#0A192F
TextColor=#E6F1FF
ButtonColor=#1E3A8A
ButtonTextColor=#FFFFFF
SecondaryButtonColor=#64FFDA
SecondaryButtonTextColor=#0A192F
BorderColor=#1E3A8A
TertiaryColor=#112240
BorderBackgroundColor=#172A45
WarningTextColor=#FFC857
SettingsTextColor=#8892B0
MenuHoverColor=#233554
SystemAccentColor=#5EACFC
"
                },
                {
                    "Dracula.ini",
                    @"
[Theme]
BackgroundColor=#282A36
TextColor=#F8F8F2
ButtonColor=#BD93F9
ButtonTextColor=#F8F8F2
SecondaryButtonColor=#44475A
SecondaryButtonTextColor=#F8F8F2
BorderColor=#6272A4
TertiaryColor=#44475A
BorderBackgroundColor=#383A59
WarningTextColor=#FFB86C
SettingsTextColor=#6272A4
MenuHoverColor=#BD93F9
SystemAccentColor=#FF79C6"
                },
                {
                    "HotdogStand.ini",
                    @"
[Theme]
BackgroundColor=#FF0000
TextColor=#FFFF00
ButtonColor=#000000
ButtonTextColor=#FFFF00
SecondaryButtonColor=#FF0000
SecondaryButtonTextColor=#FFFF00
BorderColor=#000000
TertiaryColor=#FF6600
BorderBackgroundColor=#660000
WarningTextColor=#FFFFFF
SettingsTextColor=#FFFF99
MenuHoverColor=#990000
SystemAccentColor=#FFCC00"
                },
            };

            foreach (var theme in defaultThemes)
            {
                string currentThemePath = Path.Combine(themesDirectory, theme.Key);
                if (!File.Exists(currentThemePath))
                {
                    File.WriteAllText(currentThemePath, theme.Value.Trim());
                    Console.WriteLine($"Created theme file: {currentThemePath}");
                }
            }

            // Lock default.ini to prevent accidental overwriting
            try
            {
                _fileLock = new FileStream(
                    defaultThemePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read
                );
                Console.WriteLine($"Locked {defaultThemePath} to prevent accidental overwriting.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to lock {defaultThemePath}: {ex.Message}");
                LogException(ex, "File Lock Error");
            }

            // Handle exceptions
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

        private void ApplyThemeFromFile(string themePath)
        {
            // Create a new instance of ini parser
            var themeParser = new iniParser(themePath);
            themeParser.Load(themePath);

            // Get the application resources
            var resources = Application.Current.Resources;

            // Apply each color from the theme
            resources["BackgroundColor"] = ParseColor(
                themeParser.GetValue("Theme", "BackgroundColor", "#1B262C")
            );
            resources["TextColor"] = ParseColor(
                themeParser.GetValue("Theme", "TextColor", "#BBE1FA")
            );
            resources["ButtonColor"] = ParseColor(
                themeParser.GetValue("Theme", "ButtonColor", "#3282B8")
            );
            resources["ButtonTextColor"] = ParseColor(
                themeParser.GetValue("Theme", "ButtonTextColor", "#FFFFFF")
            );
            resources["SecondaryButtonColor"] = ParseColor(
                themeParser.GetValue("Theme", "SecondaryButtonColor", "#BBE1FA")
            );
            resources["SecondaryButtonTextColor"] = ParseColor(
                themeParser.GetValue("Theme", "SecondaryButtonTextColor", "#1B262C")
            );
            resources["BorderColor"] = ParseColor(
                themeParser.GetValue("Theme", "BorderColor", "#3282B8")
            );
            resources["TertiaryColor"] = ParseColor(
                themeParser.GetValue("Theme", "TertiaryColor", "#0F4C75")
            );
            resources["BorderBackgroundColor"] = ParseColor(
                themeParser.GetValue("Theme", "BorderBackgroundColor", "#2C3E50")
            );
            resources["WarningTextColor"] = ParseColor(
                themeParser.GetValue("Theme", "WarningTextColor", "#E6C400")
            );
            resources["SettingsTextColor"] = ParseColor(
                themeParser.GetValue("Theme", "SettingsTextColor", "#8B9DA7")
            );
            resources["MenuHoverColor"] = ParseColor(
                themeParser.GetValue("Theme", "MenuHoverColor", "#275E83")
            );
            resources["SystemAccentColor"] = ParseColor(
                themeParser.GetValue("Theme", "SystemAccentColor", "#3CA1E6")
            );

            // Add system accent brush
            resources["SystemAccentBrush"] = new Avalonia.Media.SolidColorBrush(
                (Avalonia.Media.Color)resources["SystemAccentColor"]
            );
        }

        private Avalonia.Media.Color ParseColor(string hex)
        {
            return Avalonia.Media.Color.Parse(hex);
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
