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
            var INIEditor = this.FindControl<TextBox>("INIEditor");
            if (INIEditor != null)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("[Theme]");

                foreach (var kvp in themeColors)
                {
                    sb.AppendLine($"{kvp.Key}={ColorToHexString(kvp.Value)}");
                }

                INIEditor.Text = sb.ToString();
            }
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
            if (warningOutput == null) return;

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
                    errorMessage.AppendLine($"Colors not defined in INI: {string.Join(", ", missingColors)}");
                }

                if (invalidColors.Count > 0)
                {
                    errorMessage.AppendLine($"Invalid colors in INI: {string.Join(", ", invalidColors)}");
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
    }
}
