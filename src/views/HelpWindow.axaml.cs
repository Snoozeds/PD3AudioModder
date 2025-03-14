using System.Collections.Generic;
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

        private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close();
        }
    }
}
