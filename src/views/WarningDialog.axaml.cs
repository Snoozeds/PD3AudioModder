using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;

namespace PD3AudioModder
{
    public partial class WarningDialog : Window
    {
        private TextBlock? _messageTextBlock;
        private readonly AudioPlayer _audioPlayer = new AudioPlayer();

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
            InitializeComponent();
            _messageTextBlock = this.FindControl<TextBlock>("MessageTextBlock");
            this.DataContext = this;
            this.Loaded += WarningDialog_Loaded;
        }

        public WarningDialog(string message) : this()
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
            if (_messageTextBlock != null && string.IsNullOrEmpty(_messageTextBlock.Text) && Message != null)
            {
                _messageTextBlock.Text = Message;
            }

            Task.Run(() => _audioPlayer.PlaySound("assets.sounds.error.ogg"));
        }
    }
}