using System.Windows;
using System.Windows.Media;

namespace StandReminder;

public partial class StatusWindow : Window
{
    public event Action? SettingsRequested;
    public event Action? SwitchRequested;

    private bool _isClosing;

    public StatusWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => PositionBottomRight();
        Deactivated += (_, _) => CloseSafe(); // behave like a tray flyout
    }

    /// <summary>
    /// WPF throws if Close() is called while the window is already closing —
    /// and Deactivated fires during Close (e.g. gear click opens settings,
    /// which deactivates this flyout mid-close). Always close through here.
    /// </summary>
    public void CloseSafe()
    {
        if (_isClosing) return;
        _isClosing = true;
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _isClosing = true;
        base.OnClosing(e);
    }

    private void PositionBottomRight()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - 4;
        Top = area.Bottom - ActualHeight - 4;
    }

    public void UpdateView(Phase phase, bool paused, TimeSpan elapsed, TimeSpan remaining,
                           double fraction, AppSettings settings)
    {
        if (paused)
        {
            EmojiText.Text = "⏸";
            StateText.Text = "Pozastavené";
            ElapsedText.Text = "Pripomienky sú vypnuté";
            ProgressPanel.Visibility = Visibility.Collapsed;
            SwitchButton.Visibility = Visibility.Collapsed;
            return;
        }

        if (phase == Phase.Idle)
        {
            EmojiText.Text = "🌙";
            StateText.Text = "Mimo pracovného času";
            ElapsedText.Text = $"Pripomínam medzi {settings.WorkStart} a {settings.WorkEnd}";
            ProgressPanel.Visibility = Visibility.Collapsed;
            SwitchButton.Visibility = Visibility.Collapsed;
            return;
        }

        bool sitting = phase == Phase.Sitting;
        EmojiText.Text = sitting ? "🪑" : "🧍";
        StateText.Text = sitting ? "Sedíš" : "Stojíš";
        ElapsedText.Text = $"V tejto pozícii už {Fmt(elapsed)}";

        ProgressPanel.Visibility = Visibility.Visible;
        SwitchButton.Visibility = Visibility.Visible;

        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        RemainingText.Text = remaining == TimeSpan.Zero
            ? (sitting ? "Čas postaviť sa!" : "Môžeš si sadnúť")
            : $"Zostáva {Fmt(remaining)} {(sitting ? "do státia" : "do sedenia")}";

        Fill.Background = (Brush)FindResource(sitting ? "AccentSitBrush" : "AccentStandBrush");
        fraction = Math.Clamp(fraction, 0, 1);
        Fill.Width = Track.ActualWidth > 0 ? fraction * Track.ActualWidth : 0;
    }

    private static string Fmt(TimeSpan t) =>
        t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"mm\:ss");

    private void Gear_Click(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();

    private void Switch_Click(object sender, RoutedEventArgs e) => SwitchRequested?.Invoke();
}
