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

        _loaded = true;
        UpdateLabels();
    }

    private void Sliders_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loaded) UpdateLabels();
    }

    private void UpdateLabels()
    {
        SitLabel.Text = $"🪑  Sedenie: {(int)SitSlider.Value} min";
        StandLabel.Text = $"🧍  Státie: {(int)StandSlider.Value} min";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string start = (string)StartCombo.SelectedItem;
        string end = (string)EndCombo.SelectedItem;

        if (TimeSpan.Parse(end) <= TimeSpan.Parse(start))
        {
            MessageBox.Show("Koniec pracovného času musí byť neskôr ako začiatok.",
                "StandReminder", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings.SitMinutes = (int)SitSlider.Value;
        _settings.StandMinutes = (int)StandSlider.Value;
        _settings.WorkStart = start;
        _settings.WorkEnd = end;
        _settings.PlaySound = SoundCheck.IsChecked == true;
        _settings.StartWithWindows = AutostartCheck.IsChecked == true;

        Saved?.Invoke();
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
