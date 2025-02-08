using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Diagnostics;
using System;

namespace PD3AudioModder
{
    public partial class LicensesWindow : Window
    {
        public LicensesWindow()
        {
            InitializeComponent();
        }

        private void OnWwisePd3Clicked(object sender, RoutedEventArgs e)
        {
            string url = "https://github.com/YourRepo/wwise_pd3";
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception)
            {
            }
        }

        private void OnCloseClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
