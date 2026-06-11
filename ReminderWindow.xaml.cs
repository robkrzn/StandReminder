using System.Windows;
using System.Windows.Media;

namespace StandReminder;

public partial class ReminderWindow : Window
{
    public event Action? Accepted;
    public event Action? Snoozed;

    public ReminderWindow(Phase target)
    {
        InitializeComponent();

        var settings = AppSettings.Load();

        if (target == Phase.Standing)
        {
            EmojiText.Text = "🧍";
            EmojiBadge.Background = new SolidColorBrush(Color.FromArgb(40, 0x46, 0xC2, 0x8E));
            TitleText.Text = Loc.T("RemTitleStand");
            SubtitleText.Text = Loc.F("RemBodyStand", settings.StandMinutes);
            AcceptButton.Content = Loc.T("RemBtnStand");
            AcceptButton.Background = (Brush)FindResource("AccentStandBrush");
        }
        else
        {
            EmojiText.Text = "🪑";
            EmojiBadge.Background = new SolidColorBrush(Color.FromArgb(40, 0x5B, 0x8D, 0xEF));
            TitleText.Text = Loc.T("RemTitleSit");
            SubtitleText.Text = Loc.F("RemBodySit", settings.SitMinutes);
            AcceptButton.Content = Loc.T("RemBtnSit");
            AcceptButton.Background = (Brush)FindResource("AccentSitBrush");
        }

        Loaded += (_, _) => PositionBottomRight();
    }

    private void PositionBottomRight()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - 8;
        Top = area.Bottom - ActualHeight - 8;
    }

    private void Accept_Click(object sender, RoutedEventArgs e) => Accepted?.Invoke();

    private void Snooze_Click(object sender, RoutedEventArgs e) => Snoozed?.Invoke();
}
