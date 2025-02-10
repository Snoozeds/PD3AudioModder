using Avalonia.Controls;
using System.Threading.Tasks;

namespace PD3AudioModder
{
    public partial class UpdateDialog : Window
    {
        public bool UserConfirmed { get; private set; } = false;

        public UpdateDialog()
        {
            InitializeComponent();
        }

        public UpdateDialog(string newVersion)
        {
            InitializeComponent();
            this.FindControl<TextBlock>("VersionText")!.Text =
                string.Format("New version: {0}", newVersion);
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

        public static async Task<bool> ShowDialogAsync(Window parent, string newVersion)
        {
            var dialog = new UpdateDialog(newVersion);
            var result = await dialog.ShowDialog<bool>(parent);
            return result;
        }
    }

}