using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using PD3AudioModder.ViewModels;

namespace PD3AudioModder
{
    public partial class ThemeEditor : Window
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly Dictionary<string, Color> themeColors = new();
        private string currentThemeName = "Default";
        private const string IniFilePath = "ThemeSettings.ini";
        private iniParser iniParser;

        private readonly ThemeEditorViewModel _viewModel;
        private readonly Random _random = new Random(); // Used for random color generation

        public ThemeEditor()
        {
            InitializeComponent();
            iniParser = new iniParser(IniFilePath);
            _viewModel = new ThemeEditorViewModel();
            _viewModel.ThemeSelected += OnThemeSelected;

            this.Opened += OnWindowOpened;
            DataContext = _viewModel;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnThemeSelected(object? sender, string themeFilePath)
        {
            if (File.Exists(themeFilePath))
            {
                try
                {
                    iniParser.Load(themeFilePath);

                    // Get the theme name from path
                    string selectedThemeName = Path.GetFileNameWithoutExtension(themeFilePath);
                    LoadTheme(selectedThemeName);

                    // Save theme in config
                    AppConfig.Instance.Theme = Path.GetFileName(themeFilePath);
                    AppConfig.Instance.Save();

                    // Update the INI Editor with file content
                    var INIEditor = this.FindControl<TextBox>("INIEditor");
                    if (INIEditor != null)
                    {
                        INIEditor.Text = File.ReadAllText(themeFilePath);
                    }

                    // Update theme name text box
                    var themeNameTextBox = this.FindControl<TextBox>("ThemeNameTextBox");
                    if (themeNameTextBox != null)
                    {
                        themeNameTextBox.Text = selectedThemeName;
                    }

                    // Update UI elements
                    UpdateColorPreviews();
                    ApplyTheme();

                    // Update current theme name
                    currentThemeName = selectedThemeName;

                    // Update theme name in bottom bar
                    var themeNameText = this.FindControl<TextBlock>("ThemeNameText");
                    if (themeNameText != null)
                    {
                        themeNameText.Text = "Theme: " + selectedThemeName;
                    }
                }
                catch (Exception ex)
                {
                    var warningOutput = this.FindControl<TextBox>("WarningOutput");
                    if (warningOutput != null)
                    {
                        warningOutput.Text = $"Error loading theme: {ex.Message}";
                        warningOutput.Foreground = new SolidColorBrush(Colors.Red);
                    }
                }
            }
        }

        private void OnWindowOpened(object? sender, EventArgs e)
        {
            InitializeDefaultTheme();
            SetupEventHandlers();
            LoadIniIntoTextBox();
            _viewModel.LoadThemes();
        }

        private void InitializeDefaultTheme()
        {
            // Set default colors
            themeColors["BackgroundColor"] = Color.Parse("#1B262C");
            themeColors["TextColor"] = Color.Parse("#BBE1FA");
            themeColors["ButtonColor"] = Color.Parse("#3282B8");
            themeColors["ButtonTextColor"] = Color.Parse("#ffffff");
            themeColors["SecondaryButtonColor"] = Color.Parse("#BBE1FA");
            themeColors["SecondaryButtonTextColor"] = Color.Parse("#1B262C");
            themeColors["BorderColor"] = Color.Parse("#3282B8");
            themeColors["TertiaryColor"] = Color.Parse("#0F4C75");
            themeColors["BorderBackgroundColor"] = Color.Parse("#2C3E50");
            themeColors["WarningTextColor"] = Color.Parse("#E6C400");
            themeColors["SettingsTextColor"] = Color.Parse("#8B9DA7");
            themeColors["MenuHoverColor"] = Color.Parse("#275e83");
            themeColors["SystemAccentColor"] = Color.Parse("#3ca1e6");

            SaveTheme();
            UpdateColorPreviews();
        }

        private void SetupEventHandlers()
        {
            var newThemeButton = this.FindControl<Button>("NewThemeButton");
            var loadThemeButton = this.FindControl<Button>("LoadThemeButton");
            var exportThemeButton = this.FindControl<Button>("ExportThemeButton");
            var saveThemeButton = this.FindControl<Button>("SaveThemeButton");

            if (newThemeButton != null)
                newThemeButton.Click += NewTheme_Click;
            if (loadThemeButton != null)
                loadThemeButton.Click += LoadTheme_Click;
            if (exportThemeButton != null)
                exportThemeButton.Click += ExportTheme_Click;
            if (saveThemeButton != null)
                saveThemeButton.Click += SaveTheme_Click;

            SetupColorPickerHandlers();
        }

        private void SetupColorPickerHandlers()
        {
            // Get all color pickers and set up handlers
            foreach (var colorKey in themeColors.Keys)
            {
                var colorPicker = this.FindControl<ColorPicker>($"{colorKey}Picker");
                if (colorPicker != null)
                {
                    colorPicker.ColorChanged += (s, e) =>
                    {
                        if (s is ColorPicker picker && !_viewModel.isDefaultTheme)
                        {
                            string colorName = picker.Name.Replace("Picker", "");
                            if (themeColors.ContainsKey(colorName))
                            {
                                themeColors[colorName] = picker.Color;
                                ApplyTheme();
                                UpdateIniEditor();
                            }
                        }
                    };
                }
            }
        }

        private void UpdateIniEditor()
        {
            var iniEditor = this.FindControl<TextBox>("INIEditor");
            if (iniEditor == null) return;

            var colorPickers = new Dictionary<string, string>
            {
                { "BackgroundColor", GetColorFromPicker("BackgroundColorPicker") },
                { "TextColor", GetColorFromPicker("TextColorPicker") },
                { "ButtonColor", GetColorFromPicker("ButtonColorPicker") },
                { "ButtonTextColor", GetColorFromPicker("ButtonTextColorPicker") },
                { "SecondaryButtonColor", GetColorFromPicker("SecondaryButtonColorPicker") },
                { "SecondaryButtonTextColor", GetColorFromPicker("SecondaryButtonTextColorPicker") },
                { "BorderColor", GetColorFromPicker("BorderColorPicker") },
                { "TertiaryColor", GetColorFromPicker("TertiaryColorPicker") },
                { "BorderBackgroundColor", GetColorFromPicker("BorderBackgroundColorPicker") },
                { "WarningTextColor", GetColorFromPicker("WarningTextColorPicker") },
                { "SettingsTextColor", GetColorFromPicker("SettingsTextColorPicker") },
                { "MenuHoverColor", GetColorFromPicker("MenuHoverColorPicker") },
                { "SystemAccentColor", GetColorFromPicker("SystemAccentColorPicker") }
            };

            var iniContent = "[Theme]\n";
            foreach (var kvp in colorPickers)
            {
                iniContent += $"{kvp.Key}={kvp.Value}\n";
            }

            iniEditor.Text = iniContent;
        }

        private string GetColorFromPicker(string pickerName)
        {
            var picker = this.FindControl<ColorPicker>(pickerName);
            if (picker == null) return "#FF000000";

            var color = picker.Color;
            return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private void LoadIniIntoTextBox()
        {
            var INIEditor = this.FindControl<TextBox>("INIEditor")!;
            var warningOutput = this.FindControl<TextBox>("WarningOutput")!;
            if (INIEditor == null)
                return;

            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("[Theme]");

                foreach (var kvp in themeColors)
                {
                    sb.AppendLine($"{kvp.Key}={ColorToHexString(kvp.Value)}");
                }

                INIEditor.Text = sb.ToString();
                if (_viewModel.isDefaultTheme == false)
                {
                    warningOutput.Text = "Theme loaded successfully.";
                    warningOutput.Foreground = new SolidColorBrush(Colors.Lime);
                }
            }
            catch (Exception ex)
            {
                warningOutput.Text = $"Failed to load theme: {ex.Message}";
                warningOutput.Foreground = new SolidColorBrush(Colors.Red);
            }
        }

        private void LoadTheme(string themeName = "")
        {
            var warningOutput = this.FindControl<TextBox>("WarningOutput");
            if (warningOutput == null)
                return;

            warningOutput.Text = "";

            List<string> missingColors = new List<string>();
            List<string> invalidColors = new List<string>();

            foreach (var key in themeColors.Keys)
            {
                string colorValue = iniParser.GetValue("Theme", key, "");

                if (string.IsNullOrWhiteSpace(colorValue))
                {
                    missingColors.Add(key);
                    continue;
                }

                try
                {
                    themeColors[key] = ParseColor(colorValue);
                }
                catch (Exception)
                {
                    invalidColors.Add($"{key} ({colorValue})");
                }
            }

            if (missingColors.Count == 0 && invalidColors.Count == 0)
            {
                warningOutput.Text = $"Theme '{themeName}' loaded successfully.";
                warningOutput.Foreground = new SolidColorBrush(Colors.Lime);
            }
            else
            {
                StringBuilder errorMessage = new StringBuilder();

                if (missingColors.Count > 0)
                {
                    errorMessage.AppendLine(
                        $"Colors not defined in INI: {string.Join(", ", missingColors)}"
                    );
                }

                if (invalidColors.Count > 0)
                {
                    errorMessage.AppendLine(
                        $"Invalid colors in INI: {string.Join(", ", invalidColors)}"
                    );
                }

                warningOutput.Text = errorMessage.ToString().TrimEnd();
                warningOutput.Foreground = new SolidColorBrush(Colors.Red);
            }

            ApplyTheme();
            UpdateColorPreviews();
        }

        private void ApplyTheme()
        {
            var app = Application.Current;
            if (app == null)
                return;

            // Get the current application resources
            var resources = app.Resources;

            // Update each color in the resource dictionary
            resources["BackgroundColor"] = themeColors["BackgroundColor"];
            resources["TextColor"] = themeColors["TextColor"];
            resources["ButtonColor"] = themeColors["ButtonColor"];
            resources["ButtonTextColor"] = themeColors["ButtonTextColor"];
            resources["SecondaryButtonColor"] = themeColors["SecondaryButtonColor"];
            resources["SecondaryButtonTextColor"] = themeColors["SecondaryButtonTextColor"];
            resources["BorderColor"] = themeColors["BorderColor"];
            resources["TertiaryColor"] = themeColors["TertiaryColor"];
            resources["BorderBackgroundColor"] = themeColors["BorderBackgroundColor"];
            resources["WarningTextColor"] = themeColors["WarningTextColor"];
            resources["SettingsTextColor"] = themeColors["SettingsTextColor"];
            resources["MenuHoverColor"] = themeColors["MenuHoverColor"];
            resources["SystemAccentColor"] = themeColors["SystemAccentColor"];
            resources["SystemAccentBrush"] = new SolidColorBrush(themeColors["SystemAccentColor"]);
        }

        private void SaveTheme()
        {
            return;
        }

        private void UpdateColorPreviews()
        {
            if (!this.IsLoaded)
                return;
            foreach (var colorKey in themeColors.Keys)
            {
                var preview = this.FindControl<Border>($"{colorKey}Preview");
                if (preview != null)
                {
                    preview.Background = new SolidColorBrush(themeColors[colorKey]);
                }
            }
        }

        private Color ParseColor(string hex)
        {
            return Color.Parse(hex);
        }

        private string ColorToHexString(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private Color HSVToColor(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            byte v = Convert.ToByte(value);
            byte p = Convert.ToByte(value * (1 - saturation));
            byte q = Convert.ToByte(value * (1 - f * saturation));
            byte t = Convert.ToByte(value * (1 - (1 - f) * saturation));

            return hi switch
            {
                0 => Color.FromRgb(v, t, p),
                1 => Color.FromRgb(q, v, p),
                2 => Color.FromRgb(p, v, t),
                3 => Color.FromRgb(p, q, v),
                4 => Color.FromRgb(t, p, v),
                _ => Color.FromRgb(v, p, q),
            };
        }

        private double RandomDouble(double min, double max)
        {
            return min + (_random.NextDouble() * (max - min));
        }

        private void NewTheme_Click(object? sender, RoutedEventArgs e)
        {
            InitializeDefaultTheme();
            _viewModel.RefreshThemes();
            _viewModel.isDefaultTheme = false;

            // Set default theme name in textbox
            var themeNameTextBox = this.FindControl<TextBox>("ThemeNameTextBox");
            if (themeNameTextBox != null)
            {
                themeNameTextBox.Text = "New Theme";
            }

            // Update current theme name
            currentThemeName = "New Theme";

            // Update the theme name in the bottom bar
            var themeNameText = this.FindControl<TextBlock>("ThemeNameText");
            if (themeNameText != null)
            {
                themeNameText.Text = "Theme: New Theme";
            }

            SaveTheme();
        }

        private async void LoadTheme_Click(object? sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Load Theme",
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter { Name = "INI Files", Extensions = { "ini" } },
                },
            };

            var result = await dialog.ShowAsync(this);
            if (result != null && result.Length > 0)
            {
                _viewModel.isDefaultTheme = false;
                iniParser.Load(result[0]);

                string selectedThemeName = Path.GetFileNameWithoutExtension(result[0]);
                LoadTheme(selectedThemeName);

                var INIEditor = this.FindControl<TextBox>("INIEditor");
                if (INIEditor != null)
                {
                    try
                    {
                        INIEditor.Text = File.ReadAllText(result[0]);
                    }
                    catch (Exception ex)
                    {
                        var warningOutput = this.FindControl<TextBox>("WarningOutput");
                        if (warningOutput != null)
                        {
                            warningOutput.Text = $"Error loading file: {ex.Message}";
                            warningOutput.Foreground = new SolidColorBrush(Colors.Red);
                        }
                    }
                }

                ApplyTheme();
                _viewModel.RefreshThemes();
                _viewModel.isDefaultTheme = false;

                // Update theme selection for combo box
                _viewModel.SelectedTheme = selectedThemeName;

                UpdateColorPreviews();
            }
        }

