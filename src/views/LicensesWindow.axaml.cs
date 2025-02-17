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

        private void WwisePD3Click(object sender, RoutedEventArgs e)
        {
            string url = "https://github.com/MoolahModding/wwise_pd3";
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

        private void TwemojiClick(object sender, RoutedEventArgs e) {
            string url = "https://github.com/twitter/twemoji";
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

        private void CloseClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
