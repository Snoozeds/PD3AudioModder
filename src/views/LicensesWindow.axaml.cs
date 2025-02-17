using Avalonia.Controls;
using System;
using System.Diagnostics;
using System.Windows.Input;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Linq;

namespace PD3AudioModder
{
    public partial class LicensesWindow : Window
    {
        public ICommand WwisePd3Command { get; }
        public ICommand TwemojiCommand { get; }
        public ICommand CloseCommand { get; }

        public LicensesWindow()
        {
            InitializeComponent();
            WwisePd3Command = ReactiveCommand.Create(
                () => OpenUrl("https://github.com/MoolahModding/wwise_pd3"),
                outputScheduler: RxApp.MainThreadScheduler);

            TwemojiCommand = ReactiveCommand.Create(
                () => OpenUrl("https://github.com/twitter/twemoji"),
                outputScheduler: RxApp.MainThreadScheduler);

            CloseCommand = ReactiveCommand.Create(
                () => this.Close(),
                outputScheduler: RxApp.MainThreadScheduler);

            DataContext = this;
        }

        private void OpenUrl(string url)
        {
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
    }
}
