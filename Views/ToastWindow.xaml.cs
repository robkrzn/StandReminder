using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace StandReminder;

/// <summary>
/// Lightweight dark toast in the bottom-right corner — non-activating, auto-dismisses
/// after a few seconds, click to close. Used for background notifications (e.g. a
/// completed self-update) that need to match the app's look, unlike a tray balloon.
/// </summary>
public partial class ToastWindow : Window
{
    private readonly DispatcherTimer _life;

    public ToastWindow(string emoji, string title, string message, bool positive)
    {
        InitializeComponent();

        EmojiText.Text = emoji;
        TitleText.Text = title;
        MessageText.Text = message;

        // translucent green badge for success, amber for a problem
        EmojiBadge.Background = positive
            ? new SolidColorBrush(Color.FromArgb(0x26, 0x46, 0xC2, 0x8E))
            : new SolidColorBrush(Color.FromArgb(0x26, 0xEF, 0x9B, 0x5B));

        Loaded += (_, _) => PositionBottomRight();

        _life = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _life.Tick += (_, _) => Dismiss();
        _life.Start();
    }

    private void PositionBottomRight()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - 5;
        Top = area.Bottom - ActualHeight - 5;
    }

    private void Dismiss()
    {
        _life.Stop();
        Close();
    }

    private void Dismiss(object sender, MouseButtonEventArgs e) => Dismiss();
}