        private async void ExportTheme_Click(object? sender, RoutedEventArgs e)
        {
            var INIEditor = this.FindControl<TextBox>("INIEditor");
            if (INIEditor == null)
                return;

            // Get theme name from textbox
            var themeNameTextBox = this.FindControl<TextBox>("ThemeNameTextBox");
            string themeName =
                themeNameTextBox != null && !string.IsNullOrWhiteSpace(themeNameTextBox.Text)
                    ? themeNameTextBox.Text
                    : "New Theme"; // Default name if blank

            var dialog = new SaveFileDialog
            {
                Title = "Save Theme",
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter { Name = "INI Files", Extensions = { "ini" } },
                },
                InitialFileName = $"{themeName}.ini",
            };

            var result = await dialog.ShowAsync(this);
            if (!string.IsNullOrEmpty(result))
            {
                try
                {
                    File.WriteAllText(result, INIEditor.Text);

                    // Update current theme name
                    currentThemeName = themeName;

                    // Update the theme name in bottom bar
                    var themeNameText = this.FindControl<TextBlock>("ThemeNameText");
                    if (themeNameText != null)
                    {
                        themeNameText.Text = "Theme: " + themeName;
                    }

                    var warningOutput = this.FindControl<TextBox>("WarningOutput");
                    if (warningOutput != null)
                    {
                        warningOutput.Text = $"Theme '{themeName}' exported successfully.";
                        warningOutput.Foreground = new SolidColorBrush(Colors.Lime);
                    }
                }
                catch (Exception ex)
                {
                    var warningOutput = this.FindControl<TextBox>("WarningOutput");
                    if (warningOutput != null)
                    {
                        warningOutput.Text = $"Error saving theme: {ex.Message}";
                        warningOutput.Foreground = new SolidColorBrush(Colors.Red);
                    }
                }
            }
        }

        private void SaveTheme_Click(object? sender, RoutedEventArgs e)
        {
            if (_viewModel.isDefaultTheme)
            {
                return;
            }

            var INIEditor = this.FindControl<TextBox>("INIEditor");
            if (INIEditor == null)
                return;

            try
            {
                // Get theme name from textbox
                var themeNameTextBox = this.FindControl<TextBox>("ThemeNameTextBox");
                string themeName =
                    themeNameTextBox != null && !string.IsNullOrWhiteSpace(themeNameTextBox.Text)
                        ? themeNameTextBox.Text
                        : "New Theme"; // Default name if blank

                string filePath = Path.Combine(_viewModel.ThemesFolderPath, $"{themeName}.ini");
                string fileName = $"{themeName}.ini";

                // Make sure directory exists
                Directory.CreateDirectory(_viewModel.ThemesFolderPath);

                // Save the theme file
                File.WriteAllText(filePath, INIEditor.Text);

                // Set theme in config
                AppConfig.Instance.Theme = fileName;
                AppConfig.Instance.Save();

                // Update current theme name
                currentThemeName = themeName;

                // Update theme name in bottom bar
                var themeNameText = this.FindControl<TextBlock>("ThemeNameText");
                if (themeNameText != null)
                {
                    themeNameText.Text = "Theme: " + themeName;
                }

                // Update theme list
                if (!_viewModel.Themes.Contains(themeName))
                {
                    _viewModel.RefreshThemes();
                    _viewModel.SelectedTheme = themeName;
                }

                var warningOutput = this.FindControl<TextBox>("WarningOutput");
                if (warningOutput != null)
                {
                    warningOutput.Text = $"Theme '{themeName}' saved successfully.";
                    warningOutput.Foreground = new SolidColorBrush(Colors.Lime);
                }
            }
            catch (Exception ex)
            {
                var warningOutput = this.FindControl<TextBox>("WarningOutput");
                if (warningOutput != null)
                {
                    warningOutput.Text = $"Error saving theme: {ex.Message}";
                    warningOutput.Foreground = new SolidColorBrush(Colors.Red);
                }
            }
        }

        private void OnRandomizeColorsClick(object? sender, RoutedEventArgs e)
        {
            // Generate random colors for all color pickers
            var colorPickers = new Dictionary<string, ColorPicker>
            {
                { "BackgroundColorPicker", this.FindControl<ColorPicker>("BackgroundColorPicker")! },
                { "TextColorPicker", this.FindControl<ColorPicker>("TextColorPicker")! },
                { "ButtonColorPicker", this.FindControl<ColorPicker>("ButtonColorPicker")! },
                { "ButtonTextColorPicker", this.FindControl<ColorPicker>("ButtonTextColorPicker")! },
                { "SecondaryButtonColorPicker", this.FindControl<ColorPicker>("SecondaryButtonColorPicker")! },
                { "SecondaryButtonTextColorPicker", this.FindControl<ColorPicker>("SecondaryButtonTextColorPicker")! },
                { "BorderColorPicker", this.FindControl<ColorPicker>("BorderColorPicker")! },
                { "TertiaryColorPicker", this.FindControl<ColorPicker>("TertiaryColorPicker")! },
                { "BorderBackgroundColorPicker", this.FindControl<ColorPicker>("BorderBackgroundColorPicker")! },
                { "WarningTextColorPicker", this.FindControl<ColorPicker>("WarningTextColorPicker")! },
                { "SettingsTextColorPicker", this.FindControl<ColorPicker>("SettingsTextColorPicker")! },
                { "MenuHoverColorPicker", this.FindControl<ColorPicker>("MenuHoverColorPicker")! },
                { "SystemAccentColorPicker", this.FindControl<ColorPicker>("SystemAccentColorPicker")! }
            };

            // Generate a color scheme
            var baseHue = _random.NextDouble() * 360; // Random base hue
            var colors = GenerateColorScheme(baseHue);

            // Apply colors to pickers
            var colorNames = new List<string>(colors.Keys);
            int colorIndex = 0;

            foreach (var picker in colorPickers.Values)
            {
                if (picker != null && colorIndex < colorNames.Count)
                {
                    picker.Color = colors[colorNames[colorIndex]];
                    colorIndex++;
                }
            }

            // Update the INI editor
            UpdateIniEditor();
        }

        private Dictionary<string, Color> GenerateColorScheme(double baseHue)
        {
            var colors = new Dictionary<string, Color>();

            colors["Background"] = HSVToColor(baseHue, RandomDouble(0.1, 0.6), RandomDouble(0.05, 0.25));
            colors["Text"] = HSVToColor(baseHue, RandomDouble(0.0, 0.3), RandomDouble(0.8, 1.0));
            colors["Button"] = HSVToColor(baseHue, RandomDouble(0.4, 0.9), RandomDouble(0.3, 0.7));
            colors["ButtonText"] = HSVToColor(baseHue, RandomDouble(0.0, 0.2), RandomDouble(0.9, 1.0));
            colors["SecondaryButton"] = HSVToColor(baseHue, RandomDouble(0.2, 0.7), RandomDouble(0.4, 0.8));
            colors["SecondaryButtonText"] = HSVToColor(baseHue, RandomDouble(0.0, 0.3), RandomDouble(0.1, 0.3));
            colors["Border"] = HSVToColor(baseHue, RandomDouble(0.3, 0.8), RandomDouble(0.4, 0.8));
            colors["Tertiary"] = HSVToColor(baseHue, RandomDouble(0.2, 0.6), RandomDouble(0.15, 0.4));
            colors["BorderBackground"] = HSVToColor(baseHue, RandomDouble(0.1, 0.5), RandomDouble(0.1, 0.3));
            colors["WarningText"] = HSVToColor(RandomDouble(20, 50), RandomDouble(0.6, 1.0), RandomDouble(0.8, 1.0));
            colors["SettingsText"] = HSVToColor(baseHue, RandomDouble(0.1, 0.4), RandomDouble(0.5, 0.8));
            colors["MenuHover"] = HSVToColor(baseHue, RandomDouble(0.3, 0.8), RandomDouble(0.4, 0.7));
            colors["SystemAccent"] = HSVToColor(baseHue, RandomDouble(0.5, 1.0), RandomDouble(0.6, 0.9));

            return colors;
        }

    }
}
