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
        SwitchButton.Content = Loc.T("MenuSwitchNow");
        GearButton.ToolTip = Loc.T("StSettingsTip");
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
                           double fraction, AppSettings settings, Stats stats)
    {
        UpdateStats(stats);

        if (paused)
        {
            EmojiText.Text = "⏸";
            StateText.Text = Loc.T("StPaused");
            ElapsedText.Text = Loc.T("StPausedSub");
            ProgressPanel.Visibility = Visibility.Collapsed;
            SwitchButton.Visibility = Visibility.Collapsed;
            return;
        }

        if (phase == Phase.Idle)
        {
            EmojiText.Text = "🌙";
            StateText.Text = Loc.T("StIdle");
            ElapsedText.Text = Loc.F("StIdleSub", settings.WorkStart, settings.WorkEnd);
            ProgressPanel.Visibility = Visibility.Collapsed;
            SwitchButton.Visibility = Visibility.Collapsed;
            return;
        }

        bool sitting = phase == Phase.Sitting;
        EmojiText.Text = sitting ? "🪑" : "🧍";
        StateText.Text = Loc.T(sitting ? "StSitting" : "StStanding");
        ElapsedText.Text = Loc.F("StElapsed", Fmt(elapsed));

        ProgressPanel.Visibility = Visibility.Visible;
        SwitchButton.Visibility = Visibility.Visible;

        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        RemainingText.Text = remaining == TimeSpan.Zero
            ? Loc.T(sitting ? "StTimeToStand" : "StTimeToSit")
            : Loc.F(sitting ? "StRemainStand" : "StRemainSit", Fmt(remaining));

        Fill.Background = (Brush)FindResource(sitting ? "AccentSitBrush" : "AccentStandBrush");
        fraction = Math.Clamp(fraction, 0, 1);
        Fill.Width = Track.ActualWidth > 0 ? fraction * Track.ActualWidth : 0;
    }

    private static string Fmt(TimeSpan t) =>
        t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"mm\:ss");

    // ---------- Daily statistics (last 7 days) ----------

    private const double BarAreaHeight = 42;

    private void UpdateStats(Stats stats)
    {
        StatsTitle.Text = Loc.T("StStatsTitle");
        var today = stats.Today;
        TodayText.Text = $"🪑 {FmtHours(today.SitSeconds)}  ·  🧍 {FmtHours(today.StandSeconds)}";

        var week = stats.LastWeek();
        int maxDay = Math.Max(week.Max(w => w.Day.SitSeconds + w.Day.StandSeconds), 1);
        var culture = new System.Globalization.CultureInfo(Loc.Lang == "en" ? "en-US" : "sk-SK");
        var sitBrush = (Brush)FindResource("AccentSitBrush");
        var standBrush = (Brush)FindResource("AccentStandBrush");
        var emptyBrush = (Brush)FindResource("InputBrush");

        ChartGrid.Children.Clear();
        foreach (var (date, day) in week)
        {
            var bar = new System.Windows.Controls.StackPanel
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(5, 0, 5, 0)
            };

            double standH = day.StandSeconds * BarAreaHeight / maxDay;
            double sitH = day.SitSeconds * BarAreaHeight / maxDay;

            if (day.StandSeconds > 0)
                bar.Children.Add(new System.Windows.Controls.Border
                {
                    Height = Math.Max(standH, 2),
                    Background = standBrush,
                    CornerRadius = new CornerRadius(2, 2, 0, 0)
                });
            if (day.SitSeconds > 0)
                bar.Children.Add(new System.Windows.Controls.Border
                {
                    Height = Math.Max(sitH, 2),
                    Background = sitBrush,
                    CornerRadius = day.StandSeconds > 0
                        ? new CornerRadius(0)
                        : new CornerRadius(2, 2, 0, 0)
                });
            if (day.SitSeconds == 0 && day.StandSeconds == 0)
                bar.Children.Add(new System.Windows.Controls.Border
                {
                    Height = 2,
                    Background = emptyBrush
                });

            var barArea = new System.Windows.Controls.Grid { Height = BarAreaHeight };
            barArea.Children.Add(bar);

            var label = new System.Windows.Controls.TextBlock
            {
                Text = date.ToString("ddd", culture).TrimEnd('.'),
                FontSize = 10,
                Foreground = (Brush)FindResource("SubTextBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };

            var cell = new System.Windows.Controls.StackPanel
            {
                ToolTip = $"{date:d.M.}  🪑 {FmtHours(day.SitSeconds)} · 🧍 {FmtHours(day.StandSeconds)}"
            };
            cell.Children.Add(barArea);
            cell.Children.Add(label);
            ChartGrid.Children.Add(cell);
        }
    }

    private static string FmtHours(int seconds)
    {
        var t = TimeSpan.FromSeconds(seconds);
        return $"{(int)t.TotalHours}:{t.Minutes:D2}";
    }

    private void Gear_Click(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();

    private void Switch_Click(object sender, RoutedEventArgs e) => SwitchRequested?.Invoke();
}
