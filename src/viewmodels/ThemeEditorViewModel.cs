using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;

namespace PD3AudioModder.ViewModels
{
    public class ThemeEditorViewModel : ViewModelBase
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _isDefaultTheme = true;
        public bool isDefaultTheme
        {
            get => _isDefaultTheme;
            set
            {
                if (_isDefaultTheme != value)
                {
                    _isDefaultTheme = value;
                    OnPropertyChanged(nameof(isDefaultTheme));
                }
            }
        }

        private string _selectedTheme;
        public ObservableCollection<string> Themes { get; } = new();

        public string SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (SetProperty(ref _selectedTheme, value) && value != null)
                {
                    LoadThemeByName(value);
                }
            }
        }

        public void LoadThemeByName(string themeName)
        {
            isDefaultTheme = themeName.Equals("Default", StringComparison.OrdinalIgnoreCase);
            string themeFilePath = Path.Combine(ThemesFolderPath, $"{themeName}.ini");
            ThemeSelected?.Invoke(this, themeFilePath);

            AppConfig.Instance.Theme = themeName;
            AppConfig.Instance.Save();
        }

        public event EventHandler<string> ThemeSelected;

        public string ThemesFolderPath { get; } =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PD3AudioModder",
                "Themes"
            );

        public ThemeEditorViewModel()
        {
            LoadThemes();

            string savedTheme = AppConfig.Instance.Theme;
            if (Themes.Contains(savedTheme))
            {
                SelectedTheme = savedTheme;
            }
        }

        public void LoadThemes()
        {
            string theme = AppConfig.Instance.Theme;

            Themes.Clear();
            Themes.Add("Default");

            if (!Directory.Exists(ThemesFolderPath))
            {
                Directory.CreateDirectory(ThemesFolderPath);
            }

            foreach (var file in Directory.GetFiles(ThemesFolderPath, "*.ini"))
            {
                string themeName = Path.GetFileNameWithoutExtension(file);
                if (!themeName.Equals("Default", StringComparison.OrdinalIgnoreCase))
                {
                    Themes.Add(themeName);
                }
            }

            // Try to select the theme from config
            if (Themes.Contains(theme))
            {
                SelectedTheme = theme;
            }
            // Fall back to default if theme isn't available
            else if (Themes.Count > 0)
            {
                SelectedTheme = Themes[0];
            }
        }

        public void RefreshThemes()
        {
            LoadThemes();
            OnPropertyChanged(nameof(Themes));
        }
    }
}
