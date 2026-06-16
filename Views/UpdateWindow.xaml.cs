using System.Diagnostics;
using System.Windows;

namespace StandReminder;

public partial class UpdateWindow : Window
{
    public event Action? UpdateRequested;
    public event Action? Skipped;

    private readonly UpdateInfo _info;

    public UpdateWindow(UpdateInfo info, Version current)
    {
        InitializeComponent();
        _info = info;

        TitleText.Text = Loc.T("UpdTitle");
        VersionText.Text = Loc.F("UpdVersion", FormatVersion(current), info.TagName.TrimStart('v', 'V'));
        NotesHeader.Text = Loc.T("UpdNotesHeader");
        NotesText.Text = string.IsNullOrWhiteSpace(info.Notes) ? Loc.T("UpdNoNotes") : info.Notes;

        UpdateButton.Content = Loc.T("UpdBtnUpdate");
        LaterButton.Content = Loc.T("UpdBtnLater");
        SkipButton.Content = Loc.T("UpdBtnSkip");
        PageLinkText.Text = Loc.T("UpdOpenPage");

        string? page = !string.IsNullOrEmpty(info.HtmlUrl) ? info.HtmlUrl : info.DownloadUrl;
        if (string.IsNullOrEmpty(page))
            PageLink.IsEnabled = false;

        // No installable asset → guide the user to the page instead of a dead button.
        if (string.IsNullOrEmpty(info.DownloadUrl))
            UpdateButton.Visibility = Visibility.Collapsed;

        Loaded += (_, _) => PositionBottomRight();
    }

    private void PositionBottomRight()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - 5;
        Top = area.Bottom - ActualHeight - 5;
    }

    private static string FormatVersion(Version v) =>
        v.Build >= 0 ? $"{v.Major}.{v.Minor}.{v.Build}" : $"{v.Major}.{v.Minor}";

    /// <summary>Switch to the download/progress view; the buttons are no longer actionable.</summary>
    public void SetDownloading()
    {
        ButtonsRow.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;
        StatusText.Text = Loc.F("UpdDownloading", 0);
        Fill.Width = 0;
    }

    public void SetProgress(double fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        Fill.Width = Track.ActualWidth > 0 ? fraction * Track.ActualWidth : 0;
        StatusText.Text = fraction >= 1.0
            ? Loc.T("UpdInstalling")
            : Loc.F("UpdDownloading", (int)(fraction * 100));
    }

    /// <summary>Download/install failed — restore the buttons and explain.</summary>
    public void ShowError(string message)
    {
        ProgressPanel.Visibility = Visibility.Collapsed;
        ButtonsRow.Visibility = Visibility.Visible;
        NotesText.Text = message;
    }

    private void Update_Click(object sender, RoutedEventArgs e) => UpdateRequested?.Invoke();

    private void Skip_Click(object sender, RoutedEventArgs e) => Skipped?.Invoke();

    private void Later_Click(object sender, RoutedEventArgs e) => Close();

    private void OpenPage_Click(object sender, RoutedEventArgs e)
    {
        string? url = !string.IsNullOrEmpty(_info.HtmlUrl) ? _info.HtmlUrl : _info.DownloadUrl;
        if (string.IsNullOrEmpty(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* no default browser / blocked — nothing we can do, stay silent */ }
    }
}
