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
            TitleText.Text = "Čas postaviť sa!";
            SubtitleText.Text = $"Zdvihni stôl a pracuj v stoji ďalších {settings.StandMinutes} minút.";
            AcceptButton.Content = "Stojím ✓";
            AcceptButton.Background = (Brush)FindResource("AccentStandBrush");
        }
        else
        {
            EmojiText.Text = "🪑";
            EmojiBadge.Background = new SolidColorBrush(Color.FromArgb(40, 0x5B, 0x8D, 0xEF));
            TitleText.Text = "Môžeš si sadnúť";
            SubtitleText.Text = $"Spusti stôl a pohodlne si sadni na {settings.SitMinutes} minút.";
            AcceptButton.Content = "Sedím ✓";
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
