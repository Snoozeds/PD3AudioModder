using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia.Controls;

namespace PD3AudioModder
{
    public partial class HelpWindow : Window
    {
        public HelpWindow()
        {
            InitializeComponent();
        }

        public HelpWindow(string activeTab)
            : this()
        {
            InitializeComponent();

            var sections = new Dictionary<string, StackPanel>
            {
                { "SingleFile", this.FindControl<StackPanel>("SingleFileSection")! },
                { "BatchConversion", this.FindControl<StackPanel>("BatchConversionSection")! },
                { "PackFiles", this.FindControl<StackPanel>("PackFilesSection")! },
                { "IDSearch", this.FindControl<StackPanel>("IDSearchSection")! },
            };

            foreach (var section in sections)
            {
                section.Value.IsVisible = section.Key == activeTab;
            }
        }

        private void GuideClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            string url =
                "https://docs.google.com/document/d/1M7aicj57HXp92XPSMSg4KyEDkoc3iNzshaA18rIsUXw/edit?usp=sharing";

            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open URL: {url}. Error: {ex.Message}");
            }
        }

        private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close();
        }
    }
}
