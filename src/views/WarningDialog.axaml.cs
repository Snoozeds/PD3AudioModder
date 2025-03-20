using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PD3AudioModder
{
    public partial class WarningDialog : Window
    {
        private TextBlock? _messageTextBlock;
        private readonly AudioPlayer _audioPlayer = new AudioPlayer();
        private AppConfig _config = AppConfig.Instance;

        private static int _openDialogCount = 0; // Only allow 3 warning dialogs to be open at once
        private static readonly object _lock = new object();

        public string? Message
        {
            get => _messageTextBlock?.Text;
            set
            {
                if (_messageTextBlock != null)
                {
                    _messageTextBlock.Text = value;
                }
            }
        }

        public WarningDialog()
        {
            lock (_lock)
            {
                if (_openDialogCount >= 3)
                {
                    this.Close();
                    return;
                }

                _openDialogCount++;
            }

            InitializeComponent();
            _messageTextBlock = this.FindControl<TextBlock>("MessageTextBlock");
            this.DataContext = this;
            this.Loaded += WarningDialog_Loaded;
            this.Closed += WarningDialog_Closed;
        }

        public WarningDialog(string message)
            : this()
        {
            Message = message;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void WarningDialog_Loaded(object? sender, RoutedEventArgs e)
        {
            // if message was set before the window was loaded, set the text block text
            if (
                _messageTextBlock != null
                && string.IsNullOrEmpty(_messageTextBlock.Text)
                && Message != null
            )
            {
                _messageTextBlock.Text = Message;
            }

            if (!_config.MuteNotificationSound)
            {
                Task.Run(() => _audioPlayer.PlaySound("assets.sounds.error.ogg"));
            }
        }

        private void WarningDialog_Closed(object? sender, System.EventArgs e)
        {
            lock (_lock)
            {
                _openDialogCount--;
            }
        }
    }
}
