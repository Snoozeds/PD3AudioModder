using System.Threading.Tasks;
using Avalonia.Controls;

namespace PD3AudioModder
{
    public partial class UpdateDialog : Window
    {
        public bool UserConfirmed { get; private set; } = false;

        public UpdateDialog()
        {
            InitializeComponent();
        }

        public UpdateDialog(string currentVersion, string newVersion)
        {
            InitializeComponent();
            this.FindControl<TextBlock>("CurrentVersionText")!.Text = string.Format(
                "Current version: {0}",
                currentVersion
            );
            this.FindControl<TextBlock>("VersionText")!.Text = string.Format(
                "New version: {0}",
                newVersion
            );
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
