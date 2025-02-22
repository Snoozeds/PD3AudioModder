using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PD3AudioModder;

public partial class WarningConfirmDialog : Window
{
    public bool UserResponse { get; set; }
    public bool YesToAllResponse { get; set; }
    private TextBlock? _messageTextBlock;
    private TextBlock? _message2TextBlock;
    private readonly AudioPlayer _audioPlayer = new AudioPlayer();
    private AppConfig _config = AppConfig.Instance;

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

    public string? Message2
    {
        get => _message2TextBlock?.Text;
        set
        {
            if (_message2TextBlock != null)
            {
                _message2TextBlock.Text = value;
            }
        }
    }

    public WarningConfirmDialog()
    {
        InitializeComponent();
        _messageTextBlock = this.FindControl<TextBlock>("MessageTextBlock");
        _message2TextBlock = this.FindControl<TextBlock>("Message2TextBlock");
        this.DataContext = this;
        this.Loaded += WarningConfirmDialog_Loaded;
    }

    public WarningConfirmDialog(string message, string message2)
        : this()
    {
        Message = message;
        Message2 = message2;
    }

    private void WarningConfirmDialog_Loaded(object? sender, RoutedEventArgs e)
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
        if (
            _message2TextBlock != null
            && string.IsNullOrEmpty(_message2TextBlock.Text)
            && Message2 != null
        )
        {
            _message2TextBlock.Text = Message2;
        }
        if (!_config.MuteNotificationSound)
        {
            Task.Run(() => _audioPlayer.PlaySound("assets.sounds.error.ogg"));
        }
    }

    private void OnYesClick(object? sender, RoutedEventArgs e)
    {
        UserResponse = true;
        YesToAllResponse = false;
        Close(true);
    }

    private void OnNoClick(object? sender, RoutedEventArgs e)
    {
        UserResponse = false;
        YesToAllResponse = false;
        Close(false);
    }

    private void YesToAllClick(object? sender, RoutedEventArgs e)
    {
        UserResponse = true;
        YesToAllResponse = true;
        Close(true);
    }

    private void WarningCheckChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
        {
            AppConfig.Instance.DisplayFilesInExportWarning = !checkBox.IsChecked ?? false;
            AppConfig.Instance.Save();
        }
    }
}
