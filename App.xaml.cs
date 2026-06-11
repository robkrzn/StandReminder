using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace StandReminder;

public enum Phase { Idle, Sitting, Standing }

public partial class App : System.Windows.Application
{
    private static Mutex? _mutex;
    private bool _watchdogInstalled; // only the primary instance owns the health flag

    private WinForms.NotifyIcon _tray = null!;
    private DispatcherTimer _timer = null!;
    private AppSettings _settings = null!;

    private Phase _phase = Phase.Idle;
    private DateTime _phaseStart;
    private DateTime _phaseEnd;
    private bool _paused;
    private ReminderWindow? _reminder;
    private StatusWindow? _status;
    private DateTime _statusClosedAt;

    private Drawing.Icon _iconSit = null!;
    private Drawing.Icon _iconStand = null!;
    private Drawing.Icon _iconIdle = null!;

    private WinForms.ToolStripMenuItem _miStatus = null!;
    private WinForms.ToolStripMenuItem _miPause = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "StandReminder_SingleInstance", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show("StandReminder už beží (pozri systémovú lištu).", "StandReminder",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        CrashLog.Install(this);
        _watchdogInstalled = true;
        _settings = AppSettings.Load();

        _iconSit = CreateSitIcon();
        _iconStand = CreateStandIcon();
        _iconIdle = CreateIdleIcon();

        BuildTray();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();

        Tick(); // initialize immediately
    }

    // ---------- Tray ----------

