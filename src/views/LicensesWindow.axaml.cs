using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PD3AudioModder
{
    public partial class LicensesWindow : Window
    {
        private readonly Dictionary<string, string> urlMappings = new()
        {
            { "WwisePD3", "https://github.com/MoolahModding/wwise_pd3" },
            { "Twemoji", "https://github.com/twitter/twemoji" },
            { "Avalonia", "https://github.com/AvaloniaUI/Avalonia" },
            { "AvaloniaSvgSkia", "https://github.com/wieslawsoltes/Svg.Skia" },
            { "NAudio", "https://github.com/naudio/NAudio" },
            { "NAudioVorbis", "https://github.com/naudio/Vorbis" },
            { "NewtonsoftJson", "https://github.com/JamesNK/Newtonsoft.Json" },
            { "ReactiveUI", "https://github.com/reactiveui/reactiveui" },
            { "TablerIcons", "https://github.com/tabler/tabler-icons" },
            { "DiscordRichPresence", "https://github.com/Lachee/discord-rpc-csharp" },
        };

        public LicensesWindow()
        {
            InitializeComponent();
        }

        private void LaunchUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open URL: {url}. Error: {ex.Message}");
            }
        }

        private void OpenURL(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string url)
            {
                LaunchUrl(url);
            }
        }

        private void HandleUrlClick(object sender, RoutedEventArgs e)
        {
            if (
                sender is Button button
                && button.Name is string name
                && urlMappings.TryGetValue(name, out var url)
            )
            {
                LaunchUrl(url);
            }
        }

        private void CloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
