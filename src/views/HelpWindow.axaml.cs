using Avalonia.Controls;

namespace PD3AudioModder
{
    public partial class HelpWindow : Window
    {
        public HelpWindow()
        {
            InitializeComponent();
        }

        public HelpWindow(string activeTab) : this()
        {
            InitializeComponent();

            var singleFileSection = this.FindControl<StackPanel>("SingleFileSection");
            var batchConversionSection = this.FindControl<StackPanel>("BatchConversionSection");

            // Show info based off of what tab user is in
            if (activeTab == "SingleFile")
            {
                singleFileSection!.IsVisible = true;
                batchConversionSection!.IsVisible = false;
            }
            else if (activeTab == "BatchConversion")
            {
                singleFileSection!.IsVisible = false;
                batchConversionSection!.IsVisible = true;
            }
        }

        private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close();
        }
    }
}