    private void BuildTray()
    {
        var menu = new WinForms.ContextMenuStrip
        {
            Renderer = new DarkMenuRenderer(),
            ShowImageMargin = false,
            BackColor = DarkMenuRenderer.MenuBg,
            ForeColor = DarkMenuRenderer.Text,
            Font = new Drawing.Font("Segoe UI", 9.5f),
            Padding = new WinForms.Padding(4, 6, 4, 6)
        };
        menu.HandleCreated += (_, _) => UiNative.UseRoundedCorners(menu.Handle);

        _miStatus = new WinForms.ToolStripMenuItem("…") { Enabled = false };
        menu.Items.Add(_miStatus);
        menu.Items.Add(new WinForms.ToolStripSeparator());

        var miSwitch = new WinForms.ToolStripMenuItem("Zmeniť pozíciu teraz");
        miSwitch.Click += (_, _) => SwitchNow();
        menu.Items.Add(miSwitch);

        _miPause = new WinForms.ToolStripMenuItem("Pozastaviť pripomienky");
        _miPause.Click += (_, _) => TogglePause();
        menu.Items.Add(_miPause);

        var miSettings = new WinForms.ToolStripMenuItem("Nastavenia…");
        miSettings.Click += (_, _) => OpenSettings();
        menu.Items.Add(miSettings);

        menu.Items.Add(new WinForms.ToolStripSeparator());

        var miExit = new WinForms.ToolStripMenuItem("Ukončiť");
        miExit.Click += (_, _) => ExitApp();
        menu.Items.Add(miExit);

        foreach (WinForms.ToolStripItem item in menu.Items)
            item.Padding = new WinForms.Padding(2, 4, 2, 4);

        _tray = new WinForms.NotifyIcon
        {
            Icon = _iconIdle,
            Visible = true,
            Text = "StandReminder",
            ContextMenuStrip = menu
        };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left)
                ToggleStatus();
        };
    }

    /// <summary>Colored circle background with a white pictogram drawn on top.</summary>
    private static Drawing.Icon MakeIcon(Drawing.Color background, Action<Drawing.Graphics, Drawing.Pen> drawFigure)
    {
        using var bmp = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Drawing.Color.Transparent);
            using var brush = new Drawing.SolidBrush(background);
            g.FillEllipse(brush, 1, 1, 30, 30);

            using var pen = new Drawing.Pen(Drawing.Color.White, 2.8f)
            {
                StartCap = Drawing.Drawing2D.LineCap.Round,
                EndCap = Drawing.Drawing2D.LineCap.Round
            };
            drawFigure(g, pen);
        }
        return Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    /// <summary>Blue icon – seated stick figure in profile.</summary>
    private static Drawing.Icon CreateSitIcon() =>
        MakeIcon(Drawing.Color.FromArgb(91, 141, 239), (g, pen) =>
        {
            g.FillEllipse(Drawing.Brushes.White, 10.5f, 4.5f, 5.5f, 5.5f); // head
            g.DrawLine(pen, 13f, 11f, 13f, 19f);                          // spine
            g.DrawLine(pen, 13f, 19f, 20f, 19f);                          // thigh
            g.DrawLine(pen, 20f, 19f, 20f, 26f);                          // shin
        });

    /// <summary>Green icon – standing stick figure.</summary>
    private static Drawing.Icon CreateStandIcon() =>
        MakeIcon(Drawing.Color.FromArgb(70, 194, 142), (g, pen) =>
        {
            g.FillEllipse(Drawing.Brushes.White, 13.25f, 3.5f, 5.5f, 5.5f); // head
            g.DrawLine(pen, 16f, 10f, 16f, 19.5f);                          // body
            g.DrawLine(pen, 16f, 12.5f, 11.5f, 16.5f);                      // left arm
            g.DrawLine(pen, 16f, 12.5f, 20.5f, 16.5f);                      // right arm
            g.DrawLine(pen, 16f, 19.5f, 12.5f, 27f);                        // left leg
            g.DrawLine(pen, 16f, 19.5f, 19.5f, 27f);                        // right leg
        });

    /// <summary>Gray icon – pause bars (paused / outside work hours).</summary>
    private static Drawing.Icon CreateIdleIcon() =>
        MakeIcon(Drawing.Color.FromArgb(120, 126, 148), (g, pen) =>
        {
            pen.Width = 3.4f;
            g.DrawLine(pen, 12.5f, 11f, 12.5f, 21f);
            g.DrawLine(pen, 19.5f, 11f, 19.5f, 21f);
        });

    // ---------- Core logic ----------

    private bool InWorkHours(DateTime now)
    {
        var t = now.TimeOfDay;
        return t >= _settings.WorkStartTime && t < _settings.WorkEndTime;
    }

    private void Tick()
    {
        var now = DateTime.Now;

        if (!_paused)
        {
            bool inWork = InWorkHours(now);

            if (_phase == Phase.Idle && inWork)
            {
                StartPhase(Phase.Sitting); // work day begins seated
            }
            else if (_phase != Phase.Idle && !inWork)
            {
                _phase = Phase.Idle;       // work day over
                CloseReminder();
            }
            else if (_phase != Phase.Idle && now >= _phaseEnd && _reminder == null)
            {
                ShowReminder(_phase == Phase.Sitting ? Phase.Standing : Phase.Sitting);
            }
        }

        UpdateTray();
        PushStatus();
    }

    private void StartPhase(Phase phase)
    {
        _phase = phase;
        int minutes = phase == Phase.Sitting ? _settings.SitMinutes : _settings.StandMinutes;
        _phaseStart = DateTime.Now;
        _phaseEnd = _phaseStart.AddMinutes(minutes);
        CloseReminder();
        UpdateTray();
        PushStatus();
    }

    private void SwitchNow()
    {
        if (_phase == Phase.Idle)
            StartPhase(Phase.Standing);
        else
            StartPhase(_phase == Phase.Sitting ? Phase.Standing : Phase.Sitting);
    }

    private void TogglePause()
    {
        _paused = !_paused;
        _miPause.Text = _paused ? "Pokračovať v pripomienkach" : "Pozastaviť pripomienky";
        if (_paused)
            CloseReminder();
        else if (_phase != Phase.Idle)
            _phaseEnd = DateTime.Now.AddMinutes(_phase == Phase.Sitting ? _settings.SitMinutes : _settings.StandMinutes);
        UpdateTray();
    }

    private void ShowReminder(Phase target)
    {
        if (_settings.PlaySound)
            System.Media.SystemSounds.Exclamation.Play();

        _reminder = new ReminderWindow(target);
        _reminder.Accepted += () => StartPhase(target);
        _reminder.Snoozed += () =>
        {
            _phaseEnd = DateTime.Now.AddMinutes(5);
            CloseReminder();
        };
        _reminder.Closed += (_, _) => _reminder = null;
        _reminder.Show();
    }

    private void CloseReminder()
    {
        if (_reminder != null)
        {
            var w = _reminder;
            _reminder = null;
            w.Close();
        }
    }

    private void UpdateTray()
    {
        string text;
        Drawing.Icon icon;

        if (_paused)
        {
            icon = _iconIdle;
            text = "⏸ Pozastavené";
        }
        else if (_phase == Phase.Idle)
        {
            icon = _iconIdle;
            text = $"Mimo prac. času ({_settings.WorkStart}–{_settings.WorkEnd})";
        }
        else
        {
            var remaining = _phaseEnd - DateTime.Now;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            string pos = _phase == Phase.Sitting ? "🪑 Sedíš" : "🧍 Stojíš";
            text = $"{pos} – zostáva {remaining:mm\\:ss}";
            icon = _phase == Phase.Sitting ? _iconSit : _iconStand;
        }

        if (_tray.Icon != icon) _tray.Icon = icon;
        _tray.Text = text.Length > 63 ? text[..63] : text;
        _miStatus.Text = text;
    }

    // ---------- Status flyout ----------

    private void ToggleStatus()
    {
        if (_status != null)
        {
            _status.CloseSafe();
            return;
        }
        // clicking the tray icon deactivates (and closes) an open flyout right
        // before this handler runs — don't immediately reopen it
        if ((DateTime.Now - _statusClosedAt).TotalMilliseconds < 300)
            return;

        _status = new StatusWindow();
        _status.SettingsRequested += () =>
        {
            _status?.CloseSafe();
            OpenSettings();
        };
        _status.SwitchRequested += () => SwitchNow();
        _status.Closed += (_, _) =>
        {
            _status = null;
            _statusClosedAt = DateTime.Now;
        };
        _status.Show();
        _status.Activate();
        PushStatus();
    }

    private void PushStatus()
    {
        if (_status == null) return;

        var now = DateTime.Now;
        var elapsed = now - _phaseStart;
        var remaining = _phaseEnd - now;
        var total = _phaseEnd - _phaseStart;
        double fraction = total.TotalSeconds > 0 ? elapsed.TotalSeconds / total.TotalSeconds : 0;

        _status.UpdateView(_phase, _paused, elapsed, remaining, fraction, _settings);
    }

    // ---------- Settings ----------

    private SettingsWindow? _settingsWindow;

    private void OpenSettings()
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings);
        _settingsWindow.Saved += () =>
        {
            _settings.Save();
            ApplyAutostart(_settings.StartWithWindows);
            if (_phase != Phase.Idle && !_paused)
                StartPhase(_phase); // re-apply new interval to the current phase
        };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private static void ApplyAutostart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key == null) return;

            if (enable)
                key.SetValue("StandReminder", $"\"{Environment.ProcessPath}\"");
            else
                key.DeleteValue("StandReminder", throwOnMissingValue: false);
        }
        catch { /* no autostart rights -> ignore */ }
    }

    private void ExitApp()
    {
        _timer.Stop();
        _tray.Visible = false;
        _tray.Dispose();
        Shutdown();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        if (_watchdogInstalled)
            CrashLog.MarkCleanExit(); // covers Ukončiť as well as Windows logoff/shutdown
        base.OnExit(e);
    }
}
