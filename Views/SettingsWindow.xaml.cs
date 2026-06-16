using System.Windows;

namespace StandReminder;

public partial class SettingsWindow : Window
{
    public event Action? Saved;

    private readonly AppSettings _settings;
    private bool _loaded;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
            UiNative.UseDarkTitleBar(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        _settings = settings;

        for (int h = 5; h <= 21; h++)
        {
            StartCombo.Items.Add($"{h:00}:00");
            StartCombo.Items.Add($"{h:00}:30");
            EndCombo.Items.Add($"{h:00}:00");
            EndCombo.Items.Add($"{h:00}:30");
        }

        SitSlider.Value = _settings.SitMinutes;
        StandSlider.Value = _settings.StandMinutes;
        StartCombo.SelectedItem = _settings.WorkStart;
        EndCombo.SelectedItem = _settings.WorkEnd;
        if (StartCombo.SelectedItem == null) StartCombo.SelectedItem = "07:00";
        if (EndCombo.SelectedItem == null) EndCombo.SelectedItem = "16:00";
        SoundCheck.IsChecked = _settings.PlaySound;
        AutostartCheck.IsChecked = _settings.StartWithWindows;
        AutoUpdateCheck.IsChecked = _settings.AutoUpdateCheck;

        LangCombo.Items.Add("Slovenčina");
        LangCombo.Items.Add("English");
        LangCombo.SelectedIndex = _settings.Language == "en" ? 1 : 0;

        ApplyTexts();
        _loaded = true;
        UpdateLabels();
    }

    private void ApplyTexts()
    {
        Title = Loc.T("SetTitle");
        HeaderText.Text = Loc.T("SetHeader");
        var v = typeof(App).Assembly.GetName().Version ?? new Version(0, 0);
        VersionText.Text = Loc.F("SetVersion", $"{v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}");
        IntervalsHeader.Text = Loc.T("SetIntervals");
        RatioHint.Text = Loc.T("SetRatioHint");
        WorkHoursHeader.Text = Loc.T("SetWorkHours");
        FromLabel.Text = Loc.T("SetFrom");
        ToLabel.Text = Loc.T("SetTo");
        WorkHint.Text = Loc.T("SetWorkHint");
        SoundCheck.Content = Loc.T("SetSound");
        AutostartCheck.Content = Loc.T("SetAutostart");
        AutoUpdateCheck.Content = Loc.T("SetAutoUpdate");
        LangLabel.Text = Loc.T("SetLanguage");
        SaveButton.Content = Loc.T("SetSave");
        CancelButton.Content = Loc.T("SetCancel");
    }

    private void Sliders_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loaded) UpdateLabels();
    }

    private void UpdateLabels()
    {
        SitLabel.Text = Loc.F("SetSitLabel", (int)SitSlider.Value);
        StandLabel.Text = Loc.F("SetStandLabel", (int)StandSlider.Value);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string start = (string)StartCombo.SelectedItem;
        string end = (string)EndCombo.SelectedItem;

        if (TimeSpan.Parse(end) <= TimeSpan.Parse(start))
        {
            MessageBox.Show(Loc.T("SetValidation"),
                "StandReminder", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings.SitMinutes = (int)SitSlider.Value;
        _settings.StandMinutes = (int)StandSlider.Value;
        _settings.WorkStart = start;
        _settings.WorkEnd = end;
        _settings.PlaySound = SoundCheck.IsChecked == true;
        _settings.StartWithWindows = AutostartCheck.IsChecked == true;
        _settings.AutoUpdateCheck = AutoUpdateCheck.IsChecked == true;
        _settings.Language = LangCombo.SelectedIndex == 1 ? "en" : "sk";

        Saved?.Invoke();
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
