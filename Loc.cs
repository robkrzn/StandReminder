namespace StandReminder;

/// <summary>
/// Minimal localization: every UI string lives here as (sk, en).
/// Language comes from AppSettings.Language ("sk" / "en") and is applied
/// to <see cref="Lang"/> on startup and whenever settings are saved.
/// </summary>
internal static class Loc
{
    public static string Lang { get; set; } = "sk";

    public static string T(string key) => Lang == "en" ? S[key].en : S[key].sk;
    public static string F(string key, params object[] args) => string.Format(T(key), args);

    private static readonly Dictionary<string, (string sk, string en)> S = new()
    {
        // App
        ["AlreadyRunning"] = ("StandReminder už beží (pozri systémovú lištu).",
                             "StandReminder is already running (check the system tray)."),

        // Tray menu
        ["MenuSwitchNow"] = ("Zmeniť pozíciu teraz", "Switch position now"),
        ["MenuPause"] = ("Pozastaviť pripomienky", "Pause reminders"),
        ["MenuResume"] = ("Pokračovať v pripomienkach", "Resume reminders"),
        ["MenuSettings"] = ("Nastavenia…", "Settings…"),
        ["MenuExit"] = ("Ukončiť", "Exit"),

        // Tray tooltip / status line
        ["TrayPaused"] = ("⏸ Pozastavené", "⏸ Paused"),
        ["TrayIdle"] = ("Mimo prac. času ({0}–{1})", "Outside work hours ({0}–{1})"),
        ["TraySitting"] = ("🪑 Sedíš – zostáva {0}", "🪑 Sitting – {0} left"),
        ["TrayStanding"] = ("🧍 Stojíš – zostáva {0}", "🧍 Standing – {0} left"),

        // Reminder popup
        ["RemTitleStand"] = ("Čas postaviť sa!", "Time to stand up!"),
        ["RemBodyStand"] = ("Zdvihni stôl a pracuj v stoji ďalších {0} minút.",
                           "Raise the desk and work standing for the next {0} minutes."),
        ["RemBtnStand"] = ("Stojím ✓", "Standing ✓"),
        ["RemTitleSit"] = ("Môžeš si sadnúť", "You can sit down"),
        ["RemBodySit"] = ("Spusti stôl a pohodlne si sadni na {0} minút.",
                         "Lower the desk and sit back for {0} minutes."),
        ["RemBtnSit"] = ("Sedím ✓", "Sitting ✓"),

        // Status flyout
        ["StPaused"] = ("Pozastavené", "Paused"),
        ["StPausedSub"] = ("Pripomienky sú vypnuté", "Reminders are turned off"),
        ["StIdle"] = ("Mimo pracovného času", "Outside work hours"),
        ["StIdleSub"] = ("Pripomínam medzi {0} a {1}", "Reminders run between {0} and {1}"),
        ["StSitting"] = ("Sedíš", "Sitting"),
        ["StStanding"] = ("Stojíš", "Standing"),
        ["StElapsed"] = ("V tejto pozícii už {0}", "In this position for {0}"),
        ["StTimeToStand"] = ("Čas postaviť sa!", "Time to stand up!"),
        ["StTimeToSit"] = ("Môžeš si sadnúť", "You can sit down"),
        ["StRemainStand"] = ("Zostáva {0} do státia", "{0} until standing"),
        ["StRemainSit"] = ("Zostáva {0} do sedenia", "{0} until sitting"),
        ["StSettingsTip"] = ("Nastavenia", "Settings"),

        // Settings window
        ["SetTitle"] = ("StandReminder – Nastavenia", "StandReminder – Settings"),
        ["SetHeader"] = ("Nastavenia", "Settings"),
        ["SetIntervals"] = ("Intervaly zmeny pozície", "Position change intervals"),
        ["SetSitLabel"] = ("🪑  Sedenie: {0} min", "🪑  Sitting: {0} min"),
        ["SetStandLabel"] = ("🧍  Státie: {0} min", "🧍  Standing: {0} min"),
        ["SetRatioHint"] = ("Odporúčaný pomer sedenie : státie je 2:1 až 3:1.",
                           "Recommended sit : stand ratio is 2:1 to 3:1."),
        ["SetWorkHours"] = ("Pracovný čas", "Work hours"),
        ["SetFrom"] = ("Od", "From"),
        ["SetTo"] = ("do", "to"),
        ["SetWorkHint"] = ("Mimo tohto času aplikácia nepripomína.",
                          "The app stays silent outside these hours."),
        ["SetSound"] = ("Zvukové upozornenie", "Sound notification"),
        ["SetAutostart"] = ("Spustiť automaticky pri štarte Windows", "Start automatically with Windows"),
        ["SetLanguage"] = ("Jazyk", "Language"),
        ["SetSave"] = ("Uložiť", "Save"),
        ["SetCancel"] = ("Zrušiť", "Cancel"),
        ["SetValidation"] = ("Koniec pracovného času musí byť neskôr ako začiatok.",
                            "Work end time must be later than start time."),
    };
}
