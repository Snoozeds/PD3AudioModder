using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace PD3AudioModder
{
    public partial class UpdateDialog : Window
    {
        private readonly HttpClient _httpClient = new HttpClient();
        public bool UserConfirmed { get; private set; } = false;

        public UpdateDialog()
        {
            InitializeComponent();
        }

        public UpdateDialog(string currentVersion, string newVersion)
        {
            InitializeComponent();

            // Theme colours
            var textColor = this.FindResource("TextColor") as Color? ?? Color.Parse("#BBE1FA");
            var warningColor =
                this.FindResource("WarningTextColor") as Color? ?? Color.Parse("#E6C400");

            // Version display
            string displayVersion =
                currentVersion == "0.0.0" ? "0.0.0 (version.txt missing?)" : currentVersion;

            // Set version texts
            this.FindControl<TextBlock>("CurrentVersionText")!.Text = string.Format(
                "Current version: {0}",
                displayVersion
            );
            this.FindControl<TextBlock>("VersionText")!.Text = string.Format(
                "New version: {0}",
                newVersion
            );

            // Load the commit history between versions
            LoadCommitHistory(currentVersion, newVersion, textColor, warningColor);
        }

        private async void LoadCommitHistory(
            string currentVersion,
            string newVersion,
            Color textColor,
            Color warningColor
        )
        {
            var commitsPanel = this.FindControl<StackPanel>("CommitsPanel")!;
            commitsPanel.Children.Clear();

            // Add loading indicator
            var loadingText = new SelectableTextBlock
            {
                Text = "Loading changes...",
                Foreground = new SolidColorBrush(textColor),
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
            };
            commitsPanel.Children.Add(loadingText);

            try
            {
                // Set up GitHub API request
                string url =
                    $"https://api.github.com/repos/Snoozeds/PD3AudioModder/compare/{currentVersion}...{newVersion}";
                _httpClient.DefaultRequestHeaders.UserAgent.Add(
                    new ProductInfoHeaderValue("PD3AudioModder", "1.0")
                );
                var response = await _httpClient.GetStringAsync(url);

                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                commitsPanel.Children.Clear();

                // Add header for changes
                var headerText = new SelectableTextBlock
                {
                    Text = $"Changes since {currentVersion}:",
                    Foreground = new SolidColorBrush(textColor),
                    FontSize = 14,
                    FontWeight = FontWeight.Bold,
                    Margin = new Thickness(0, 0, 0, 5),
                    TextWrapping = TextWrapping.Wrap,
                };
                commitsPanel.Children.Add(headerText);

                // Process commits
                if (root.TryGetProperty("commits", out var commits))
                {
                    int commitCount = 0;
                    foreach (var commit in commits.EnumerateArray())
                    {
                        if (
                            commit.TryGetProperty("commit", out var commitInfo)
                            && commitInfo.TryGetProperty("message", out var message)
                        )
                        {
                            // Get only the first line of commit message
                            string commitMessage =
                                message.GetString()?.Split('\n')[0].Trim() ?? "Unknown commit";

                            // Skip unnecesary commits
                            if (
                                commitMessage.Contains(
                                    "Update version.txt",
                                    StringComparison.OrdinalIgnoreCase
                                )
                                || commitMessage.Contains(
                                    "Revert version.txt",
                                    StringComparison.OrdinalIgnoreCase
                                )
                                || commitMessage.Contains(
                                    "Update README.md",
                                    StringComparison.OrdinalIgnoreCase
                                )
                                || commitMessage.Contains(
                                    "Merge branch 'main' of https://github.com/Snoozeds/PD3AudioModder",
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                            {
                                continue;
                            }

                            var commitText = new TextBlock
                            {
                                Text = $"• {commitMessage}",
                                Foreground = new SolidColorBrush(textColor),
                                FontSize = 14,
                                TextWrapping = TextWrapping.Wrap,
                                Margin = new Thickness(0, 0, 0, 5),
                            };
                            commitsPanel.Children.Add(commitText);
                            commitCount++;
                        }
                    }

                    // Update commit count text
                    var commitCountText = this.FindControl<TextBlock>("CommitCount")!;
                    commitCountText.Text = $"{commitCount} commits since last update.";

                    if (commitCount == 0)
                    {
                        var noChangesText = new SelectableTextBlock
                        {
                            Text = "No changes found between these versions.",
                            Foreground = new SolidColorBrush(textColor),
                            FontSize = 14,
                            TextWrapping = TextWrapping.Wrap,
                        };
                        commitsPanel.Children.Add(noChangesText);
                    }
                }
                else
                {
                    var errorText = new SelectableTextBlock
                    {
                        Text = "No commits found between these versions.",
                        Foreground = new SolidColorBrush(textColor),
                        FontSize = 14,
                        TextWrapping = TextWrapping.Wrap,
                    };
                    commitsPanel.Children.Add(errorText);
                }
            }
            catch (Exception ex)
            {
                commitsPanel.Children.Clear();
                var errorText = new SelectableTextBlock
                {
                    Text = $"Could not load changes: {ex.Message}",
                    Foreground = new SolidColorBrush(warningColor),
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap,
                };
                commitsPanel.Children.Add(errorText);
            }
        }

        private void OnYesClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            UserConfirmed = true;
            Close(true);
        }

        private void OnNoClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            UserConfirmed = false;
            Close(false);
        }

        public static async Task<bool> ShowDialogAsync(
            Window parent,
            string currentVersion,
            string newVersion
        )
        {
            var dialog = new UpdateDialog(currentVersion, newVersion);
            var result = await dialog.ShowDialog<bool>(parent);
            return result;
        }
    }
}
